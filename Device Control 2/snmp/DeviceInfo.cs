using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;
using SnmpSharpNet;

namespace Device_Control_2.snmp
{
    class DeviceInfo
    {
        Action<Form1.snmp_result> localResult;
        Action<string> localError;

        Pdu std = new Pdu(PduType.Get);

        RawDeviceList.Client cl;

        Survey survey;

        public struct Status
        {
            public int[] interface_list;

            public string info_updated_time;
            public string SysTime;

            public string[] standart;
            public string[] temperatures;

            public string[,] additional;
            public string[,] interface_table;
        }

        public Status status = new Status();

        Timer timer = new Timer();

        public DeviceInfo(RawDeviceList.Client client)
        {
            cl = client;

            std.VbList.Add("1.3.6.1.2.1.1.1.0"); // sysDescr
            std.VbList.Add("1.3.6.1.2.1.1.3.0"); // sysUpTime
            std.VbList.Add("1.3.6.1.2.1.1.5.0"); // sysName
            std.VbList.Add("1.3.6.1.2.1.1.6.0"); // sysLocation
            std.VbList.Add("1.3.6.1.2.1.2.1.0"); // ifNumber

            if (cl.Connect)
            {
                Inspect();
                timer.Interval = 6000;
                timer.Tick += new EventHandler(InspectTimer);
            }
        }

        public void Init()
        {

        }

        public void Save()
        {

        }

        private void Inspect()
        {
            // Первый раз опрашивает устройство и записывает его таблицу интерфейсов для исключения в дальнейшем пустых опросов

            survey = new Survey(cl.Ip, std);
            survey.RegisterCallback(GetStandart);
        }

        private string UpdateInfo()
        {
            //Survey;

            string time = (DateTime.Now.Hour < 10) ? "0" + DateTime.Now.Hour + ":" : DateTime.Now.Hour + ":";
            time += (DateTime.Now.Minute < 10) ? "0" + DateTime.Now.Minute : DateTime.Now.Minute.ToString();
            return "Последний раз обновлено: " + time;
        }

        private void InspectTimer(object sender, EventArgs e)
        {
            Inspect();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            //UpdateInfo();
        }

        void GetStandart(Form1.snmp_result res)
        {
            /*if (InvokeRequired)
                Invoke(new PostAsyncResultDelegate(GetResult), new object[] { res });
            else
            {
                dataGridView1.Rows.Add(res.Ip, "");

                foreach (Vb vb in res.vb)
                {
                    dataGridView1.Rows.Add(vb.Oid, vb.Value);
                }
            }*/

            status.standart = new string[5];

            int i = 0;

            foreach (Vb vb in res.vb)
            {
                status.standart[i++] = vb.Value.ToString();
            }

            status.interface_list = new int[int.Parse(status.standart[4])];

            status.info_updated_time = UpdateInfo();
        }

        public delegate void PostAsyncResultDelegate(Form1.snmp_result res);

        protected void PostAsyncResult(Form1.snmp_result result)
        {
            localResult?.Invoke(result);
        }
    }
}
