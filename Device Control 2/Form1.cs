using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Net.Sockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using SnmpSharpNet;

namespace Device_Control_2
{
    public partial class Form1 : Form
    {
        #region Переменные
        int client_f = 2;

        string[] stdclients = { "folder names: hm_mach4002, octopus, bkm, fs1" };

        string[] std_oids = { "1.3.6.1.2.1.1.1.0", "1.3.6.1.2.1.1.2.0", "1.3.6.1.2.1.1.3.0", "1.3.6.1.2.1.1.4.0", "1.3.6.1.2.1.1.5.0", "1.3.6.1.2.1.1.6.0", "1.3.6.1.2.1.2.1.0", "1.3.6.1.2.1.2.2.1."};
                            // sysDescr          // sysObjectID       // sysUpTime         // sysContact        // sysName           // sysLocation       // ifNumber          // ifTable

        string[] std_config = { "Имя: ", "ip: ", "Autoconnect: 1" };

        string[] std_hm_optlist = { "Важные", "",
                                    "Температура текущая, °С (hmTemperature): 1.3.6.1.4.1.248.14.2.5.1.0",
                                    "Температура максимально\r\nдопустимая, °С (hmTempUprLimit): 1.3.6.1.4.1.248.14.2.5.2.0",
                                    "Температура минимально\r\nдопустимая, °С(hmTempLwrLimit): 1.3.6.1.4.1.248.14.2.5.3.0",
                                    "Питание #1 (hmPSState1): 1.3.6.1.4.1.248.14.1.2.1.3.1.1",
                                    "Питание #2 (hmPSState2): 1.3.6.1.4.1.248.14.1.2.1.3.1.2",
                                    "Вентилятор (hmFanState1): 1.3.6.1.4.1.248.14.1.3.1.3.1.1",
                                    "", "Описание устройства", "",
                                    "Системное время (hmSystemTime): 1.3.6.1.4.1.248.14.1.1.30.0",
                                    "", "Интерфейсы", "",
                                    "hmIfaceName: 1.3.6.1.4.1.248.14.1.1.11.1.9.1.ifIndex" };

        string[] std_optlist = { "Важные", "",
                                 "Температура текущая, °С (temperature): 1.2.643.2.92.2.5.1.0",
                                 "Температура максимально\r\nдопустимая, °С (max temperature): 1.2.643.2.92.2.5.2.0",
                                 "Температура минимально\r\nдопустимая, °С(min temperature): 1.2.643.2.92.2.5.3.0",
                                 "Вентилятор #1 (fan 1 speed): 1.2.643.2.92.1.3.1.3.2.0",
                                 "Вентилятор #2 (fan 2 speed): 1.2.643.2.92.1.3.1.3.4.0",
                                 "Вентилятор #3 (fan 3 speed): 1.2.643.2.92.1.3.1.3.6.0",
                                 "", "Описание устройства", "",
                                 "Системное время (systime): 1.2.643.2.92.1.1.30.0",
                                 "", "Интерфейсы", "",
                                 "hmIfaceName: 1.2.643.2.92.1.1.11.1.9.1.ifIndex" };

        string[,] std_mib = new string[1024, 2]; // каждый клиент может занимать не более 10 позиций обычных мибов, определение клиента идёт по десяткам либо сотням (сотни нужны как доп. мибы)
        // т.е. мибы клиента 1: 11, 12, 13, ... // 101, 102, 103... // 121, 122, 123, ... (10 - 19 и 100 - 199); мибы клиента 2: 21, 22, 23, ... // 201, 202, 203, 204 (20 - 29 и 200 - 299) и т.д.
        string[,] mib = new string[1024, 2];
        // mib'ы клиентов (не более 1024 mib'ов на клиента)
        // mibs[клиент, mib]
        // все mib диапазона 0-23 - стандартные, которые относятся ко всем устройствам
        // локальные mib устройств прописаны в диапазоне 24-1024

        string[,] interfaces = new string[1024, 6];

        AutoResetEvent waiter = new AutoResetEvent(false);

