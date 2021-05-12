using System;
//using System.Collections.Generic;
//using System.Linq;
using System.Net.NetworkInformation;
//using System.Text;
using System.Threading;
//using System.Threading.Tasks;
//using System.Windows.Forms;
using SnmpSharpNet;
using Device_Control_2.Features;

namespace Device_Control_2.snmp
{
    class DeviceInfo
    {
        #region Показать
        bool is_first = true,
            to_survey = true; // переменная наличия связи с интернетом / кабелем

        bool[] survey_exists = new bool[6],
            icmp_connection = new bool[10],
            snmp_connection = new bool[10];

        int step = 0,
            conn_state_counter = 0, row_counter = 0, ri_counter = 0; // номер строки в таблице интерфейсов

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

        Survey[] survey = new Survey[7];
        Logs log = new Logs(); // простые изменения кидать в логи отсюда, а в форму передавать лишь нештатные состояния

        Action<Status> localResult;
        Action<Form1.note> localNote;

        public struct Status
        {
            public int icmp_conn; // 1 - отлично, 3 - плохо
            public int snmp_conn; // 0 - отлично, 1 - не очень, 2 - плохо

            public int id;
            public int interface_count;

            public int[] interface_list;

            public string info_updated_time;
            public string SysTime; // survey 2

            public string[] standart; // survey 0
            public string[] temperatures; // survey 3
            public string[] ifnames; // survey 4

            public string[,] additional; // survey 5
            public string[,] interface_table; // survey 1
        }

        public Status status = new Status();

        Form1.note notification = new Form1.note();

        System.Windows.Forms.Timer timer = new System.Windows.Forms.Timer();
        #endregion

        public DeviceInfo(RawDeviceList.Client client, Action<Status> status_callback, Action<Form1.note> notification_callback)
        {
            cl = client;

            ping.PingCompleted += new PingCompletedEventHandler(Received_ping_reply);

            timer.Interval = 6000;
            timer.Tick += new EventHandler(TimerTick);

            status.id = cl.id;
            status.interface_list = new int[0];
            status.temperatures = new string[0];
            status.ifnames = new string[0];
            status.additional = new string[0, 0];
            status.interface_table = new string[0, 0];

            notification.id = cl.id;
            notification.type = new bool[5];

            if(cl.Addition != null)
                notification.add_type = new bool[cl.Addition.Length];

            for (int i = 0; i < 5; i++) { notification.type[i] = false; }

            localResult = status_callback;
            localNote = notification_callback;

            FillSurveyArray();
            TryPing();
        }



        /// <summary>
        /// Заполняет массив Survey каждого клиента
        /// </summary>
        void FillSurveyArray()
        {
            survey[0] = new Survey(cl.Ip, GetStandart, GetError);

            string[,] table = new string[100, 6];

            for (int i = 0; i < 100; i++)
            {
                table[i, 0] = "1.3.6.1.2.1.2.2.1.1." + (i + 1); // 1 столбец
                table[i, 1] = "1.3.6.1.2.1.2.2.1.2." + (i + 1); // 2 столбец

                table[i, 2] = "1.3.6.1.2.1.2.2.1.8." + (i + 1); // 4 столбец
                table[i, 3] = "1.3.6.1.2.1.2.2.1.5." + (i + 1); // 5 столбец
                table[i, 4] = "1.3.6.1.2.1.2.2.1.3." + (i + 1); // 6 столбец
            }

            survey[1] = new Survey(cl.Ip, table, GetTable, GetError);
            survey[2] = new Survey(cl.Ip, cl.SysTime, GetSysTime, GetError);

            if (cl.Temperature != null)
            {
                string[] temp = new string[cl.Temperature.Length / 3];

                for (int i = 0; i < temp.Length; i++) { temp[i] = cl.Temperature[i, 0]; }

                survey[3] = new Survey(cl.Ip, temp, GetTemperatures, GetError);
            }

            if (cl.Addition != null)
            {
                string[] add = new string[cl.Addition.Length / 6];

                for (int i = 0; i < add.Length; i++) { add[i] = cl.Addition[i, 0]; }

                survey[5] = new Survey(cl.Ip, add, GetAdditional, GetError);
            }

            survey[6] = new Survey(cl.Ip, "1.3.6.1.2.1.1.1.0", GetConnection, GetError);
        }

