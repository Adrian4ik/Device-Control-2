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
        #region Показать
        bool is_first = true,
            to_survey = true; // переменная наличия связи с интернетом / кабелем

        bool[] icmp_connection = new bool[10];
        bool[] snmp_connection = new bool[10];

        int conn_state_counter = 0, row_counter = 0, ri_counter = 0; // номер строки в таблице интерфейсов

        string[,] if_table; // таблица с 5-ю столбцами, доп. столбец заполняется в string[] ifnames
                            // данная таблица хранит в себе все действующие oid'ы из таблицы интерфейсов

        // последовательность опроса устройства:
        // 1 - icmp
        // 2 - snmp standart
        // 3 - snmp inteface table
        // 4 - snmp system time
        // 5 - snmp temperature
        // 6 - snmp additional

        Ping ping = new Ping();

        AutoResetEvent waiter = new AutoResetEvent(false);

        RawDeviceList.Client cl;

        Survey survey;
        Logs log = new Logs(); // простые изменения кидать в логи отсюда, а в форму передавать лишь нештатные состояния

        Action<Status> localResult;
        Action<Form1.note> localNote;

        public struct Status
        {
            public int icmp_conn; // 1 - отлично, 3 - плохо
            public int snmp_conn; // 0 - отлично, 1 - не очень, 2 - плохо

            public int[] interface_list;

            public string info_updated_time;
            public string SysTime;

            public string[] standart;
            public string[] temperatures;
            public string[] ifnames;

            public string[,] additional;
            public string[,] interface_table;
        }

        public Status status = new Status();

        Form1.note notification = new Form1.note();

        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        #endregion

        public DeviceInfo(RawDeviceList.Client client)
        {
            cl = client;

            ping.PingCompleted += new PingCompletedEventHandler(Received_ping_reply);

            timer.Interval = 6000;
            timer.Tick += new EventHandler(TimerTick);

            notification.id = cl.id;
            notification.type = new bool[5];

            if(cl.Addition != null)
                notification.add_type = new bool[cl.Addition.Length];

            for (int i = 0; i < 5; i++) { notification.type[i] = false; }
        }

        public void RegisterCallback(Action<Status> callback)
        {
            localResult = callback;
        }

        public void RegisterCallback(Action<Form1.note> callback)
        {
            localNote = callback;

            TryPing();
        }

        void TryPing()
        {
            if (cl.Connect)
            {
                try { ping.SendAsync(cl.Ip, 3000, waiter); }
                catch
                {
                    to_survey = false;

                    notification.type[0] = true;
                    PostAsyncNotification(notification);

                    if (!timer.Enabled)
                        timer.Start();
                }
            }
        } // 1



        void Received_ping_reply(object sender, PingCompletedEventArgs e)
        {
            if (e.Cancelled)
                ((AutoResetEvent)e.UserState).Set();

            if (e.Error != null)
                ((AutoResetEvent)e.UserState).Set();

            // Let the main thread resume.
            ((AutoResetEvent)e.UserState).Set();

            notification.type[0] = false;

            if (e.Reply.Status == IPStatus.Success)
            {
                icmp_connection[conn_state_counter] = true;

                notification.type[1] = false;

                survey = new Survey(cl.Ip);
                survey.RegisterCallback(GetStandart);
            }
            else
            {
                icmp_connection[conn_state_counter] = false;

                notification.type[1] = true;
                PostAsyncNotification(notification);

                if (!timer.Enabled)
                    timer.Start();
            }
        } // 1 res & 2
        


        void GetStandart(Form1.snmp_result res) //---------------------------------------------------------------------------
        {
            notification.type[1] = false;

            if (res.vb != null)
            {
                snmp_connection[conn_state_counter] = true;

                notification.type[2] = false;

                if (is_first) // is_first можно убрать
                {
                    status.standart = new string[5];

                    int i = 0;

                    foreach (Vb vb in res.vb) { status.standart[i++] = vb.Value.ToString(); }

                    status.interface_list = new int[int.Parse(status.standart[4])];

                    if (row_counter == 0)
                        NextRow();
                    else
                        is_first = false;
                }
                else if (res.vb != null)
                    InspectStdChanges(res.vb);

                UpdateInfo(); // скорее всего требуется в другом месте (в конце опроса устройства)
            }
            else
            {
                snmp_connection[conn_state_counter] = false;

                notification.type[2] = true;
                PostAsyncNotification(notification);

                if (!timer.Enabled)
                    timer.Start();
            }

            if (conn_state_counter == 10)
                AnalyzeConnection(); //----------------------------------------------
            else
                conn_state_counter++;
        } // 2 res & 3

        void AnalyzeConnection()
        {
            int bad_connections = 0;

            foreach(bool conn_state in icmp_connection)
            {
                if (!conn_state)
                    bad_connections++;
            }

            /*if (bad_connections > 0)
            {*/
                if (bad_connections == 10)
                    status.icmp_conn = 3;
                else
                    status.icmp_conn = 1;
            /*}
            else
                status.icmp_conn = 2;*/

            bad_connections = 0;

            foreach (bool conn_state in snmp_connection)
            {
                if (!conn_state)
                    bad_connections++;
            }

            if (bad_connections > 0)
            {
                if (bad_connections == 10)
                    status.snmp_conn = 2;
                else
                    status.snmp_conn = 1;
            }
            else
                status.snmp_conn = 0;

            conn_state_counter = 0;
        }

        void NextRow()
        {
            row_counter++;

            string[] arr = new string[5];

            arr[0] = "1.3.6.1.2.1.2.2.1.1." + row_counter; // 1 столбец
            arr[1] = "1.3.6.1.2.1.2.2.1.2." + row_counter; // 2 столбец

            arr[2] = "1.3.6.1.2.1.2.2.1.8." + row_counter; // 4 столбец
            arr[3] = "1.3.6.1.2.1.2.2.1.5." + row_counter; // 5 столбец
            arr[4] = "1.3.6.1.2.1.2.2.1.3." + row_counter; // 6 столбец

            if (ri_counter + 1 == status.interface_list.Length)
            {
                survey = new Survey(cl.Ip, arr);
                survey.RegisterCallback(Save);
            }
        }

        void Save(Form1.snmp_result res)
        {
            // Первый раз опрашивает устройство и записывает его таблицу интерфейсов для исключения в дальнейшем пустых опросов

            if (res.Ip != cl.Ip)
                MessageBox.Show("Ничоси, ip " + res.Ip.ToString() + " не совпадает с клиентом " + cl.Name);
            else if(res.vb != null)
            {
                status.interface_list[ri_counter] = row_counter;
                
                for (int i = 0; i < 5; i++)
                {
                    if_table[ri_counter, i] = res.vb[i].Oid.ToString();
                    status.interface_table[ri_counter, i] = res.vb[i].Value.ToString();
                }

                ri_counter++;
            }
        } //---------------------------------------------------------------------------

        void InspectStdChanges(Vb[] vbs)
        {
            int i = 0;

            foreach (Vb vb in vbs) { status.standart[i++] = vb.Value.ToString(); }

            status.interface_list = new int[int.Parse(status.standart[4])];

            survey = new Survey(cl.Ip, if_table);
            survey.RegisterCallback(RewriteTable);
        } //---------------------------------------------------------------------------

        void RewriteTable(Form1.snmp_result res)
        {
            for (int i = 0; i < res.vb.Length / 5; i++)
                for (int j = 0; j < 5; j++)
                    status.interface_table[i, j] = res.vb[i * 5 + j].Value.ToString();
        }

        void UpdateInfo()
        {
            string time = (DateTime.Now.Hour < 10) ? "0" + DateTime.Now.Hour + ":" : DateTime.Now.Hour + ":";
            time += (DateTime.Now.Minute < 10) ? "0" + DateTime.Now.Minute : DateTime.Now.Minute.ToString();
            status.info_updated_time = "Последний раз обновлено: " + time;
        }

        void TimerTick(object sender, EventArgs e)
        {
            TryPing();
        }



        public void ChangeStat(Form1.snmp_result trap)
        {
            // сканируем все oid'ы trap'а на соответствие и возможно выдаём нештатку
        } // метод применяемый при поимке snmp trap



        public delegate void PostAsyncResultDelegate(Status result);

        protected void PostAsyncResult(Status result)
        {
            localResult?.Invoke(result);
        }

        public delegate void PostAsyncNotificationDelegate(Form1.note result);

        protected void PostAsyncNotification(Form1.note result)
        {
            localNote?.Invoke(result);
        }
    }
}
