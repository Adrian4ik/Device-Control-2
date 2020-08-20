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
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using SnmpSharpNet;

namespace Device_Control_2
{
    public partial class Form1 : Form
    {
        #region Переменные
        int client_f = 2;

        string[] std_oids = { "1.3.6.1.2.1.1.1.0", "1.3.6.1.2.1.1.2.0", "1.3.6.1.2.1.1.3.0", "1.3.6.1.2.1.1.4.0", "1.3.6.1.2.1.1.5.0", "1.3.6.1.2.1.1.6.0", "1.3.6.1.2.1.2.1.0", "1.3.6.1.2.1.2.2.1."};
                            // sysDescr          // sysObjectID       // sysUpTime         // sysContact        // sysName           // sysLocation       // ifNumber          // ifTable

        string[] std_mib = new string[1024]; // каждый клиент может занимать не более 10 позиций обычных мибов, определение клиента идёт по десяткам либо сотням (сотни нужны как доп. мибы)
        // т.е. мибы клиента 1: 11, 12, 13, ... // 101, 102, 103... // 121, 122, 123, ... (10 - 19 и 100 - 199); мибы клиента 2: 21, 22, 23, ... // 201, 202, 203, 204 (20 - 29 и 200 - 299) и т.д.
        string[] mib = new string[1024];
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

            Check_clients();
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



            std_mib[10] = "1.2.643.2.92.1.1.30.0";          // systime oid
            std_mib[11] = "1.2.643.2.92.2.5.1.0";           // temperature
            std_mib[12] = "1.2.643.2.92.2.5.2.0";           // max temperature
            std_mib[13] = "1.2.643.2.92.2.5.3.0";           // min temperature
            std_mib[14] = "1.2.643.2.92.1.3.1.3.1.0";       // fan 1
            std_mib[15] = "1.2.643.2.92.1.3.1.3.2.0";       // fan 1 speed
            std_mib[16] = "1.2.643.2.92.1.3.1.3.3.0";       // fan 2
            std_mib[17] = "1.2.643.2.92.1.3.1.3.4.0";       // fan 2 speed
            std_mib[18] = "1.2.643.2.92.1.3.1.3.5.0";       // fan 3
            std_mib[19] = "1.2.643.2.92.1.3.1.3.6.0";       // fan 3 speed

            std_mib[100] = "1.2.643.2.92.1.1.11.1.9.1.";        // abonent ifname

            std_mib[20] = "1.3.6.1.4.1.248.14.2.5.1.0";     // hmTemperature
            std_mib[21] = "1.3.6.1.4.1.248.14.2.5.2.0";     // hmTempUprLimit
            std_mib[22] = "1.3.6.1.4.1.248.14.2.5.3.0";     // hmTempLwrLimit
            std_mib[23] = "1.3.6.1.4.1.248.14.1.2.1.3.1.1"; // hmPSState:1
            std_mib[24] = "1.3.6.1.4.1.248.14.1.2.1.3.1.2"; // hmPSState:2
            std_mib[25] = "1.3.6.1.4.1.248.14.1.3.1.3.1.1"; // hmFanState:1
            std_mib[26] = "1.3.6.1.4.1.248.14.1.1.30.0";    // hmSystemTime

            std_mib[200] = "1.3.6.1.4.1.248.14.1.1.11.1.9.1."; // + ifIndex; hmIfaceName

            mib = std_mib;
        }
        // Доделать
        private void Check_clients()
        {
            //string[] al;

            /*
            if (!File.Exists("Clients.txt"))
            { // если файл со списком клиентов не существует, то...
                FileStream f = File.Create("Clients.txt"); // создаём его
                f.Close();
                File.WriteAllLines("Clients.txt", StandartClientList); // и заполняем стандартным списком
            }
            */

            if(!Directory.Exists("devices"))
                Directory.CreateDirectory("devices");

            if (!Directory.Exists("devices\\localhost"))
            {
                Directory.CreateDirectory("devices\\localhost");
            }

            if(!File.Exists("devices\\localhost\\config.xml"))
            {
                FileStream f = File.Create("devices\\localhost\\config.xml");
                f.Close();
            }


            if (!File.Exists("devices\\localhost\\optlist.xml"))
            {
                FileStream f = File.Create("devices\\localhost\\optlist.xml");
                f.Close();
            }

            //al = File.ReadAllLines("Clients.txt"); // читаем список клиентов

            // if()
                cl = std_cl;
            // else

            for (int i = 0; i < 7; i++)
                list0.VbList.Add(std_oids[i]);
            
            for (int i = 10; i < 20; i++)
                if (mib[i] != null)
                    list1.VbList.Add(mib[i]);

            //for (int i = 100; i < 200; i++)
            //    if (mibs[i] != null)
            //        list1.VbList.Add(mibs[i]);

            for (int i = 20; i < 30; i++)
                if (mib[i] != null)
                    list2.VbList.Add(mib[i]);

            for (int i = 30; i < 40; i++)
                if (mib[i] != null)
                    list3.VbList.Add(mib[i]);

            for (int i = 40; i < 50; i++)
                if (mib[i] != null)
                    list4.VbList.Add(mib[i]);

            for (int i = 50; i < 60; i++)
                if (mib[i] != null)
                    list5.VbList.Add(mib[i]);

            for (int i = 60; i < 70; i++)
                if (mib[i] != null)
                    list6.VbList.Add(mib[i]);

            for (int i = 70; i < 80; i++)
                if (mib[i] != null)
                    list7.VbList.Add(mib[i]);

            for (int i = 80; i < 90; i++)
                if (mib[i] != null)
                    list8.VbList.Add(mib[i]);

            for (int i = 90; i < 100; i++)
                if (mib[i] != null)
                    list9.VbList.Add(mib[i]);
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

                Fill_main();
            }
            else
                Change_Ping_Status(3);

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