        /*public void RegisterCallback(Action<Status> callback)
        {
            localResult = callback;
        }

        public void RegisterCallback(Action<Form1.note> callback)
        {
            localNote = callback;

            TryPing();
        }*/



        void TryPing()
        {
            if (cl.Connect)
            {
                try { ping.SendAsync(cl.Ip, 3000, waiter); }
                catch
                {
                    //to_survey = false;

                    notification.type[0] = true;
                    PostAsyncNotification(notification);

                    if (!timer.Enabled)
                        timer.Start();
                }
            }
            else
            {
                status.icmp_conn = 5;
                status.snmp_conn = 5;
                UpdateInfo();
                PostAsyncResult(status);
            }
        } // 1

        void Received_ping_reply(object sender, PingCompletedEventArgs e)
        {
            if (timer.Enabled)
                timer.Stop();

            if (e.Cancelled || e.Error != null)
                ((AutoResetEvent)e.UserState).Set();

            // Let the main thread resume.
            ((AutoResetEvent)e.UserState).Set();

            notification.type[0] = false;

            if (e.Reply.Status == IPStatus.Success)
            {
                icmp_connection[conn_state_counter] = true;

                notification.type[1] = false;

                status.icmp_conn = 1;

                //if (!survey_exists[0])
                //{
                //    survey_exists[0] = true;
                //    getstandart(survey[0].snmpsurvey()); // -----------------------
                //}
                //else
                //    getstandart(survey[0].snmpsurvey()); // -----------------------

                GetConnection(survey[6].snmpSurvey());
            }
            else
            {
                icmp_connection[conn_state_counter] = false;

                status.icmp_conn = 3;
                status.snmp_conn = 2;

                notification.type[1] = true;
                PostAsyncNotification(notification);
            }

            //if (!timer.Enabled)
            //    timer.Start();

            UpdateInfo();
            PostAsyncResult(status);
        }



        void GetConnection(Form1.snmp_result res)
        {
            notification.type[1] = false;

            conn_state_counter++;

            if (res.vb == null)
                RestartConnection();

            if (conn_state_counter == 10)
                AnalyzeConnection(); //----------------------------------------------

            UpdateInfo();
            PostAsyncResult(status);

            if (conn_state_counter == 10)
            {
                conn_state_counter = 0;

                if (timer.Enabled)
                    timer.Stop();

                GetNext();
            }
            else if (!timer.Enabled)
                timer.Start();

        }

        void RestartConnection() //----------------------------------------------
        {
            snmp_connection[conn_state_counter] = false;

            notification.type[2] = true;
            PostAsyncNotification(notification);

            status.snmp_conn = 1;

            if (!timer.Enabled)
                timer.Start();
        }

        void AnalyzeConnection()
        {
            int bad_connections = 0;

            foreach (bool conn_state in icmp_connection)
            {
                if (!conn_state)
                    bad_connections++;
            }

            if (bad_connections > 0)
            {
                if (bad_connections == 10)
                    status.icmp_conn = 3;
                else
                    status.icmp_conn = 1;
            }
            else
                status.icmp_conn = 2;

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
        }