        Pdu list0 = new Pdu(PduType.Get);
        Pdu list1 = new Pdu(PduType.Get);
        Pdu list2 = new Pdu(PduType.Get);
        Pdu list3 = new Pdu(PduType.Get);
        Pdu list4 = new Pdu(PduType.Get);
        Pdu list5 = new Pdu(PduType.Get);
        Pdu list6 = new Pdu(PduType.Get);
        Pdu list7 = new Pdu(PduType.Get);
        Pdu list8 = new Pdu(PduType.Get);
        Pdu list9 = new Pdu(PduType.Get);
        Pdu list10 = new Pdu(PduType.Get);
        Pdu list11 = new Pdu(PduType.Get);
        Pdu list12 = new Pdu(PduType.Get);
        Pdu list13 = new Pdu(PduType.Get);
        Pdu list14 = new Pdu(PduType.Get);
        Pdu list15 = new Pdu(PduType.Get);
        Pdu list16 = new Pdu(PduType.Get);
        Pdu list17 = new Pdu(PduType.Get);
        Pdu list18 = new Pdu(PduType.Get);
        Pdu list19 = new Pdu(PduType.Get);

        struct Client
        {
            public string Name { get; set; }
            public string Ip { get; set; }
        }

        Client[] std_cl = new Client[3];
        Client[] cl;
        // список клиентов (не более 1024 клиентов)

        struct Device
        {
            public string FolderName { get; set; }
            public string[] Config { get; set; }
            public string[] OptList { get; set; }
        }

        Notification notify = new Notification();
        #endregion

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)// , string[] argv
        {
            Preprocessing();

            Survey();
        }

