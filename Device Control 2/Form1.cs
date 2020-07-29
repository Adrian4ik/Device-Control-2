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

        /// <summary>
        /// Creates a GIF using .Net GIF encoding and additional animation headers.
        /// </summary>
        public class GifWriter : IDisposable
        {
            #region Fields
            const long SourceGlobalColorInfoPosition = 10,
                SourceImageBlockPosition = 789;

            readonly BinaryWriter _writer;
            bool _firstFrame = true;
            readonly object _syncLock = new object();
            #endregion

            /// <summary>
            /// Creates a new instance of GifWriter.
            /// </summary>
            /// <param name="OutStream">The <see cref="Stream"/> to output the Gif to.</param>
            /// <param name="DefaultFrameDelay">Default Delay between consecutive frames... FrameRate = 1000 / DefaultFrameDelay.</param>
            /// <param name="Repeat">No of times the Gif should repeat... -1 not to repeat, 0 to repeat indefinitely.</param>
            public GifWriter(Stream OutStream, int DefaultFrameDelay = 500, int Repeat = -1)
            {
                if (OutStream == null)
                    throw new ArgumentNullException(nameof(OutStream));

                if (DefaultFrameDelay <= 0)
                    throw new ArgumentOutOfRangeException(nameof(DefaultFrameDelay));

                if (Repeat < -1)
                    throw new ArgumentOutOfRangeException(nameof(Repeat));

                _writer = new BinaryWriter(OutStream);
                this.DefaultFrameDelay = DefaultFrameDelay;
                this.Repeat = Repeat;
            }

            /// <summary>
            /// Creates a new instance of GifWriter.
            /// </summary>
            /// <param name="FileName">The path to the file to output the Gif to.</param>
            /// <param name="DefaultFrameDelay">Default Delay between consecutive frames... FrameRate = 1000 / DefaultFrameDelay.</param>
            /// <param name="Repeat">No of times the Gif should repeat... -1 not to repeat, 0 to repeat indefinitely.</param>
            public GifWriter(string FileName, int DefaultFrameDelay = 500, int Repeat = -1)
                : this(new FileStream(FileName, FileMode.OpenOrCreate, FileAccess.Write, FileShare.Read), DefaultFrameDelay, Repeat) { }

            #region Properties
            /// <summary>
            /// Gets or Sets the Default Width of a Frame. Used when unspecified.
            /// </summary>
            public int DefaultWidth { get; set; }

            /// <summary>
            /// Gets or Sets the Default Height of a Frame. Used when unspecified.
            /// </summary>
            public int DefaultHeight { get; set; }

            /// <summary>
            /// Gets or Sets the Default Delay in Milliseconds.
            /// </summary>
            public int DefaultFrameDelay { get; set; }

            /// <summary>
            /// The Number of Times the Animation must repeat.
            /// -1 indicates no repeat. 0 indicates repeat indefinitely
            /// </summary>
            public int Repeat { get; }
            #endregion

            /// <summary>
            /// Adds a frame to this animation.
            /// </summary>
            /// <param name="Image">The image to add</param>
            /// <param name="Delay">Delay in Milliseconds between this and last frame... 0 = <see cref="DefaultFrameDelay"/></param>
            public void WriteFrame(Image Image, int Delay = 0)
            {
                lock (_syncLock)
                    using (var gifStream = new MemoryStream())
                    {
                        Image.Save(gifStream, ImageFormat.Gif);

                        // Steal the global color table info
                        if (_firstFrame)
                            InitHeader(gifStream, _writer, Image.Width, Image.Height);

                        WriteGraphicControlBlock(gifStream, _writer, Delay == 0 ? DefaultFrameDelay : Delay);
                        WriteImageBlock(gifStream, _writer, !_firstFrame, 0, 0, Image.Width, Image.Height);
                    }

                if (_firstFrame)
                    _firstFrame = false;
            }

            #region Write
            void InitHeader(Stream SourceGif, BinaryWriter Writer, int Width, int Height)
            {
                // File Header
                Writer.Write("GIF".ToCharArray()); // File type
                Writer.Write("89a".ToCharArray()); // File Version

                Writer.Write((short)(DefaultWidth == 0 ? Width : DefaultWidth)); // Initial Logical Width
                Writer.Write((short)(DefaultHeight == 0 ? Height : DefaultHeight)); // Initial Logical Height

                SourceGif.Position = SourceGlobalColorInfoPosition;
                Writer.Write((byte)SourceGif.ReadByte()); // Global Color Table Info
                Writer.Write((byte)0); // Background Color Index
                Writer.Write((byte)0); // Pixel aspect ratio
                WriteColorTable(SourceGif, Writer);

                // App Extension Header for Repeating
                if (Repeat == -1)
                    return;

                Writer.Write(unchecked((short)0xff21)); // Application Extension Block Identifier
                Writer.Write((byte)0x0b); // Application Block Size
                Writer.Write("NETSCAPE2.0".ToCharArray()); // Application Identifier
                Writer.Write((byte)3); // Application block length
                Writer.Write((byte)1);
                Writer.Write((short)Repeat); // Repeat count for images.
                Writer.Write((byte)0); // terminator
            }

            static void WriteColorTable(Stream SourceGif, BinaryWriter Writer)
            {
                SourceGif.Position = 13; // Locating the image color table
                var colorTable = new byte[768];
                SourceGif.Read(colorTable, 0, colorTable.Length);
                Writer.Write(colorTable, 0, colorTable.Length);
            }

            static void WriteGraphicControlBlock(Stream SourceGif, BinaryWriter Writer, int FrameDelay)
            {
                SourceGif.Position = 781; // Locating the source GCE
                var blockhead = new byte[8];
                SourceGif.Read(blockhead, 0, blockhead.Length); // Reading source GCE

                Writer.Write(unchecked((short)0xf921)); // Identifier
                Writer.Write((byte)0x04); // Block Size
                Writer.Write((byte)(blockhead[3] & 0xf7 | 0x08)); // Setting disposal flag
                Writer.Write((short)(FrameDelay / 10)); // Setting frame delay
                Writer.Write(blockhead[6]); // Transparent color index
                Writer.Write((byte)0); // Terminator
            }

            static void WriteImageBlock(Stream SourceGif, BinaryWriter Writer, bool IncludeColorTable, int X, int Y, int Width, int Height)
            {
                SourceGif.Position = SourceImageBlockPosition; // Locating the image block
                var header = new byte[11];
                SourceGif.Read(header, 0, header.Length);
                Writer.Write(header[0]); // Separator
                Writer.Write((short)X); // Position X
                Writer.Write((short)Y); // Position Y
                Writer.Write((short)Width); // Width
                Writer.Write((short)Height); // Height

                if (IncludeColorTable) // If first frame, use global color table - else use local
                {
                    SourceGif.Position = SourceGlobalColorInfoPosition;
                    Writer.Write((byte)(SourceGif.ReadByte() & 0x3f | 0x80)); // Enabling local color table
                    WriteColorTable(SourceGif, Writer);
                }
                else Writer.Write((byte)(header[9] & 0x07 | 0x07)); // Disabling local color table

                Writer.Write(header[10]); // LZW Min Code Size

                // Read/Write image data
                SourceGif.Position = SourceImageBlockPosition + header.Length;

                var dataLength = SourceGif.ReadByte();
                while (dataLength > 0)
                {
                    var imgData = new byte[dataLength];
                    SourceGif.Read(imgData, 0, dataLength);

                    Writer.Write((byte)dataLength);
                    Writer.Write(imgData, 0, dataLength);
                    dataLength = SourceGif.ReadByte();
                }

                Writer.Write((byte)0); // Terminator
            }
            #endregion

            /// <summary>
            /// Frees all resources used by this object.
            /// </summary>
            public void Dispose()
            {
                // Complete File
                _writer.Write((byte)0x3b); // File Trailer

                _writer.BaseStream.Dispose();
                _writer.Dispose();
            }
        }

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