        void GetStandart(Form1.snmp_result res) //---------------------------------------------------------------------------
        {
            if (res.vb != null)
            {
                snmp_connection[conn_state_counter] = true;

                notification.type[2] = false;

                UpdateInfo();
                PostAsyncResult(status);

                status.standart = new string[5];

                status.snmp_conn = 0;

                int i = 0;

                foreach (Vb vb in res.vb)
                {
                    if (status.standart[i] != vb.Value.ToString())
                        log.Write(cl.Name, "Значение переменной было изменено: [" + "]=" + vb.Value.ToString()); //\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\\

                    status.standart[i++] = vb.Value.ToString();
                }

                status.interface_list = new int[int.Parse(status.standart[4])];

                if (row_counter == 0)
                {
                    if_table = new string[int.Parse(status.standart[4]), 5];
                    status.interface_table = new string[int.Parse(status.standart[4]), 5];

                    NextRow();
                }
                else
                {
                    is_first = false;

                    if (!survey_exists[1])
                    {
                        //survey[1] = new Survey(cl.Ip, if_table, RewriteTable, GetError);
                        survey_exists[1] = true;
                        //RewriteTable(survey[1].snmpSurvey());
                    }
                    else
                        survey[1].snmpSurvey();

                    //GetNext();
                }
            }
        } // 2 res & 3

        void InspectStdChanges(Vb[] vbs)
        {
            int i = 0;

            foreach (Vb vb in vbs) { status.standart[i++] = vb.Value.ToString(); }

            status.interface_list = new int[int.Parse(status.standart[4])];
        } //---------------------------------------------------------------------------



        void NextRow()
        {
            row_counter++;

            string[] arr = new string[5];

            arr[0] = "1.3.6.1.2.1.2.2.1.1." + row_counter; // 1 столбец
            arr[1] = "1.3.6.1.2.1.2.2.1.2." + row_counter; // 2 столбец

            arr[2] = "1.3.6.1.2.1.2.2.1.8." + row_counter; // 4 столбец
            arr[3] = "1.3.6.1.2.1.2.2.1.5." + row_counter; // 5 столбец
            arr[4] = "1.3.6.1.2.1.2.2.1.3." + row_counter; // 6 столбец

            if (ri_counter + 1 <= status.interface_list.Length)
            {
                survey[1] = new Survey(cl.Ip, arr, Save, GetError);
                Save(survey[1].snmpSurvey());
            }
        }