        private void Preprocessing()
        {
            dataGridView1.Rows.Add(128);
            Change_SNMP_Status(4);
            Change_Ping_Status(4);

            FillConstants();

            cl = std_cl;
            Check_clients();

            WriteLog(false, "Программа запущена");
        }
        // Доделать
        private void FillConstants()
        {
            //int ifIndex = 0;

            std_cl[0].Ip = "127.0.0.1";
            std_cl[0].Name = "Loopback";
            std_cl[1].Ip = "10.1.2.252";
            std_cl[1].Name = "БКМ";
            std_cl[2].Ip = "10.1.2.254";
            std_cl[2].Name = "БРИ-1";



            std_mib[10, 0] = "1.2.643.2.92.1.1.30.0";
            std_mib[10, 1] = "systime";
            std_mib[11, 0] = "1.2.643.2.92.2.5.1.0";
            std_mib[11, 1] = "temperature";
            std_mib[12, 0] = "1.2.643.2.92.2.5.2.0";
            std_mib[12, 1] = "max temperature";
            std_mib[13, 0] = "1.2.643.2.92.2.5.3.0";
            std_mib[13, 1] = "min temperature";
            std_mib[14, 0] = "1.2.643.2.92.1.3.1.3.1.0";
            std_mib[14, 1] = "fan 1";
            std_mib[15, 0] = "1.2.643.2.92.1.3.1.3.2.0";
            std_mib[15, 1] = "fan 1 speed";
            std_mib[16, 0] = "1.2.643.2.92.1.3.1.3.3.0";
            std_mib[16, 1] = "fan 2";
            std_mib[17, 0] = "1.2.643.2.92.1.3.1.3.4.0";
            std_mib[17, 1] = "fan 2 speed";
            std_mib[18, 0] = "1.2.643.2.92.1.3.1.3.5.0";
            std_mib[18, 1] = "fan 3";
            std_mib[19, 0] = "1.2.643.2.92.1.3.1.3.6.0";
            std_mib[19, 1] = "fan 3 speed";

            std_mib[100, 0] = "1.2.643.2.92.1.1.11.1.9.1.";        // abonent ifname

            std_mib[20, 0] = "1.3.6.1.4.1.248.14.1.1.30.0";
            std_mib[20, 1] = "hmSystemTime";
            std_mib[21, 0] = "1.3.6.1.4.1.248.14.2.5.1.0";
            std_mib[21, 1] = "hmTemperature";
            std_mib[22, 0] = "1.3.6.1.4.1.248.14.2.5.2.0";
            std_mib[22, 1] = "hmTempUprLimit";
            std_mib[23, 0] = "1.3.6.1.4.1.248.14.2.5.3.0";
            std_mib[23, 1] = "hmTempLwrLimit";
            std_mib[24, 0] = "1.3.6.1.4.1.248.14.1.2.1.3.1.1";
            std_mib[24, 1] = "hmPSState:1";
            std_mib[25, 0] = "1.3.6.1.4.1.248.14.1.2.1.3.1.2";
            std_mib[25, 1] = "hmPSState:2";
            std_mib[26, 0] = "1.3.6.1.4.1.248.14.1.3.1.3.1.1";
            std_mib[26, 1] = "hmFanState:1";

            std_mib[200, 0] = "1.3.6.1.4.1.248.14.1.1.11.1.9.1."; // hmIfaceName

            mib = std_mib;
        }
        // Доделать
        private void Check_clients()
        {
            if (!File.Exists("config.xml"))
            { // если файл со списком клиентов не существует, то...
                FileStream f = File.Create("config.xml"); // создаём его
                f.Close(); // и закрываем для предотвращения различных ошибок
                File.WriteAllLines("config.xml", stdclients); // и заполняем файл стандартным списком
            }

            if (!Directory.Exists("devices")) // если папка со списком клиентов не существует
            {
                Directory.CreateDirectory("devices"); // то создаём её

                CreateDevice("localhost", std_config, std_oids);
                CreateDevice("hm_mach4002", std_config, std_hm_optlist);
                CreateDevice("octopus", std_config, std_hm_optlist);
                CreateDevice("bkm", std_config, std_optlist);
                CreateDevice("fs1", std_config, std_hm_optlist);
            }

            string[] al = File.ReadAllLines("config.xml"); // переписываем список клиентов

            /*if (al[0].Substring(0, 14) == "folder names: ")
                al[0] = al[0].Substring(14, al[0].Length - 14);

            int flag = 0;

            for (int s = 0; s < al.Count(); s++)
            {
                for (int c = 0; c < al[s].Length; c++)
                {
                    if ("" + al[s][c] + al[s][c + 1] == ", ") // Правило разбиения строки на компоненты (имя1, имя2, имя3)
                        flag++;
                    //else
                        //g_lists[group, flag][cl] += al[s][c];
                }
            }*/

            // if()
                cl = std_cl;
            // else

            for (int i = 0; i < 7; i++)
                list0.VbList.Add(std_oids[i]);
            
            for (int i = 10; i < 20; i++)
                if (mib[i, 0] != null)
                    list1.VbList.Add(mib[i, 0]);

            //for (int i = 100; i < 200; i++)
            //    if (mibs[i] != null)
            //        list1.VbList.Add(mibs[i]);

            for (int i = 20; i < 30; i++)
                if (mib[i, 0] != null)
                    list2.VbList.Add(mib[i, 0]);

            for (int i = 30; i < 40; i++)
                if (mib[i, 0] != null)
                    list3.VbList.Add(mib[i, 0]);

            for (int i = 40; i < 50; i++)
                if (mib[i, 0] != null)
                    list4.VbList.Add(mib[i, 0]);

            for (int i = 50; i < 60; i++)
                if (mib[i, 0] != null)
                    list5.VbList.Add(mib[i, 0]);

            for (int i = 60; i < 70; i++)
                if (mib[i, 0] != null)
                    list6.VbList.Add(mib[i, 0]);

            for (int i = 70; i < 80; i++)
                if (mib[i, 0] != null)
                    list7.VbList.Add(mib[i, 0]);

            for (int i = 80; i < 90; i++)
                if (mib[i, 0] != null)
                    list8.VbList.Add(mib[i, 0]);

            for (int i = 90; i < 100; i++)
                if (mib[i, 0] != null)
                    list9.VbList.Add(mib[i, 0]);
        }

