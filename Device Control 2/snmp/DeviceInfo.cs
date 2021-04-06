using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using SnmpSharpNet;
using Device_Control_2.Features;

namespace Device_Control_2.snmp
{
    class DeviceInfo
    {
        Action<Status> localResult;
        Action<string> localError;

        Pdu std = new Pdu(PduType.Get);

        Ping ping = new Ping();

        AutoResetEvent waiter = new AutoResetEvent(false);

        RawDeviceList.Client cl;

        Survey survey;

        bool is_first = true;

        public struct Status
        {
            public int icmp_conn;
            public int snmp_conn;

            public int[] interface_list;

            public string info_updated_time;
            public string SysTime;

            public string[] standart;
            public string[] temperatures;

            public string[,] additional;
            public string[,] interface_table;
        }

        public Status status = new Status();

        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();

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
                status.icmp_conn = 0;
                status.snmp_conn = 0;

                ping.PingCompleted += new PingCompletedEventHandler(Received_ping_reply);

                timer.Interval = 6000;
                timer.Tick += new EventHandler(TimerTick);

                survey = new Survey(cl.Ip, std);
            }
        }

        private void Received_ping_reply(object sender, PingCompletedEventArgs e)
        {
            if (e.Cancelled)
                ((AutoResetEvent)e.UserState).Set();

            if (e.Error != null)
                ((AutoResetEvent)e.UserState).Set();

            // Let the main thread resume.
            ((AutoResetEvent)e.UserState).Set();

            if (e.Reply.Status == IPStatus.Success)
            {
                status.icmp_conn = 2;

                survey.RegisterCallback(GetStandart);
            }
            else
                status.icmp_conn = 0;
        }

        public void Init()
        {

        }

        public void Save() //---------------------------------------------------------------------------
        {
            // Первый раз опрашивает устройство и записывает его таблицу интерфейсов для исключения в дальнейшем пустых опросов

            status.standart = new string[5];

            int i = 0;

            foreach (Vb vb in vbs) { status.standart[i++] = vb.Value.ToString(); }

            status.interface_list = new int[int.Parse(status.standart[4])];

            is_first = false;
        }

        private void Inspect(Vb[] vbs) //---------------------------------------------------------------------------
        {
            int i = 0;

            foreach (Vb vb in vbs) { status.standart[i++] = vb.Value.ToString(); }

            status.interface_list = new int[int.Parse(status.standart[4])];
        }

        private string UpdateInfo()
        {
            string time = (DateTime.Now.Hour < 10) ? "0" + DateTime.Now.Hour + ":" : DateTime.Now.Hour + ":";
            time += (DateTime.Now.Minute < 10) ? "0" + DateTime.Now.Minute : DateTime.Now.Minute.ToString();
            return "Последний раз обновлено: " + time;
        }

        private void TimerTick(object sender, EventArgs e)
        {
            bool to_survey = true;

            try { ping.SendAsync(cl.Ip, 3000, waiter); }
            catch { to_survey = false; }

            if (to_survey)
            {
                survey.RegisterCallback(GetStandart);
            }
        }

        void GetStandart(Form1.snmp_result res) //---------------------------------------------------------------------------
        {
            if (res.vb != null)
            {
                status.snmp_conn = 2;

                if (is_first)
                    Save();
                else if (res.vb != null)
                    Inspect(res.vb);

                status.info_updated_time = UpdateInfo();
            }
            else
                status.snmp_conn = 0;
        }

        public delegate void PostAsyncResultDelegate(Status result);

        protected void PostAsyncResult(Status result)
        {
            localResult?.Invoke(result);
        }

        public delegate void ResultDelegate(string result);

        protected void Result(string result)
        {
            localError?.Invoke(result);
        }
    }
}
