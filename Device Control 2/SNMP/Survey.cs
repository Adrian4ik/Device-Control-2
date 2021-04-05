using System;
using System.Collections.Generic;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using SnmpSharpNet;

namespace Device_Control_2.snmp
{
    class Survey
    {
		Action<Form1.snmp_result> localResult;
		Action<string> localError;

		IPAddress ip;
		Pdu list;

		public Survey(IPAddress address, Pdu pdu)
		{
			ip = address;
			list = pdu;
		}

		private void SurveyList()
		{
			// Define agent parameters class
			AgentParameters param = new AgentParameters(new OctetString("public"));
			// Set SNMP version to 1 (or 2)
			param.Version = SnmpVersion.Ver1;
			// Construct target
			UdpTarget target = new UdpTarget(ip, 161, 2000, 1);

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

		private void ReceiveCallback(AsyncRequestResult result, SnmpPacket packet) // program itself
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

		public void RegisterCallback(Action<string> callback)
		{
			localError = callback;
		}

		protected void PostAsyncResult(Form1.snmp_result result)
		{
			localResult?.Invoke(result);
		}

		public void RegisterCallback(Action<Form1.snmp_result> callback)
		{
			localResult = callback;

			SurveyList();
		}
	}
}