        private Device CheckDevice(string folder_name)
        {
            Device device = new Device();

            if (!Directory.Exists("devices\\" + folder_name))
                Directory.CreateDirectory("devices\\" + folder_name);
            device.FolderName = folder_name;

            if (!File.Exists("devices\\" + folder_name + "\\config.xml"))
            {
                FileStream f = File.Create("devices\\" + folder_name + "\\config.xml");
                f.Close();
                File.WriteAllLines("devices\\" + folder_name + "\\config.xml", std_config);
            }


            if (!File.Exists("devices\\" + folder_name + "\\optlist.xml"))
            {
                FileStream f = File.Create("devices\\" + folder_name + "\\optlist.xml");
                f.Close();
                File.WriteAllLines("devices\\" + folder_name + "\\optlist.xml", std_optlist);
            }

            return device;
        }

        private void CreateDevice(string folder_name, string[] std_config, string[] std_optlist)
        {
            Device device = new Device();

            if (!Directory.Exists("devices\\" + folder_name))
                Directory.CreateDirectory("devices\\" + folder_name);
            device.FolderName = folder_name;

            if (!File.Exists("devices\\" + folder_name + "\\config.xml"))
            {
                FileStream f = File.Create("devices\\" + folder_name + "\\config.xml");
                f.Close();
                File.WriteAllLines("devices\\" + folder_name + "\\config.xml", std_config);
            }


            if (!File.Exists("devices\\" + folder_name + "\\optlist.xml"))
            {
                FileStream f = File.Create("devices\\" + folder_name + "\\optlist.xml");
                f.Close();
                File.WriteAllLines("devices\\" + folder_name + "\\optlist.xml", std_optlist);
            }
        }

        private void CheckLog()
        {
            string date = DateTime.Now.Year.ToString();
            date += (DateTime.Now.Month < 10) ? "0" + DateTime.Now.Month : DateTime.Now.Month.ToString();
            date += (DateTime.Now.Day < 10) ? "0" + DateTime.Now.Day : DateTime.Now.Day.ToString();

            if (!Directory.Exists("log"))
            {
                Directory.CreateDirectory("log");

                FileStream f = File.Create("log\\" + date + ".txt");
                f.Close();
            }
            else if(!File.Exists("log\\" + date + ".txt"))
            {
                FileStream f = File.Create("log\\" + date + ".txt");
                f.Close();
            }
        }
        
        private void CheckEventLog()
        {
            string date = DateTime.Now.Year.ToString();
            date += (DateTime.Now.Month < 10) ? "0" + DateTime.Now.Month : DateTime.Now.Month.ToString();

            if (!Directory.Exists("event log"))
            {
                Directory.CreateDirectory("event log");

                FileStream f = File.Create("event log\\" + date + ".txt");
                f.Close();
            }
            else if (!File.Exists("event log\\" + date + ".txt"))
            {
                FileStream f = File.Create("event log\\" + date + ".txt");
                f.Close();
            }
        }

        private void WriteLog(bool WithClient, string text)
        {
            CheckLog();

            string date = DateTime.Now.Year.ToString();
            date += (DateTime.Now.Month < 10) ? "0" + DateTime.Now.Month : DateTime.Now.Month.ToString();
            date += (DateTime.Now.Day < 10) ? "0" + DateTime.Now.Day : DateTime.Now.Day.ToString();

            if(WithClient)
                File.AppendAllText("log\\" + date + ".txt", "\n" + "[" + DateTime.Now + "] <" + cl[client_f].Name + " / " + cl[client_f].Ip + "> " + text);
            else
                File.AppendAllText("log\\" + date + ".txt", "\n" + "[" + DateTime.Now + "] " + text);
        }
        private void WriteLog(bool WithClient, string[] text)
        {
            CheckLog();

            string date = DateTime.Now.Year.ToString();
            date += (DateTime.Now.Month < 10) ? "0" + DateTime.Now.Month : DateTime.Now.Month.ToString();
            date += (DateTime.Now.Day < 10) ? "0" + DateTime.Now.Day : DateTime.Now.Day.ToString();

            if (WithClient)
                for(int i = 0; i < text.Count(); i++)
                    File.AppendAllText("log\\" + date + ".txt", "\n" + "[" + DateTime.Now + "] <" + cl[client_f].Name + " / " + cl[client_f].Ip + "> " + text[i]);
            else
                for (int i = 0; i < text.Count(); i++)
                    File.AppendAllText("log\\" + date + ".txt", "\n" + "[" + DateTime.Now + "] " + text[i]);
        }
        
