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
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Device_Control_2
{
    public partial class Form1 : Form
    {
        string[] comm = new string[8];
        // все виды комьюнити: public, private, ...

        string[] client = new string[1024];
        // список клиентов (не более 1024 клиентов)

        string[,] mibs = new string[1024, 1024];
        // mib'ы клиентов (не более 1024 mib'ов на клиента)
        // mibs[клиент, mib]
        // все mib диапазона 0-23 - стандартные, которые относятся ко всем устройствам
        // локальные mib устройств прописаны в диапазоне 24-1024

        string[,] lolkekcheburek = new string[63, 6];

        int[,] output_i = new int[1024, 1024];
        string[,] output_s = new string[1024, 1024];

        public Form1()
        {
            InitializeComponent();
        }

        void FillConstants()
        {
            int ifIndex = 0;

            comm[0] = "public";
            comm[1] = "private";

            client[0] = "127.0.0.1";
            client[1] = "10.1.2.252"; // БКМ
            client[2] = "10.1.2.254"; // БРИ

            for (int i = 0; i < client.Count(); i++)
            {
                mibs[i, 0] = "1.3.6.1.2.1.1.1.0"; // sysDescr
                mibs[i, 1] = "1.3.6.1.2.1.1.3.0"; // sysUptime
                mibs[i, 2] = "1.3.6.1.2.1.1.4.0"; // sysContact
                mibs[i, 3] = "1.3.6.1.2.1.1.5.0"; // sysName
                mibs[i, 4] = "1.3.6.1.2.1.1.6.0"; // sysLocation
                mibs[i, 5] = "1.3.6.1.2.1.1.7.0"; // sysService
                mibs[i, 6] = "1.3.6.1.2.1.2.1.0"; // ifNumber
                //mibs[i, 7] = "1.3.6.1.2.1.2.2.1.1." + ifIndex; // sysLocation
            }

            mibs[1, 24] = "1.2.643.2.92.1.1.30.0";          // systime oid
            mibs[1, 25] = "1.2.643.2.92.1.1.11.1.9.1";      // abonent ifname
            mibs[1, 26] = "1.2.643.2.92.2.5.1.0";           // temperature
            mibs[1, 27] = "1.2.643.2.92.2.5.2.0";           // max temperature
            mibs[1, 28] = "1.2.643.2.92.2.5.3.0";           // min temperature
            mibs[1, 29] = "1.2.643.2.92.1.3.1.3.1.0";       // fan 1
            mibs[1, 30] = "1.2.643.2.92.1.3.1.3.2.0";       // fan 1 speed
            mibs[1, 31] = "1.2.643.2.92.1.3.1.3.3.0";       // fan 2
            mibs[1, 32] = "1.2.643.2.92.1.3.1.3.4.0";       // fan 2 speed
            mibs[1, 33] = "1.2.643.2.92.1.3.1.3.5.0";       // fan 3
            mibs[1, 34] = "1.2.643.2.92.1.3.1.3.6.0";       // fan 3 speed

            mibs[2, 24] = "1.3.6.1.4.1.248.14.2.5.1.0";     // hmTemperature
            mibs[2, 25] = "1.3.6.1.4.1.248.14.2.5.2.0";     // hmTempUprLimit
            mibs[2, 26] = "1.3.6.1.4.1.248.14.2.5.3.0";     // hmTempLwrLimit
            mibs[2, 27] = "1.3.6.1.4.1.248.14.1.2.1.3.1.1"; // hmPSState:1
            mibs[2, 28] = "1.3.6.1.4.1.248.14.1.2.1.3.1.2"; // hmPSState:2
            mibs[2, 29] = "1.3.6.1.4.1.248.14.1.3.1.3.1.1"; // hmFanState:1
            mibs[2, 30] = "1.3.6.1.4.1.248.14.1.1.30.0";    // hmSystemTime
            mibs[2, 31] = "1.3.6.1.4.1.248.14.1.1.11.1.9.1." + ifIndex; // hmIfaceName
        }

        [Obsolete]
        private void Form1_Load(object sender, EventArgs e)// , string[] argv
        {
            dataGridView1.Rows.Add(64);

            lolkekcheburek[0, 0] = "48";
            lolkekcheburek[1, 0] = "Module: 5 Port: 5 - 10/100 Mbit TX";
            lolkekcheburek[2, 0] = "LAPTOP RSS1";
            lolkekcheburek[3, 0] = "Связь есть";
            lolkekcheburek[4, 0] = "100";
            lolkekcheburek[5, 0] = "Ethernet";

            FillConstants();

            //Survey();

            Fill_main();

            Fill_Grid();

            pictureBox1.Image = Properties.Resources.red24;
        }

        private void Check_TXT()
        {
            string[] al;

            /*
            if (!File.Exists("Clients.txt"))
            { // если файл со списком клиентов не существует, то...
                FileStream f = File.Create("Clients.txt"); // создаём его
                f.Close();
                File.WriteAllLines("Clients.txt", StandartClientList); // и заполняем стандартным списком
            }
            */
            al = File.ReadAllLines("Clients.txt"); // читаем список клиентов
        }

        [Obsolete]
        void Survey()
        {
            #region Comment
            /*          |// // // // // // // // // // // // // //|
                        |/ // // // // // // // // // // // // // |
                        | // // // // // // // // // // // // // /|
                        |// // // // // // // // // // // // // //|
            */

            /*commlength = Convert.ToInt16(response[6]);
            miblength = Convert.ToInt16(response[23 + commlength]);

            datatype = Convert.ToInt16(response[24 + commlength + miblength]);
            datalength = Convert.ToInt16(response[25 + commlength + miblength]);

            datastart = 26 + commlength + miblength;
            output = Encoding.ASCII.GetString(response, datastart, datalength);

            label11.Text = "sysName - Datatype: " + datatype + ", Value: " + output;

            response = conn.get("get", "127.0.0.1", "public", "1.3.6.1.2.1.1.6.0");
            if(response[0] == 0xff)
            {
                label12.Text = "No response from Loopback";
                return;
            }*/
            #endregion

            if (NetworkInterface.GetIsNetworkAvailable())
            {
                Console.WriteLine("Device SNMP information:");

                output_s[1, 0] = snmp_request_str(client[1], comm[0], mibs[1, 0]);
                //Console.WriteLine("  sysDescr: {0}", output_s[1, 0]);

                output_i[1, 1] = snmp_request_int(client[1], comm[0], mibs[1, 1]);
                //Console.WriteLine("  sysUptime: {0}", output_s[1, 1]);

                output_s[1, 3] = snmp_request_str(client[1], comm[0], mibs[1, 3]);
                //Console.WriteLine("  sysName: {0}", output_s[1, 3]);

                output_s[1, 4] = snmp_request_str(client[1], comm[0], mibs[1, 4]);
                //Console.WriteLine("  sysLocation: {0}", output_s[1, 4]);

                //output_i[1, 3] = snmp_request_int(client[1], comm[0], mibs[1, 3]);
                //Console.WriteLine("  systime oid: {0}", output_i[1, 3]);

                output_i[1, 24] = snmp_request_int(client[1], comm[0], mibs[1, 24]);
                //Console.WriteLine("  systime oid: {0}", output_s[1, 24]);

                //output_i[1, 25] = snmp_request_int(client[1], comm[0], mibs[1, 25]);
                //Console.WriteLine("  temperature: {0}", output_i[1, 25]);

                //output_i[1, 26] = snmp_request_int(client[1], comm[0], mibs[1, 26]);
                //Console.WriteLine("  fan 1: {0}", output_i[1, 26]);

                //output_i[1, 27] = snmp_request_int(client[1], comm[0], mibs[1, 27]);
                //Console.WriteLine("  fan 1 speed: {0}", output_i[1, 27]);
            }
            else
                Console.WriteLine("Network is unavailable, check connection and restart program.");

            Console.Read();
        }

        [Obsolete]
        private static string snmp_request_str(string host, string comm, string mib)
        {
            int commlength, miblength, datatype, datalength, datastart;
            string output;
            SNMP conn = new SNMP();
            byte[] response = new byte[1024];

            // Send sysName SNMP request
            response = conn.get("get", host, comm, mib);
            if (response[0] == 0xff)
            {
                Console.WriteLine("No response from {0}", host);
                //return;
            }

            // If response, get the community name and MIB lengths
            commlength = Convert.ToInt16(response[6]);
            miblength = Convert.ToInt16(response[23 + commlength]);

            // Extract the MIB data from the SNMP response
            datatype = Convert.ToInt16(response[24 + commlength + miblength]);
            datalength = Convert.ToInt16(response[25 + commlength + miblength]);
            datastart = 26 + commlength + miblength;

            //output = BitConverter.ToString(response, datastart, datalength);
            output = Encoding.ASCII.GetString(response, datastart, datalength);

            //output = BitConverter.ToInt32(response, datastart);

            return output;
        }

        [Obsolete]
        private static int snmp_request_int(string host, string comm, string mib)
        {
            int commlength, miblength, datatype, datalength, datastart;
            int output = 0;
            SNMP conn = new SNMP();
            byte[] response = new byte[1024];

            // Send a SysUptime SNMP request
            response = conn.get("get", host, comm, mib);
            if (response[0] == 0xff)
            {
                Console.WriteLine("No response from {0}", host);
                //return;
            }

            // Get the community and MIB lengths of the response
            commlength = Convert.ToInt16(response[6]);
            miblength = Convert.ToInt16(response[23 + commlength]);

            // Extract the MIB data from the SNMp response
            datatype = Convert.ToInt16(response[24 + commlength + miblength]);
            datalength = Convert.ToInt16(response[25 + commlength + miblength]);
            datastart = 26 + commlength + miblength;

            // The sysUptime value may by a multi-byte integer
            // Each byte read must be shifted to the higher byte order
            while (datalength > 0)
            {
                output = (output << 8) + response[datastart++];
                datalength--;
            }

            return output;
        }

        private void Fill_main()
        {
            label11.Text = output_s[1, 0];
            label12.Text = output_i[1, 1].ToString();
            label13.Text = output_s[1, 3];
            label14.Text = output_s[1, 4];
            label15.Text = output_i[1, 24].ToString();
        }

        private void Fill_Grid()
        {
            for (int i = 0; i < 64; i++)
            {
                dataGridView1[0, i].Value = lolkekcheburek[0, 0];
                dataGridView1[1, i].Value = lolkekcheburek[1, 0];
                dataGridView1[2, i].Value = lolkekcheburek[2, 0];
                dataGridView1[3, i].Value = lolkekcheburek[3, 0];
                dataGridView1[4, i].Value = lolkekcheburek[4, 0];
                dataGridView1[5, i].Value = lolkekcheburek[5, 0];
            }
        }

        private void Form1_Resize(object sender, EventArgs e)
        {

        }
    }

    class SNMP
    {
        public SNMP()
        {

        }

        [Obsolete]
        public byte[] get(string request, string host, string community, string mibstring)
        {
            byte[] packet = new byte[1024];
            byte[] mib = new byte[1024];
            int snmplen;
            int comlen = community.Length;
            string[] mibvals = mibstring.Split('.');
            int miblen = mibvals.Length;
            int cnt = 0, temp, i;
            int orgmiblen = miblen;
            int pos = 0;

            // Convert the string MIB into a byte array of integer values
            // Unfortunately, values over 128 require multiple bytes
            // which also increases the MIB length
            for (i = 0; i < orgmiblen; i++)
            {
                temp = Convert.ToInt16(mibvals[i]);
                if (temp > 127)
                {
                    mib[cnt] = Convert.ToByte(128 + (temp / 128));
                    mib[cnt + 1] = Convert.ToByte(temp - ((temp / 128) * 128));
                    cnt += 2;
                    miblen++;
                }
                else
                {
                    mib[cnt] = Convert.ToByte(temp);
                    cnt++;
                }
            }
            snmplen = 29 + comlen + miblen - 1;  //Length of entire SNMP packet

            //The SNMP sequence start
            packet[pos++] = 0x30; //Sequence start
            packet[pos++] = Convert.ToByte(snmplen - 2);  //sequence size

            //SNMP version
            packet[pos++] = 0x02; //Integer type
            packet[pos++] = 0x01; //length
            packet[pos++] = 0x00; //SNMP version 1

            //Community name
            packet[pos++] = 0x04; // String type
            packet[pos++] = Convert.ToByte(comlen); //length
                                                    //Convert community name to byte array
            byte[] data = Encoding.ASCII.GetBytes(community);
            for (i = 0; i < data.Length; i++)
            {
                packet[pos++] = data[i];
            }

            //Add GetRequest or GetNextRequest value
            if (request == "get")
                packet[pos++] = 0xA0;
            else
                packet[pos++] = 0xA1;

            packet[pos++] = Convert.ToByte(20 + miblen - 1); //Size of total MIB

            //Request ID
            packet[pos++] = 0x02; //Integer type
            packet[pos++] = 0x04; //length
            packet[pos++] = 0x00; //SNMP request ID
            packet[pos++] = 0x00;
            packet[pos++] = 0x00;
            packet[pos++] = 0x01;

            //Error status
            packet[pos++] = 0x02; //Integer type
            packet[pos++] = 0x01; //length
            packet[pos++] = 0x00; //SNMP error status

            //Error index
            packet[pos++] = 0x02; //Integer type
            packet[pos++] = 0x01; //length
            packet[pos++] = 0x00; //SNMP error index

            //Start of variable bindings
            packet[pos++] = 0x30; //Start of variable bindings sequence

            packet[pos++] = Convert.ToByte(6 + miblen - 1); // Size of variable binding

            packet[pos++] = 0x30; //Start of first variable bindings sequence
            packet[pos++] = Convert.ToByte(6 + miblen - 1 - 2); // size
            packet[pos++] = 0x06; //Object type
            packet[pos++] = Convert.ToByte(miblen - 1); //length

            //Start of MIB
            packet[pos++] = 0x2b;
            //Place MIB array in packet
            for (i = 2; i < miblen; i++)
                packet[pos++] = Convert.ToByte(mib[i]);
            packet[pos++] = 0x05; //Null object value
            packet[pos++] = 0x00; //Null

            //Send packet to destination
            Socket sock = new Socket(AddressFamily.InterNetwork, SocketType.Dgram,
                             ProtocolType.Udp);
            sock.SetSocketOption(SocketOptionLevel.Socket,
                            SocketOptionName.ReceiveTimeout, 5000);
            IPHostEntry ihe = Dns.Resolve(host);
            IPEndPoint iep = new IPEndPoint(ihe.AddressList[0], 161);
            EndPoint ep = (EndPoint)iep;
            sock.SendTo(packet, snmplen, SocketFlags.None, iep);

            //Receive response from packet
            try
            {
                int recv = sock.ReceiveFrom(packet, ref ep);
            }
            catch (SocketException)
            {
                packet[0] = 0xff;
            }
            return packet;
        }

        public string getnextMIB(byte[] mibin)
        {
            string output = "1.3";
            int commlength = mibin[6];
            int mibstart = 6 + commlength + 17; //find the start of the mib section
                                                //The MIB length is the length defined in the SNMP packet
                                                // minus 1 to remove the ending .0, which is not used
            int miblength = mibin[mibstart] - 1;
            mibstart += 2; //skip over the length and 0x2b values
            int mibvalue;

            for (int i = mibstart; i < mibstart + miblength; i++)
            {
                mibvalue = Convert.ToInt16(mibin[i]);
                if (mibvalue > 128)
                {
                    mibvalue = (mibvalue / 128) * 128 + Convert.ToInt16(mibin[i + 1]);
                    //ERROR here, it should be mibvalue = (mibvalue-128)*128 + Convert.ToInt16(mibin[i+1]);
                    //for mib values greater than 128, the math is not adding up correctly   

                    i++;
                }
                output += "." + mibvalue;
            }
            return output;
        }
    }
}