        void Save(Form1.snmp_result res)
        {
            // Первый раз опрашивает устройство и записывает его таблицу интерфейсов для исключения в дальнейшем пустых опросов

            if (res.vb != null)
            {
                if (cl.Name == "Localhost")
                {
                    if (res.vb[4].Value != null && res.vb[4].Value.ToString() == "6")
                    {
                        status.interface_list[ri_counter] = row_counter;

                        for (int i = 0; i < 5; i++) { if_table[ri_counter, i] = res.vb[i].Oid.ToString(); }

                        status.interface_table[ri_counter, 0] = res.vb[0].Value.ToString();
                        status.interface_table[ri_counter, 1] = res.vb[1].Value.ToString();
                        status.interface_table[ri_counter, 2] = res.vb[2].Value.ToString() == "1" ? "Связь есть" : "Отключен";
                        status.interface_table[ri_counter, 3] = (int.Parse(res.vb[3].Value.ToString()) / 1000000).ToString();
                        status.interface_table[ri_counter, 4] = "Ethernet";

                        ri_counter++;
                    }
                    else
                        Console.WriteLine(row_counter + " is empty");
                }
                else if (cl.Name == "БРИ-CM")
                {
                    if (res.vb[4].Value != null && res.vb[4].Value.ToString() == "6")
                    {
                        status.interface_list[ri_counter] = row_counter;

                        for (int i = 0; i < 5; i++) { if_table[ri_counter, i] = res.vb[i].Oid.ToString(); }

                        status.interface_table[ri_counter, 0] = res.vb[0].Value.ToString();
                        status.interface_table[ri_counter, 1] = res.vb[1].Value.ToString();
                        status.interface_table[ri_counter, 2] = res.vb[2].Value.ToString() == "1" ? "Связь есть" : "Отключен";
                        status.interface_table[ri_counter, 3] = (int.Parse(res.vb[3].Value.ToString()) / 1000000).ToString();
                        status.interface_table[ri_counter, 4] = "Ethernet";

                        ri_counter++;
                    }
                    else
                        Console.WriteLine(row_counter + " is empty");
                }
                else if (cl.Name == "БКМ")
                {
                    if (res.vb[4].Value != null && res.vb[4].Value.ToString() == "6")
                    {
                        status.interface_list[ri_counter] = row_counter;

                        for (int i = 0; i < 5; i++) { if_table[ri_counter, i] = res.vb[i].Oid.ToString(); }

                        status.interface_table[ri_counter, 0] = res.vb[0].Value.ToString();
                        status.interface_table[ri_counter, 1] = res.vb[1].Value.ToString();
                        status.interface_table[ri_counter, 2] = res.vb[2].Value.ToString() == "1" ? "Связь есть" : "Отключен";
                        status.interface_table[ri_counter, 3] = (int.Parse(res.vb[3].Value.ToString()) / 1000000).ToString();
                        status.interface_table[ri_counter, 4] = "Ethernet";

                        ri_counter++;
                    }
                    else
                        Console.WriteLine(row_counter + " is empty");
                }
                else
                {
                    if (res.vb[4].Value != null && res.vb[4].Value.ToString() == "6")
                    {
                        status.interface_list[ri_counter] = row_counter;

                        for (int i = 0; i < 5; i++) { if_table[ri_counter, i] = res.vb[i].Oid.ToString(); }

                        status.interface_table[ri_counter, 0] = res.vb[0].Value.ToString();
                        status.interface_table[ri_counter, 1] = res.vb[1].Value.ToString();
                        status.interface_table[ri_counter, 2] = res.vb[2].Value.ToString() == "1" ? "Связь есть" : "Отключен";
                        status.interface_table[ri_counter, 3] = (int.Parse(res.vb[3].Value.ToString()) / 1000000).ToString();
                        status.interface_table[ri_counter, 4] = "Ethernet";

                        //if_table[ri_counter, ];

                        ri_counter++;

                        /*if (ri_counter == 30)
                        {

                        }*/
                    }
                    else
                        Console.WriteLine(row_counter + " is empty");
                }

                int ifs = int.Parse(status.standart[4]);

                if (ri_counter < ifs && row_counter < ifs + 30)
                    NextRow();
                else
                {
                    status.interface_count = ri_counter;

                    if (cl.Temperature!= null)
                    {
                        if (!timer.Enabled)
                            timer.Start();
                    }
                    else if (!timer.Enabled)
                        timer.Start();
                }
            }
            else
                RestartConnection();
        } //---------------------------------------------------------------------------



        void GetTable(Form1.snmp_result res)
        {

        }

        void RewriteTable()
        {

        }

        void RewriteTable(Form1.snmp_result res)
        {
            if(res.vb.Length > 5)
                for (int i = 0; i < res.vb.Length / 5; i++)
                {
                    status.interface_table[i, 0] = res.vb[i * 5].Value.ToString();
                    status.interface_table[i, 1] = res.vb[i * 5 + 1].Value.ToString();
                    status.interface_table[i, 2] = res.vb[i * 5 + 2].Value.ToString() == "1" ? "Связь есть" : "Отключен";
                    status.interface_table[i, 3] = (int.Parse(res.vb[i * 5 + 3].Value.ToString()) / 1000000).ToString();
                    status.interface_table[i, 4] = "Ethernet";

                    if(i >= 25)
                    {
                        int j = i * 5;
                        string n = cl.Name;
                    }
                }
            else
            {
                status.interface_table[row_counter, 0] = res.vb[0].Value.ToString();
                status.interface_table[row_counter, 1] = res.vb[1].Value.ToString();
                status.interface_table[row_counter, 2] = res.vb[2].Value.ToString() == "1" ? "Связь есть" : "Отключен";
                status.interface_table[row_counter, 3] = (int.Parse(res.vb[3].Value.ToString()) / 1000000).ToString();
                status.interface_table[row_counter, 4] = "Ethernet";
            }
        }