        private void WriteEvent(bool WithClient, string text)
        {
            CheckEventLog();

            string date = DateTime.Now.Year.ToString();
            date += (DateTime.Now.Month < 10) ? "0" + DateTime.Now.Month : DateTime.Now.Month.ToString();

            if (WithClient)
                File.AppendAllText("event log\\" + date + ".txt", "\n" + "[" + DateTime.Now + "] <" + cl[client_f].Name + " / " + cl[client_f].Ip + "> " + text);
            else
                File.AppendAllText("event log\\" + date + ".txt", "\n" + "[" + DateTime.Now + "] " + text);
        }

        private void Survey()
        {
            label1.Text = cl[client_f].Name;

            if (NetworkInterface.GetIsNetworkAvailable())
            {
                Ping ping = new Ping();
                ping.PingCompleted += new PingCompletedEventHandler(Received_ping_reply);
                ping.SendAsync(cl[client_f].Ip, 3000, waiter);

                Change_Ping_Status(0);
            }
            else
            {
                Console.WriteLine("Network is unavailable, check connection and restart program.");

                WriteLog(false, "Соединение отсутствует");
                //WriteEvent(false, "Соединение отсутствует");

                Change_SNMP_Status(4);
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
                Change_Ping_Status(1);
                Change_SNMP_Status(0);

                WriteLog(true, "Связь присутствует");

                Fill_main();
            }
            else
            {
                Change_Ping_Status(3);

                WriteLog(true, "Связь отсутствует");
            }

            SurveyUpdate();
        }

        private void Change_Ping_Status(int stat)
        {
            switch (stat)
            {
                case 0:
                    pictureBox2.Image = Properties.Resources.ajax_loader;
                    break;
                case 1:
                    pictureBox2.Image = Properties.Resources.green24;
                    break;
                case 2:
                    pictureBox2.Image = Properties.Resources.orange24;
                    break;
                case 3:
                    pictureBox2.Image = Properties.Resources.red24;
                    break;
                case 4:
                    pictureBox2.Image = Properties.Resources.gray24;
                    break;
            }
        }

        private void Fill_main()
        {
            SnmpV1Packet result = SurveyList(0, cl[client_f].Ip, list0);

            CheckStdOIDChanges(label11.Text, 0, result.Pdu.VbList[0].Value.ToString());
            label11.Text = result.Pdu.VbList[0].Value.ToString();
            CheckStdOIDChanges(label12.Text, 2, result.Pdu.VbList[2].Value.ToString());
            label12.Text = result.Pdu.VbList[2].Value.ToString();
            CheckStdOIDChanges(label13.Text, 4, result.Pdu.VbList[4].Value.ToString());
            label13.Text = result.Pdu.VbList[4].Value.ToString();
            CheckStdOIDChanges(label14.Text, 5, result.Pdu.VbList[5].Value.ToString());
            label14.Text = result.Pdu.VbList[5].Value.ToString();

            int ifNumber = Convert.ToInt32(result.Pdu.VbList[6].Value.ToString());

            switch (client_f)
            {
                case 1:
                    result = SurveyList(0, cl[client_f].Ip, list1);
                    break;
                case 2:
                    result = SurveyList(0, cl[client_f].Ip, list2);
                    break;
                case 3:
                    result = SurveyList(0, cl[client_f].Ip, list3);
                    break;
                case 4:
                    result = SurveyList(0, cl[client_f].Ip, list4);
                    break;
                case 5:
                    result = SurveyList(0, cl[client_f].Ip, list5);
                    break;
                case 6:
                    result = SurveyList(0, cl[client_f].Ip, list6);
                    break;
                case 7:
                    result = SurveyList(0, cl[client_f].Ip, list7);
                    break;
                case 8:
                    result = SurveyList(0, cl[client_f].Ip, list8);
                    break;
                case 9:
                    result = SurveyList(0, cl[client_f].Ip, list9);
                    break;
            }

            string time = result.Pdu.VbList[0].Value.ToString();
            if (result.Pdu.VbList[0].Type == 48)
                time = Decrypt_Time(time);

            CheckModOIDChanges(label15.Text, 0, time);
            long convertedTime = Convert.ToInt64(time); //сконвертированное в long время из string
            label15.Text = DateTimeOffset.FromUnixTimeSeconds(convertedTime).ToString().Substring(0, 19);

            CheckModOIDChanges(label21.Text, 1, result.Pdu.VbList[1].Value.ToString());
            label21.Text = result.Pdu.VbList[1].Value.ToString();
            CheckModOIDChanges(label22.Text, 2, result.Pdu.VbList[2].Value.ToString());
            label22.Text = result.Pdu.VbList[2].Value.ToString();
            CheckModOIDChanges(label23.Text, 3, result.Pdu.VbList[3].Value.ToString());
            label23.Text = result.Pdu.VbList[3].Value.ToString();
            CheckModOIDChanges(label24.Text, 5, result.Pdu.VbList[5].Value.ToString());
            label24.Text = result.Pdu.VbList[5].Value.ToString();

            if(client_f == 1)
            {
                CheckModOIDChanges(label25.Text, 7, result.Pdu.VbList[7].Value.ToString());
                label25.Text = result.Pdu.VbList[7].Value.ToString();
                CheckModOIDChanges(label26.Text, 9, result.Pdu.VbList[9].Value.ToString());
                label26.Text = result.Pdu.VbList[9].Value.ToString();
            }

            Change_SNMP_Status(1);

            int curt = Convert.ToInt32(result.Pdu.VbList[1].Value.ToString());
            int maxt = Convert.ToInt32(result.Pdu.VbList[2].Value.ToString());
            int mint = Convert.ToInt32(result.Pdu.VbList[3].Value.ToString());

            //if (curt >= maxt || curt <= mint)
                //notify.Show();

            Survey_grid(ifNumber);
        }

