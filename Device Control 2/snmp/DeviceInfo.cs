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

        int step = -1,
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

            public string[] additional; // survey 5
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
            status.additional = new string[0];
            status.interface_table = new string[0, 0];

            notification.id = cl.id;
            notification.type = new bool[5];

            //if (cl.Addition != null)
                //notification.add_type = new bool[cl.Addition.Length];

            for (int i = 0; i < 5; i++) { notification.type[i] = false; }

            localResult = status_callback;
            localNote = notification_callback;

            FillSurveyArray();

            //if(cl.Addition != null)
                //FillNotificationConstants();
            
            TryPing();
        }



        /// <summary>
        /// Заполняет массив Survey каждого клиента
        /// </summary>
        void FillSurveyArray()
        {
            int table_max_rows = 30;

            survey[0] = new Survey(cl.Ip, GetStandart, GetError);

            status.standart = new string[6];

            string[,] table = new string[table_max_rows, 6];

            for (int i = 0; i < table_max_rows; i++)
            {
                table[i, 0] = "1.3.6.1.2.1.2.2.1.1." + (i + 1); // 1 столбец
                table[i, 1] = "1.3.6.1.2.1.2.2.1.2." + (i + 1); // 2 столбец

                table[i, 2] = "1.3.6.1.2.1.2.2.1.8." + (i + 1); // 4 столбец
                table[i, 3] = "1.3.6.1.2.1.2.2.1.5." + (i + 1); // 5 столбец
                table[i, 4] = "1.3.6.1.2.1.2.2.1.3." + (i + 1); // 6 столбец
            }

            survey[1] = new Survey(cl.Ip, table, GetTable, GetError);

            status.interface_table = new string[table_max_rows, 6];

            if (cl.SysTime != null)
                survey[2] = new Survey(cl.Ip, cl.SysTime, GetSysTime, GetError);

            if (cl.Temperature != null)
            {
                string[] temp = new string[cl.Temperature.Length / 3];

                for (int i = 0; i < temp.Length; i++) { temp[i] = cl.Temperature[i, 0]; }

                survey[3] = new Survey(cl.Ip, temp, GetTemperatures, GetError);

                status.temperatures = new string[cl.Temperature.Length / 3];
            }

            if (cl.IfName != null)
            {
                string[] ifn = new string[table_max_rows];

                for (int i = 0; i < table_max_rows; i++) { ifn[i] = cl.IfName + i; }

                survey[4] = new Survey(cl.Ip, ifn, GetIfNames, GetError);

                status.ifnames = new string[table_max_rows];
            }

            if (cl.Addition != null)
            {
                string[] add = new string[cl.Addition.Length / 6];

                for (int i = 0; i < add.Length; i++) { add[i] = cl.Addition[i, 0]; }

                survey[5] = new Survey(cl.Ip, add, GetAdditional, GetError);

                status.additional = new string[cl.Addition.Length / 6];
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


        void FillNotificationConstants()
        {
            for (int i = 0; i < Form1.notifications.Length; i++)
            {
                int n_id = i % 10;

                switch (n_id)
                {
                    case 0:
                        //notifications[i].Text = "Нештатное состояние системы питания устройства " + cl[(i / 10) + 0].Name;
                        Form1.notifications[(cl.id * 10) + 4].Text = "Прервана связь с устройством " + cl.Name;
                        Form1.notifications[(cl.id * 10) + 4].Criticality = 2;
                        break;
                    case 1:
                        Form1.notifications[(cl.id * 10) + 4].Text = "Нештатное состояние системы питания устройства " + cl.Name;
                        Form1.notifications[(cl.id * 10) + 4].Criticality = 2;
                        break;
                    case 2:
                        Form1.notifications[(cl.id * 10) + 4].Text = "Нештатное значение температуры устройства " + cl.Name;
                        Form1.notifications[(cl.id * 10) + 4].Criticality = 2;
                        break;
                    case 3:
                        Form1.notifications[(cl.id * 10) + 4].Text = "Нештатное состояние вентилятора устройства " + cl.Name;
                        Form1.notifications[(cl.id * 10) + 4].Criticality = 2;
                        break;
                }
            }
        }



        void TryPing()
        {
            if (timer.Enabled)
                timer.Stop();

            if (cl.Connect)
            {
                try { ping.SendAsync(cl.Ip, 3000, waiter); }
                catch
                {
                    //to_survey = false;

                    if (!Form1.notifications[cl.id * 10].State)
                    {
                        Form1.notifications[cl.id * 10].Time = GetTime();
                        Form1.notifications[cl.id * 10].State = true;
                    }

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

                Form1.notifications[cl.id * 10].Time = "";
                Form1.notifications[cl.id * 10].State = false;

                notification.type[1] = false;

                status.icmp_conn = 1;
            }
            else
            {
                icmp_connection[conn_state_counter] = false;

                status.icmp_conn = 3;
                status.snmp_conn = 2;

                if (!Form1.notifications[cl.id * 10].State)
                {
                    Form1.notifications[cl.id * 10].Time = GetTime();
                    Form1.notifications[cl.id * 10].State = true;
                }

                log.WriteEvent(Form1.notifications[cl.id * 10].Text);

                notification.type[1] = true;
                PostAsyncNotification(notification);
            }

            //UpdateInfo();
            PostAsyncResult(status);

            notification.type[2] = true;

            if (e.Reply.Status == IPStatus.Success)
                GetConnection(survey[6].snmpSurvey());
            else if (!timer.Enabled)
                timer.Start();
        }



        void GetStandart(Form1.snmp_result res) //---------------------------------------------------------------------------
        {
            snmp_connection[conn_state_counter] = true;

            //notification.type[2] = false;

            UpdateInfo();
            PostAsyncResult(status);

            //status.standart = new string[5];

            status.snmp_conn = 0;

            string[] names = { "sysDescr", "sysUpTime", "sysName", "sysLocation", "ifNumber" };

            for (int i = 0; i < 5; i++)
            {
                if (status.standart[i] == null)
                    log.Write(cl.Name + " / " + cl.Ip, "Значение переменной: [" + names[i] + "]=" + res.vb[i].Value.ToString());
                else if (status.standart[i] != res.vb[i].Value.ToString())
                    log.Write(cl.Name + " / " + cl.Ip, "Значение переменной было изменено: [" + names[i] + "]=" + res.vb[i].Value.ToString());

                status.standart[i] = res.vb[i].Value.ToString();
            }

            status.interface_list = new int[int.Parse(status.standart[4])];

            if (row_counter == 0)
            {
                if_table = new string[int.Parse(status.standart[4]), 5];
                status.interface_table = new string[int.Parse(status.standart[4]), 5];

                //GetNext();
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

                if (cl.Temperature != null)
                {
                    if (!timer.Enabled)
                        timer.Start();
                }
                else if (!timer.Enabled)
                    timer.Start();
            }
        } //---------------------------------------------------------------------------

        void BunchSave(Form1.snmp_result res)
        {
            bool rewrite_if_count = status.interface_count == 0 ? true : false;

            for (int i = 0, k = 0; i < res.vb.Length / 5; i++)
            {
                bool to_up = false;

                string[] val = new string[5];

                for (int j = 0; j < 5; j++)
                {
                    val[j] = res.vb[(i * 5) + j].Value.ToString();
                }

                if (val != null && val[4] == "6")
                {
                    if (status.interface_table[k, 0] == "" && status.interface_table[k, 0] != null)
                    {
                        /*for (int j = 0; j < 5; j++)
                        {
                            log.Write(cl.Name + " / " + cl.Ip, "Значение переменной: [" + GetIfType(j) + ":" + i.ToString() + "]=" + val);

                            status.interface_table[k, j] = val[j];
                        }*/

                        status.interface_table[k, 0] = val[0];
                        status.interface_table[k, 1] = val[1];
                        status.interface_table[k, 2] = val[2];
                        status.interface_table[k, 3] = val[3];
                        status.interface_table[k, 4] = val[4];

                        string[] desc = new string[5];

                        desc[0] = "Значение переменной: [ifIndex:" + (i + 1).ToString() + "]=" + val[0];
                        desc[1] = "Значение переменной: [ifDescr:" + (i + 1).ToString() + "]=" + val[1];
                        desc[2] = "Значение переменной: [ifOperStatus:" + (i + 1).ToString() + "]=" + val[2];
                        desc[3] = "Значение переменной: [ifSpeed:" + (i + 1).ToString() + "]=" + val[3];
                        desc[4] = "Значение переменной: [ifType:" + (i + 1).ToString() + "]=" + val[4];

                        log.Write(cl.Name + " / " + cl.Ip, desc);
                    }
                    else
                    {
                        /*for (int j = 1; j < 4; j++)
                        {
                            log.Write(cl.Name + " / " + cl.Ip, "Значение переменной было изменено: [" + GetIfType(j) + ":" + i.ToString() + "]=" + val[1]);
                            status.interface_table[k, j] = val[j];
                        }*/

                        if (status.interface_table[k, 1] != val[1])
                        {
                            log.Write(cl.Name + " / " + cl.Ip, "Значение переменной было изменено: [ifDescr:" + (i + 1).ToString() + "]=" + val[1]);
                            status.interface_table[k, 1] = val[1];
                        }
                        
                        if (status.interface_table[k, 2] != val[2])
                        {
                            log.Write(cl.Name + " / " + cl.Ip, "Значение переменной было изменено: [ifOperStatus:" + (i + 1).ToString() + "]=" + val[2]);
                            status.interface_table[k, 2] = val[2];
                        }
                        
                        if (status.interface_table[k, 3] != val[3])
                        {
                            log.Write(cl.Name + " / " + cl.Ip, "Значение переменной было изменено: [ifSpeed:" + (i + 1).ToString() + "]=" + val[3]);
                            status.interface_table[k, 3] = val[3];
                        }

                        //for (int j = 1; j < 4; j++)
                        //    status.interface_table[k, j] = val[j];
                    }

                    k++;

                    if (rewrite_if_count)
                        status.interface_count++;
                }

                /*for (int j = 0; j < 5; j++)
                {
                    //if (status.interface_table[i, j] == null)
                    //    log.Write(cl.Name + " / " + cl.Ip, "Значение переменной: [" + GetNameFromTable(i) + "]=" + res.vb[(i * 5) + j].Value.ToString());
                    //else if (status.interface_table[i, j] != res.vb[(i * 5) + j].Value.ToString())
                    //    log.Write(cl.Name + " / " + cl.Ip, "Значение переменной было изменено: [" + GetNameFromTable(i) + "]=" + res.vb[(i * 5) + j].Value.ToString());

                    string eth = res.vb[(i * 5) + 4].Value.ToString();

                    if (val != null && val[j] != "Null" && status.interface_table[k, j] != val[j] && eth == "6")
                    {
                        to_up = true;

                        if (j != 5 || j != 0)
                        {
                            if (status.interface_table[k, j] == "" || status.interface_table[k, j] == null)
                                log.Write(cl.Name + " / " + cl.Ip, "Значение переменной: [" + GetIfType(j) + ":" + i.ToString() + "]=" + val[j]);
                            else
                            {
                                switch (j)
                                {
                                    case 1:
                                        if ((status.interface_table[k, j] == "Связь есть" && val[j] != "1") || (status.interface_table[k, j] == "Отключен" && val[j] == "1"))
                                            log.Write(cl.Name + " / " + cl.Ip, "Значение переменной было изменено: [ifDescr:" + i.ToString() + "]=" + val[j]);
                                        break;
                                    case 2:
                                        if (status.interface_table[k, j] != val[j])
                                            log.Write(cl.Name + " / " + cl.Ip, "Значение переменной было изменено: [ifOperStatus:" + i.ToString() + "]=" + val[j]);
                                        break;
                                    case 3:
                                        if (status.interface_table[k, j] != val[j] + "000000")
                                            log.Write(cl.Name + " / " + cl.Ip, "Значение переменной было изменено: [ifSpeed:" + i.ToString() + "]=" + val[j]);
                                        break;
                                }
                            }
                        }

                        status.interface_table[k, j] = val;
                    }
                    else
                    {

                    }

                    //if ((i * 5) + j >= 90)
                    //{
                    //    Console.WriteLine(k + " " + ((i * 5) + j));
                    //}
                }
                */

                /*if (to_up)
                {
                    //RewriteRow(k);

                    k++;

                    if(rewrite_if_count)
                        status.interface_count++;
                }*/
            }
        }

        string GetIfType(int id)
        {
            if (id == 0)
                return "ifIndex";
            if (id == 1)
                return "ifDescr";
            if (id == 2)
                return "ifOperStatus";
            if (id == 3)
                return "ifSpeed";
            else
                return "ifType";
        }

        void RewriteRow(int row)
        {
            status.interface_table[row, 2] = status.interface_table[row, 2] == "1" ? "Связь есть" : "Отключен";
            status.interface_table[row, 3] = (int.Parse(status.interface_table[row, 3]) / 1000000).ToString();
            status.interface_table[row, 4] = "Ethernet";
        }

        string GetNameFromTable(int id)
        {
            if (id == 0)
                return "if_id";
            if (id == 1)
                return "if_name";
            if (id == 2)
                return "if_state";
            if (id == 3)
                return "if_speed";
            else
                return "if_type";
        }



        void GetTable(Form1.snmp_result res)
        {

        }

        void RewriteTable()
        {

        }

        void RewriteTable(Form1.snmp_result res)
        {
            if (res.vb.Length > 5)
                for (int i = 0; i < res.vb.Length / 5; i++)
                {
                    status.interface_table[i, 0] = res.vb[i * 5].Value.ToString();
                    status.interface_table[i, 1] = res.vb[i * 5 + 1].Value.ToString();
                    status.interface_table[i, 2] = res.vb[i * 5 + 2].Value.ToString() == "1" ? "Связь есть" : "Отключен";
                    status.interface_table[i, 3] = (int.Parse(res.vb[i * 5 + 3].Value.ToString()) / 1000000).ToString();
                    status.interface_table[i, 4] = "Ethernet";

                    if (i >= 25)
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



        void NextStep()
        {
            step++;

            if (survey[step] != null)
            {
                Form1.snmp_result res = survey[step].snmpSurvey();

                if (res.vb != null)
                    GetNextSurvey(res);
            }
            else if (step == 5)
                step -= 6;

            UpdateInfo();

            if (step == -1) // && !timer.Enabled
            {
                //timer.Start();

                UpdateInfo();
                PostAsyncResult(status);
            }
            else
                NextStep();
        }

        void GetNextSurvey(Form1.snmp_result res)
        {
            if (step == 0) { GetStandart(res); }
            else if (step == 1)
            {
                BunchSave(survey[1].snmpSurvey());

                string[] ifn = new string[status.interface_count];

                for (int i = 1; i <= status.interface_count; i++) { ifn[i - 1] = cl.IfName + i; }

                survey[4] = new Survey(cl.Ip, ifn, GetIfNames, GetError);

                //AnalyzeTable();
                /*if (is_first)
                {
                    //for(int i = 0; i < )
                    //survey[1] = new Survey();

                    BunchSave(survey[1].snmpSurvey());

                    //interface_table
                }
                else
                {

                }*/
            }
            else if (step == 2) { GetSysTime(res); }
            else if (step == 3) { GetTemperatures(res); }
            else if (step == 4) { GetIfNames(res); }
            else if (step == 5)
            {
                step -= 6;

                GetAdditional(res);
            }
            /*else if (step > 5 && step < 15)
            {
                step -= 16;

                GetConnection(survey[6].snmpSurvey());
            }*/
        }



        #region Survey [2]
        void GetSysTime(Form1.snmp_result res)
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
                    log.Write(cl.Name + " / " + cl.Ip, "Значение переменной было изменено: [system time]=" + time);

            status.SysTime = time;

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
            for (int i = 0; i < 3; i++)
            {
                if (status.temperatures[i] == null)
                    log.Write(cl.Name + " / " + cl.Ip, "Значение переменной: [" + cl.Temperature[i, 1] + "]=" + res.vb[i].Value.ToString());
                else if (status.temperatures[i] != res.vb[i].Value.ToString())
                    log.Write(cl.Name + " / " + cl.Ip, "Значение переменной было изменено: [" + cl.Temperature[i, 1] + "]=" + res.vb[i].Value.ToString());

                status.temperatures[i] = res.vb[i].Value.ToString();
            }

            if(int.Parse(res.vb[0].Value.ToString()) > int.Parse(res.vb[1].Value.ToString()) || int.Parse(res.vb[2].Value.ToString()) > int.Parse(res.vb[0].Value.ToString()))
            {
                if (!Form1.notifications[(cl.id * 10) + 2].State)
                {
                    Form1.notifications[(cl.id * 10) + 2].Time = GetTime();
                    Form1.notifications[(cl.id * 10) + 2].State = true;
                }

                //notification.type[1] = true;
                PostAsyncNotification(notification);

                log.WriteEvent(Form1.notifications[(cl.id * 10) + 2].Text);
            }
            else
            {
                Form1.notifications[(cl.id * 10) + 2].Time = "";
                Form1.notifications[(cl.id * 10) + 2].State = false;

                //notification.type[1] = true;
                PostAsyncNotification(notification);
            }

            //GetNext();
        }

        void GetIfNames(Form1.snmp_result res)
        {
            if(cl.Name == "БКМ")
                for (int i = 0; i < res.vb.Length; i++)
                {
                    if (res.vb[i].Value != null && res.vb[i].Value.ToString() != "Null")
                        status.ifnames[i] = res.vb[i].Value.ToString();
                }
            //survey[4] = new Survey(cl.Ip, cl.IfName, GetIfNames, GetError);

            //GetNext();
        }

        void GetAdditional(Form1.snmp_result res)
        {
            for (int i = 0; i < cl.Addition.Length / 6; i++)
            {
                if (status.additional[i] == null)
                    log.Write(cl.Name + " / " + cl.Ip, "Значение переменной: [" + cl.Addition[i, 1] + "]=" + res.vb[i].Value.ToString());
                else if (status.additional[i] != res.vb[i].Value.ToString())
                    log.Write(cl.Name + " / " + cl.Ip, "Значение переменной было изменено: [" + cl.Addition[i, 1] + "]=" + res.vb[i].Value.ToString());

                status.additional[i] = res.vb[i].Value.ToString();
            }

            //GetNext();
        }

        void AnalyzeTable()
        {
            for(int i = 0; i < status.interface_table.Length / 6; i++)
            {
                for(int j = 0; j < 5; j++)
                {
                    if (status.interface_table[i, j] != "")
                        log.Write("Значение переменной было изменено []=");
                }
            }
        }

        void GetError(string msg)
        {

        }



        void GetConnection(Form1.snmp_result res)
        {
            notification.type[1] = false;

            conn_state_counter++;

            if (res.vb == null)
                RestartConnection();
            else
            {
                notification.type[2] = false;
                
                if (conn_state_counter == 1)
                    NextStep();
                else if (conn_state_counter == 10)
                    AnalyzeConnection(); //----------------------------------------------
            }

            //UpdateInfo();
            PostAsyncResult(status);

            if (conn_state_counter == 10)
            {
                conn_state_counter = 0;

                /*if (timer.Enabled)
                    timer.Stop();*/
            }
            
            if (!timer.Enabled)
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
                    status.icmp_conn = 2;
            }
            else
                status.icmp_conn = 1;

            bad_connections = 0;

            foreach (bool conn_state in snmp_connection)
            {
                if (!conn_state)
                    bad_connections++;
            }

            if (bad_connections == 9)
                status.snmp_conn = 0;
            else
                status.snmp_conn = 1;

            /*if (bad_connections > 0)
            {
                if (bad_connections == 10)
                    status.snmp_conn = 2;
                else
                    status.snmp_conn = 1;
            }
            else
                status.snmp_conn = 0;*/
        }



        void UpdateInfo()
        {            
            status.info_updated_time = "Последний раз обновлено: " + GetTime();
        }

        string GetTime()
        {
            string time = (DateTime.Now.Hour < 10) ? "0" + DateTime.Now.Hour + ":" : DateTime.Now.Hour + ":";
            time += (DateTime.Now.Minute < 10) ? "0" + DateTime.Now.Minute : DateTime.Now.Minute.ToString();

            //time += (DateTime.Now.Second < 10) ? "0" + DateTime.Now.Second : DateTime.Now.Second.ToString();

            return time;
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