        /*private static void Count()
        {
            output_s[1, 0] = snmp_request_str(client[1], comm[0], mibs[1, 0]);

            output_i[1, 1] = snmp_request_int(client[1], comm[0], mibs[1, 1]);

            output_s[1, 3] = snmp_request_str(client[1], comm[0], mibs[1, 3]);

            output_s[1, 4] = snmp_request_str(client[1], comm[0], mibs[1, 4]);

            output_i[1, 24] = snmp_request_int(client[1], comm[0], mibs[1, 24]);

            //output_i[1, 25] = snmp_request_int(client[1], comm[0], mibs[1, 25]);

            output_i[1, 26] = snmp_request_int(client[1], comm[0], mibs[1, 26]);

            output_i[1, 27] = snmp_request_int(client[1], comm[0], mibs[1, 27]);

            output_i[1, 28] = snmp_request_int(client[1], comm[0], mibs[1, 28]);

            output_i[1, 30] = snmp_request_int(client[1], comm[0], mibs[1, 30]);

            CountdownEvent cde = new CountdownEvent(1);
            cde.Reset();
        }

        public class ThresholdReachedEventArgs : EventArgs
        {
            public int Threshold { get; set; }
            public DateTime TimeReached { get; set; }
        }*/

        private void Fill_main()
        {
            SnmpV1Packet result = SurveyList(0, cl[client_f].Ip, list0);

            label11.Text = result.Pdu.VbList[0].Value.ToString();
            label12.Text = result.Pdu.VbList[2].Value.ToString();
            label13.Text = result.Pdu.VbList[4].Value.ToString();
            label14.Text = result.Pdu.VbList[5].Value.ToString();
            int ifNumber = Convert.ToInt32(result.Pdu.VbList[6].Value.ToString());

            switch(client_f)
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

            long convertedTime = Convert.ToInt64(result.Pdu.VbList[0].Value.ToString()); //сконвертированное в long время из string

            label15.Text = DateTimeOffset.FromUnixTimeSeconds(convertedTime).ToString().Substring(0, 19);

            label21.Text = result.Pdu.VbList[1].Value.ToString();
            label22.Text = result.Pdu.VbList[2].Value.ToString();
            label23.Text = result.Pdu.VbList[3].Value.ToString();
            label24.Text = result.Pdu.VbList[5].Value.ToString();
            if(client_f == 1)
            {
                label25.Text = result.Pdu.VbList[7].Value.ToString();
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

            bool flag = true;
            int i = 1, k = 0;

            while (flag == true) // бред, но работает только в том случае, если оиды из одной подсетки (из разных запрещено делать запросы)
            {
                Pdu list = new Pdu(PduType.Get);
                //Console.Write("port {0}: ", i);
                                
                list.VbList.Add(std_oids[7] + "1." + i); // 1 столбец
                list.VbList.Add(std_oids[7] + "2." + i); // 2 столбец
                list.VbList.Add(std_oids[7] + "3." + i); // 6 столбец
                list.VbList.Add(std_oids[7] + "5." + i); // 5 столбец
                list.VbList.Add(std_oids[7] + "8." + i); // 4 столбец
                //if (client_f == 1)
                    //list.VbList.Add(mib[100] + i);       // 3 столбец
                //else
                    //list.VbList.Add(mib[200] + i);       // 3 столбец

                SnmpV1Packet result = SurveyList(0, cl[client_f].Ip, list);

                string type = "";

                if (i == 129)
                    type = "";

                string lol = result.Pdu.VbList[2].Value.ToString();

                switch (lol)
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
                    case null:
                        type = "";
                        break;
                    case "":
                        type = "";
                        break;
                    case "Null":
                        type = "";
                        break;
                }

                if (result.Pdu.VbList[2].Value.ToString() == "6")
                {
                    fi++;

                    string state = (Convert.ToInt32(result.Pdu.VbList[4].Value.ToString()) == 1) ? "Связь есть" : "Отключен";

                    interfaces[k, 0] = (k + 1).ToString(); // result.Pdu.VbList[0].Value.ToString();
                    interfaces[k, 1] = result.Pdu.VbList[1].Value.ToString();
                    //if (result.Pdu.VbList[5].Value.ToString() != "Null")
                    //{
                    //    interfaces[k, 2] = result.Pdu.VbList[5].Value.ToString();
                    //}
                    interfaces[k, 3] = state;
                    interfaces[k, 4] = result.Pdu.VbList[3].Value.ToString();
                    interfaces[k, 5] = type;

                    k++;

                    if (k == ifNum)
                        flag = false;
                }
                else
                    if (result.Pdu.VbList[2].Value.ToString() == "1" || result.Pdu.VbList[2].Value.ToString() == "135" || result.Pdu.VbList[2].Value.ToString() == "161")
                        flag = false;
                
                i++;

                //if(i == fi)
                //{
                //}
            }

            /*int lim;
            if (client_f == 1)
                lim = fi;
            else
                lim = ifNum;

            for (int j = 1; j <= lim; j++)
            {
                Pdu list = new Pdu(PduType.Get);

                if (client_f == 1)
                    list.VbList.Add(mib[100] + i);       // 3 столбец
                else
                    list.VbList.Add(mib[200] + i);       // 3 столбец

                result = SurveyList(0, cl[client_f].Ip, list);

                if(result.Pdu.VbList[0].Value.ToString() != "Null")
                    interfaces[j++, 2] = result.Pdu.VbList[0].Value.ToString();
            }*/

            Fill_grid(ifNum);
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
    }
}
