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

        int counter = 0, ri_counter = 0;

        string[,] if_table; // таблица с 5-ю столбцами, доп. столбец заполняется в string[] ifnames
                            // данная таблица хранит в себе все действующие oid'ы из таблицы интерфейсов

        bool[] notifications = new bool[7]; // список уведомлений (false означает, что всё нормально)
                                            // если связь плохая, то в ячейке утеранной связи должно стоять false
                                            // 
                                            // 0 - связь icmp утеряна
                                            // 1 - связь icmp плохая
                                            // 2 - связь snmp утеряна
                                            // 3 - связь snmp плохая
                                            // 4 - нештатка по температуре
                                            // 5 - нештатка по питанию
                                            // 6 - доп. нештатка, если присутствует

        // такая же и последовательность опроса устройства:
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
        Action<bool[]> localChanges;

        public struct Status
        {
            public int icmp_conn;
            public int snmp_conn;

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

        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        #endregion

        public DeviceInfo(RawDeviceList.Client client)
        {
            cl = client;

            if (cl.Connect)
            {
                status.icmp_conn = 0;
                status.snmp_conn = 0;

                ping.PingCompleted += new PingCompletedEventHandler(Received_ping_reply);

                timer.Interval = 6000;
                timer.Tick += new EventHandler(TimerTick);
            }
        }

        public void RegisterCallback(Action<Status> callback)
        {
            localResult = callback;

            TryPing();
        }

        void TryPing()
        {
            try { ping.SendAsync(cl.Ip, 3000, waiter); }
            catch { to_survey = false; }
        }

        void Received_ping_reply(object sender, PingCompletedEventArgs e)
        {
            if (e.Cancelled)
                ((AutoResetEvent)e.UserState).Set();

            if (e.Error != null)
                ((AutoResetEvent)e.UserState).Set();

            // Let the main thread resume.
            ((AutoResetEvent)e.UserState).Set();

            if (to_survey)
            {
                survey = new Survey(cl.Ip);
                survey.RegisterCallback(GetStandart);
            }

            if (e.Reply.Status == IPStatus.Success)
            {
                status.icmp_conn = 2;

                survey = new Survey(cl.Ip);
                survey.RegisterCallback(GetStandart);
            }
            else
                status.icmp_conn = 0;
        } // устранить повторение RegisterCallback(GetStandart)

        void GetStandart(Form1.snmp_result res)
        {
            if (res.vb != null)
            {
                status.snmp_conn = 2;

                if (is_first) // is_first можно убрать
                {
                    status.standart = new string[5];

                    int i = 0;

                    foreach (Vb vb in res.vb) { status.standart[i++] = vb.Value.ToString(); }

                    status.interface_list = new int[int.Parse(status.standart[4])];

                    if (counter == 0)
                        NextRow();

                    if (cl.SysTime != null)
                    {
                        survey = new Survey(cl.Ip, cl.SysTime);
                        survey.RegisterCallback(GetStandart);
                    }
                    else if (cl.Temperature != null)
                    {
                        string[] arr = new string[cl.Temperature.Length / 3];

                        for (int j = 0; j < arr.Length; j++)
                        {
                            arr[j] = cl.Temperature[j, 0];
                        }

                        survey = new Survey(cl.Ip, arr);
                        survey.RegisterCallback(GetStandart);
                    }
                    else
                        is_first = false;
                }
                else if (res.vb != null)
                    InspectStdChanges(res.vb);

                UpdateInfo(); // скорее всего требуется в другом месте (в конце опроса устройства)
            }
            else
                status.snmp_conn = 0;
        } //---------------------------------------------------------------------------

        void NextRow()
        {
            counter++;

            string[] arr = new string[5];

            arr[0] = "1.3.6.1.2.1.2.2.1.1." + counter; // 1 столбец
            arr[1] = "1.3.6.1.2.1.2.2.1.2." + counter; // 2 столбец

            arr[2] = "1.3.6.1.2.1.2.2.1.8." + counter; // 4 столбец
            arr[3] = "1.3.6.1.2.1.2.2.1.5." + counter; // 5 столбец
            arr[4] = "1.3.6.1.2.1.2.2.1.3." + counter; // 6 столбец

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
                status.interface_list[ri_counter] = counter;
                
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
            if (cl.Connect)
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

        public delegate void ResultDelegate(string result);

        protected void Result(bool[] result)
        {
            localChanges?.Invoke(result);
        }
    }
}