        private string Decrypt_Time(string value)
        {
            string result, days = "", hours = "", minutes = "", seconds = "", milliseconds = "";

            value = value.Substring(0, value.Length - 2);

            for(int i = 0, flag = 0; i < value.Length; i++)
            {
                if(value[i] == 'd' || value[i] == 'h' || value[i] == 'm' || value[i] == 's' || value[i] == ' ')
                {
                    if(value[i] != ' ')
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

        private void CheckStdOIDChanges(string original, int oid_id, string oid_result)
        {
            if(oid_result != original)
                if(original == "" || original == null)
                    switch (oid_id)
                    {
                        case 0:
                            WriteLog(true, "Значение переменной: [sysDescr]=" + oid_result);
                            break;
                        case 2:
                            WriteLog(true, "Значение переменной: [sysUpTime]=" + oid_result);
                            break;
                        case 4:
                            WriteLog(true, "Значение переменной: [sysName]=" + oid_result);
                            break;
                        case 5:
                            WriteLog(true, "Значение переменной: [sysLocation]=" + oid_result);
                            break;
                    }
                else
                    switch (oid_id)
                    {
                        case 0:
                            WriteLog(true, "Значение переменной было изменено: [sysDescr]=" + oid_result);
                            break;
                        case 2:
                            WriteLog(true, "Значение переменной было изменено: [sysUpTime]=" + oid_result);
                            break;
                        case 4:
                            WriteLog(true, "Значение переменной было изменено: [sysName]=" + oid_result);
                            break;
                        case 5:
                            WriteLog(true, "Значение переменной было изменено: [sysLocation]=" + oid_result);
                            break;
                    }
        }

        private void CheckModOIDChanges(string original, int oid_id, string oid_result)
        {
            oid_id = (client_f * 10) + oid_id;
            if (oid_result != original)
                if (original == "" || original == null)
                {
                    string jopa = mib[oid_id, 1];
                    WriteLog(true, "Значение переменной: [" + mib[oid_id, 1] + "]=" + oid_result);
                }
                else
                    WriteLog(true, "Значение переменной было изменено: [" + mib[oid_id, 1] + "]=" + oid_result);
        }

        private void Change_SNMP_Status(int stat)
        {
            switch (stat)
            {
                case 0:
                    pictureBox1.Image = Properties.Resources.ajax_loader;
                    label2.Text = "Соединение";
                    break;
                case 1:
                    pictureBox1.Image = Properties.Resources.green24;
                    label2.Text = "Режим опроса";
                    break;
                case 2:
                    pictureBox1.Image = Properties.Resources.orange24;
                    label2.Text = "Режим опроса";
                    break;
                case 3:
                    pictureBox1.Image = Properties.Resources.red24;
                    label2.Text = "Автономный";
                    break;
                case 4:
                    pictureBox1.Image = Properties.Resources.gray24;
                    label2.Text = "Автономный";
                    break;
                case 5:
                    pictureBox1.Image = Properties.Resources.gray24;
                    label2.Text = "Неактивный";
                    break;
            }
        }
        // Доделать
        private void Survey_grid(int ifNum)
        {
            int fi = 0;

            // Изменить под пропуски ifIndex
            //for (int i = 1; i <= ifNum; i++) // строки

            int i = 1, k = 0;

            while (/*flag == */true) // бред, но работает только в том случае, если оиды из одной подсетки (из разных запрещено делать запросы)
            {
                Pdu list = new Pdu(PduType.Get);
                //Console.Write("port {0}: ", i);
                                
                list.VbList.Add(std_oids[7] + "1." + i); // 1 столбец
                list.VbList.Add(std_oids[7] + "2." + i); // 2 столбец
                list.VbList.Add(std_oids[7] + "3." + i); // 6 столбец
                list.VbList.Add(std_oids[7] + "5." + i); // 5 столбец
                list.VbList.Add(std_oids[7] + "8." + i); // 4 столбец

                SnmpV1Packet result = SurveyList(0, cl[client_f].Ip, list);

                string type = "";

                if (i == 129)
                    type = "";

                string itype = result.Pdu.VbList[2].Value.ToString();

                switch (itype)
                {
                    case "1":
                        type = "Other";
                        break;
                    case "6":
                        type = "Ethernet";
                        break;
                    case "135":
                        type = "l2vlan";
                        break;
                    case "161":
                        type = "ieee8023AdLag";
                        break;
                    case "":
                        type = "";
                        break;
                    case "Null":
                        type = "";
                        break;
                    case null:
                        type = "";
                        break;
                }

                if (result.Pdu.VbList[2].Value.ToString() == "6")
                {
                    fi++;

                    string state = (Convert.ToInt32(result.Pdu.VbList[4].Value.ToString()) == 1) ? "Связь есть" : "Отключен";
                    string state_to_log = "";

                    if (interfaces[k, 3] == "Связь есть")
                        state_to_log = "1";
                    else if (interfaces[k, 3] == "Отключен")
                        state_to_log = "2";

                    interfaces[k, 0] = (k + 1).ToString(); // result.Pdu.VbList[0].Value.ToString();
                    CheckITableChanges(interfaces[k, 1], k, result.Pdu.VbList[1].Value.ToString(), "ifDescr");
                    interfaces[k, 1] = result.Pdu.VbList[1].Value.ToString();

                    CheckITableChanges(state_to_log, k, result.Pdu.VbList[4].Value.ToString(), "ifOperStatus");
                    interfaces[k, 3] = state;
                    CheckITableChanges(Convert.ToInt32(interfaces[k, 4]) * 1000000 + "", k, result.Pdu.VbList[3].Value.ToString(), "ifSpeed");
                    interfaces[k, 4] = Convert.ToInt32(result.Pdu.VbList[3].Value.ToString()) / 1000000 + "";
                    interfaces[k, 5] = type;

                    k++;

                    if (k == ifNum)
                        break;
                }
                else
                    if (result.Pdu.VbList[2].Value.ToString() == "1" || result.Pdu.VbList[2].Value.ToString() == "135" || result.Pdu.VbList[2].Value.ToString() == "161")
                        break;
                
                i++;
            }

            int ilimit = i;

            i = 1;
            k = 0;
            
            while (true)
            {
                Pdu list = new Pdu(PduType.Get);

                if (client_f == 1)
                    list.VbList.Add(mib[100, 0] + i++);  // 3 столбец
                else
                    list.VbList.Add(mib[200, 0] + i++);  // 3 столбец

                SnmpV1Packet result = SurveyList(0, cl[client_f].Ip, list);

                if (result.Pdu.VbList[0].Value.ToString() != "Null")
                {
                    CheckINamesChanges(interfaces[k, 2], k, result.Pdu.VbList[0].Value.ToString());
                    interfaces[k++, 2] = result.Pdu.VbList[0].Value.ToString();
                }

                if (i == ilimit || k == ifNum)
                    break;
            }

            Fill_grid(ifNum);
        }

        private void CheckITableChanges(string original, int ifindex, string oid_result, string oid_name)
        {
            if (oid_result != original)
                if (original == "" || original == null)
                    WriteLog(true, "Значение переменной: [" + oid_name + ":" + ++ifindex + "]=" + oid_result);
                else
                    WriteLog(true, "Значение переменной было изменено: [" + oid_name + ":" + ++ifindex + "]=" + oid_result);
        }

        private void CheckINamesChanges(string original, int oid_id, string oid_result)
        {
            if (oid_result != original)
                if (original == "" || original == null)
                    WriteLog(true, "Значение переменной: [ifname:" + ++oid_id + "]=" + oid_result);
                else
                    WriteLog(true, "Значение переменной было изменено: [ifname:" + ++oid_id + "]=" + oid_result);
        }

        private void Fill_grid(int rows_count)
        {
            //dataGridView1 = new DataGridView();
            //dataGridView1.Rows.Add(rows_count);

            for (int i = 0; i < 64; i++)
                for(int j = 0; j < 6; j++)
                    dataGridView1[j, i].Value = interfaces[i, j];

            for (int i = 0; i < 64; i++)
                if (interfaces[i, 3] == "Отключен")
                    for (int j = 0; j < 6; j++)
                        dataGridView1[j, i].Style.BackColor = Color.FromArgb(223, 223, 223);
        }

        private SnmpV1Packet SurveyList(int community, string ip, Pdu list)
        {
            // SNMP community name
            OctetString comm;

            if (community == 1)
                comm = new OctetString("private");
            else
                comm = new OctetString("public");

            // Define agent parameters class
            AgentParameters param = new AgentParameters(comm);
            // Set SNMP version to 1 (or 2)
            param.Version = SnmpVersion.Ver1;
            // Construct the agent address object
            // IpAddress class is easy to use here because
            //  it will try to resolve constructor parameter if it doesn't
            //  parse to an IP address
            IpAddress agent = new IpAddress(ip);

            // Construct target
            UdpTarget target = new UdpTarget((IPAddress)agent, 161, 2000, 1);

            // Pdu class used for all requests
            Pdu pdu = list;

            // Make SNMP request
            SnmpV1Packet result = (SnmpV1Packet)target.Request(pdu, param);

            // If result is null then agent didn't reply or we couldn't parse the reply.
            if (result != null)
                if (result.Pdu.ErrorStatus != 0)
                    Console.WriteLine("Error in SNMP reply. Error {0} index {1}", result.Pdu.ErrorStatus, result.Pdu.ErrorIndex);
                else
                {
                    target.Close();

                    return result;
                }
            else
                Console.WriteLine("No response received from SNMP agent.");

            target.Close();

            return result;
        }

        private void SurveyUpdate()
        {
            // Тернарная операция: z = (x > y) ? x : y;
            string time = (DateTime.Now.Hour > 10) ? DateTime.Now.Hour + ":" : "0" + DateTime.Now.Hour + ":";
            time += (DateTime.Now.Minute > 10) ? DateTime.Now.Minute.ToString() : "0" + DateTime.Now.Minute;
            label3.Text = "Последний раз обновлено: " + time;

            if(!timer1.Enabled)
                timer1.Start();
        }
        // Сделать
        private void Form1_Resize(object sender, EventArgs e)
        {

        }

        private void timer1_Tick(object sender, EventArgs e)
        {
            Survey();
        }

        private void Form1_FormClosing(object sender, FormClosingEventArgs e)
        {
            WriteLog(false, "Программа завершена");
        }

        private void timer2_Tick(object sender, EventArgs e)
        {

        }
    }
}
