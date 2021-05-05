using System;
using System.Collections.Generic;
//using System.Linq;
using System.Net;
//using System.Net.Sockets;
//using System.Text;
//using System.Threading.Tasks;
using SnmpSharpNet;

namespace Device_Control_2.snmp
{
    class Survey
    {
		Action<Form1.snmp_result> localResult;
		Action<string> localError;

		IPAddress ip;
		Pdu list = new Pdu();

		/// <summary>
		/// Опрашивает список стандартных oid'ов устройства
		/// </summary>
		public Survey(IPAddress address, Action<Form1.snmp_result> callback, Action<string> error_callback)
		{
			list.VbList.Add("1.3.6.1.2.1.1.1.0"); // sysDescr
			list.VbList.Add("1.3.6.1.2.1.1.3.0"); // sysUpTime
			list.VbList.Add("1.3.6.1.2.1.1.5.0"); // sysName
			list.VbList.Add("1.3.6.1.2.1.1.6.0"); // sysLocation
			list.VbList.Add("1.3.6.1.2.1.2.1.0"); // ifNumber

			ip = address;
			localResult = callback;
			localError = error_callback;
		}

		public Survey(IPAddress address, string oid, Action<Form1.snmp_result> callback, Action<string> error_callback)
		{
			if (oid != null)
			{
				list.VbList.Add(oid);

				Init(oid, address, callback, error_callback);
			}
		}

		public Survey(IPAddress address, string[] oid_list, Action<Form1.snmp_result> callback, Action<string> error_callback)
		{
			if(oid_list != null)
			{
				foreach (string oid in oid_list)
				{
					if (oid != null)
						list.VbList.Add(oid);
				}

				Init(oid_list, address, callback, error_callback);
			}
		}

		public Survey(IPAddress address, string[,] iftable, Action<Form1.snmp_result> callback, Action<string> error_callback)
        {
			if (iftable != null)
			{
				for (int i = 0; i < iftable.Length / 5; i++)
					for (int j = 0; j < 5; j++)
						if (iftable[i, j] != null)
							list.VbList.Add(iftable[i, j]);

				Init(iftable, address, callback, error_callback);
			}
		}

		public Survey(IPAddress address, List<string> oids, Action<Form1.snmp_result> callback, Action<string> error_callback)
        {
			if (oids != null)
            {
				foreach (string oid in oids)
					if (oid != null)
						list.VbList.Add(oid);

				ip = address;
				localResult = callback;
				localError = error_callback;
			}
        }

		void Init(object obj, IPAddress address, Action<Form1.snmp_result> callback, Action<string> error_callback)
		{
			if(obj != null)
			{
				FillList(obj);

				ip = address;
				localResult = callback;
				localError = error_callback;
			}
		}

		void FillList(object obj)
        {
			if (obj.GetType() == typeof(string))
				list.VbList.Add((string)obj);
			else if((obj.GetType() == typeof(string[])))
			{
				foreach (string oid in (string[])obj)
					if (oid != null)
						list.VbList.Add(oid);
			}
			else if ((obj.GetType() == typeof(string[,])))
			{
				foreach (string oid in (string[,])obj)
					if (oid != null)
						list.VbList.Add(oid);

				/*string[,] iftable = (string[,])obj;

				for (int i = 0; i < iftable.Length / 5; i++)
					for (int j = 0; j < 5; j++)
						if (iftable[i, j] != null)
							list.VbList.Add(iftable[i, j]);*/
			}

			/*foreach (string oid in obj)
            {

            }*/
		}

		public void Start()
		{
			if (list.VbCount != 0)
				SurveyList();
		}

		void SurveyList()
		{
			// Define agent parameters class
			AgentParameters param = new AgentParameters(SnmpVersion.Ver1, new OctetString("public"));
			// Construct target
			UdpTarget target = new UdpTarget(ip, 161, 2000, 0);

			try
			{
				// Make SNMP request
				target.RequestAsync(list, param, new SnmpAsyncResponse(ReceiveCallback));
			}
			catch
			{
				PostAsyncResult("Связь с устройством: [SNMP]= ");
			}
		}

		void ReceiveCallback(AsyncRequestResult result, SnmpPacket packet) // program itself
		{
			if (packet != null)
			{
				Form1.snmp_result res = new Form1.snmp_result();
				res.Ip = ip;
				res.vb = new Vb[packet.Pdu.VbList.Count];

				int i = 0;

				foreach (Vb vb in packet.Pdu.VbList)
				{
					res.vb[i++] = vb;
				}

				PostAsyncResult(res);
			}
		}

		protected void PostAsyncResult(string msg)
		{
			localError?.Invoke(msg);
		}

		protected void PostAsyncResult(Form1.snmp_result result)
		{
			localResult?.Invoke(result);
		}
	}
}
