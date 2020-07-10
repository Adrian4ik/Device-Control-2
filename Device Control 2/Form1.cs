using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Diagnostics.Contracts;
using System.Drawing;
using System.Linq;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Device_Control_2
{
    public partial class Form1 : Form
    {
        int commlength, miblength, datatype, datalength, datastart;
        int uptime = 0;
        string output;
        SNMP conn = new SNMP();
        byte[] response = new byte[1024];



        string[,] lolkekcheburek = new string[63, 6];

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)// , string[] argv
        {
            dataGridView1.Rows.Add(64);

            lolkekcheburek[0, 0] = "48";
            lolkekcheburek[1, 0] = "Module: 5 Port: 5 - 10/100 Mbit TX";
            lolkekcheburek[2, 0] = "LAPTOP RSS1";
            lolkekcheburek[3, 0] = "Связь есть";
            lolkekcheburek[4, 0] = "100";
            lolkekcheburek[5, 0] = "Ethernet";

            Fill_Grid();


/*          |// // // // // // // // // // // // // //|
            |/ // // // // // // // // // // // // // |
            | // // // // // // // // // // // // // /|
            |// // // // // // // // // // // // // //|
*/

            commlength = Convert.ToInt16(response[6]);
            miblength = Convert.ToInt16(response[23 + commlength]);

            datatype = Convert.ToInt16(response[24 + commlength + miblength]);
            datalength = Convert.ToInt16(response[25 + commlength + miblength]);

            datastart = 26 + commlength + miblength;
            output = Encoding.ASCII.GetString(response, datastart, datalength);

            label11.Text = "sysName - Datatype: " + datatype + ", Value: " + output;

            response = conn.get("get", argv[0], argv[1], "1.3.6.1.2.1.1.6.0");
            if(response[0] == 0xff)
            {
                label12.Text = "No response from " + argv[0];
                return;
            }


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