        void GetNext()
        {
            step++;

            switch (step)
            {
                case 0:
                    // standart
                    break;
                case 1:
                    if(is_first)
                    {
                        //interface_table
                    }
                    else
                    {

                    }
                    break;
                case 2:
                    if (cl.SysTime != null)
                        GetSysTime(survey[2].snmpSurvey());
                    break;
                case 3:
                    if (cl.Temperature != null && cl.Temperature.Length != 0)
                        GetTemperatures(survey[3].snmpSurvey());
                    break;
                case 4:
                    //if (cl.IfName != null && cl.Temperature.Length != 0) // ------------------
                        //GetIfNames(survey[4].snmpSurvey());
                    break;
                case 5:
                    step = -1;

                    if (cl.Addition != null && cl.Addition.Length != 0)
                        GetAdditional(survey[5].snmpSurvey());
                    break;
            }

            UpdateInfo();

            if (step == -1 && !timer.Enabled)
                timer.Start();
            else
                GetNext();
        }

        #region Survey [2]
        void GetSysTime(Form1.snmp_result res)
        {
            if (res.vb != null)
            {
                string time = "0";

                try { time = res.vb[0].Value.ToString(); }
                catch { Console.WriteLine("time = " + time); }

                if (res.vb[0].Value.Type == SnmpVariableType.TimeTicks)
                    time = Decrypt_Time(time);

                long convertedTime = Convert.ToInt64(time); //сконвертированное в long время из string

                time = DateTimeOffset.FromUnixTimeSeconds(convertedTime).ToString().Substring(0, 19);

                if(status.SysTime != null)
                    if (status.SysTime.Substring(0, status.SysTime.IndexOf(' ')) != time.Substring(0, status.SysTime.IndexOf(' ')))
                        log.Write(cl.Name, "Значение переменной было изменено: [system time]=" + time);

                status.SysTime = time;
            }

            //GetNext();
        }

        private string Decrypt_Time(string value)
        {
            string result, days = "", hours = "", minutes = "", seconds = "", milliseconds = "";

            value = value.Substring(0, value.Length - 2);

            for (int i = 0, flag = 0; i < value.Length; i++)
            {
                if (value[i] == 'd' || value[i] == 'h' || value[i] == 'm' || value[i] == 's' || value[i] == ' ')
                {
                    if (value[i] != ' ')
                        flag++;
                }
                else
                    switch (flag)
                    {
                        case 0:
                            days += value[i];
                            break;
                        case 1:
                            hours += value[i];
                            break;
                        case 2:
                            minutes += value[i];
                            break;
                        case 3:
                            seconds += value[i];
                            break;
                        case 4:
                            milliseconds += value[i];
                            break;
                    }
            }

            long i_d = Convert.ToInt64(days);
            long i_h = Convert.ToInt64(hours);
            long i_m = Convert.ToInt64(minutes);
            long i_s = Convert.ToInt64(seconds);
            long ims = Convert.ToInt64(milliseconds);

            ims += i_s * 1000;
            ims += i_m * 60000;
            ims += i_h * 3600000;
            ims += i_d * 86400000;

            ims /= 10;

            result = ims.ToString();

            return result;
        }
        #endregion

        void GetTemperatures(Form1.snmp_result res)
        {
            if(res.vb != null)
            {

            }

            //GetNext();
        }

        void GetIfNames(Form1.snmp_result res)
        {
            if (res.vb != null)
            {

            }

            survey[4] = new Survey(cl.Ip, cl.IfName, GetIfNames, GetError);

            //GetNext();
        }

        void GetAdditional(Form1.snmp_result res)
        {
            if (res.vb != null)
            {
                
            }

            //GetNext();
        }

        void GetError(string msg)
        {

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



        //public void ChangeStat(Form1.snmp_result trap)
        //{
        // сканируем все oid'ы trap'а на соответствие и возможно выдаём нештатное состояние
        //} // метод применяемый при поимке snmp trap



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
