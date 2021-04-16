using System;
using System.Net;
using System.Net.Sockets;
using System.Windows.Forms;
using SnmpSharpNet;

namespace Device_Control_2.snmp
{
	class Traps
	{
		protected Socket _socket = null; // из тутора
		protected byte[] _inbuffer; // из тутора
		protected IPEndPoint _peerIP; // из тутора

		readonly Timer timer1 = new Timer();
        readonly Action<Form1.snmp_result> localResult;
        readonly Action<string> localError;

		public Traps(Action<Form1.snmp_result> callback, Action<string> error_callback)
		{
			timer1.Interval = 1000;
			timer1.Tick += new EventHandler(Timer1_Tick);

			localResult = callback;
			localError = error_callback;

			Start();
		}

		private void Start() // (Start 1)
		{
			if (!InitializeReceiver())
			{
				// unable to start TRAP receiver
				if (!timer1.Enabled)
					timer1.Start();

				return;
			}
			else if (timer1.Enabled)
				timer1.Stop();
		}

		public bool InitializeReceiver() // (Start 2)
		{
			if (_socket != null)
			{
				StopReceiver();
			}

			try
			{
				// create an IP/UDP socket
				_socket = new Socket(AddressFamily.InterNetwork, SocketType.Dgram, ProtocolType.Udp);

			}
			catch (Exception ex)
			{
				//listBox1.Items.Add("SNMP trap receiver socket initialization failed with error: " + ex.Message);
				// there is no need to close the socket because it was never correctly created
				_socket = null;
			}

			if (_socket == null)
				return false;

			try
			{
				// prepare to "bind" the socket to the local port number
				// binding notifies the operating system that application 
				// wishes to receive data sent to the specified port number

				// prepare EndPoint that will bind the application to all available 
				//IP addresses and port 162 (snmp-trap)
				EndPoint localEP = new IPEndPoint(IPAddress.Any, 162);
				// bind socket
				_socket.Bind(localEP);
			}
			catch (Exception ex)
			{
				//listBox1.Items.Add("SNMP trap receiver initialization failed with error: " + ex.Message);
				_socket.Close();
				_socket = null;
			}

			if (_socket == null)
				return false;

			if (!RegisterReceiveOperation())
				return false;

			return true;
		}

		public void StopReceiver() // (балласт, а также выключатель trap службы)
		{
			if (_socket != null)
			{
				_socket.Close();
				_socket = null;
			}
		}

		public bool RegisterReceiveOperation() // (Start 3)
		{
			if (_socket == null)
				return false;
			// socket has been closed
			try
			{
				_peerIP = new IPEndPoint(IPAddress.Any, 0);
				// receive from anybody
				EndPoint ep = _peerIP;
				_inbuffer = new byte[64 * 1024];
				// nice and big receive buffer
				_socket.BeginReceiveFrom(_inbuffer, 0, 64 * 1024,
					SocketFlags.None, ref ep, new AsyncCallback(ReceiveCallback), _socket);
			}
			catch (Exception ex)
			{
				//listBox1.Items.Add("Registering receive operation failed with message: " + ex.Message);
				_socket.Close();
				_socket = null;
			}
			if (_socket == null)
				return false;
			return true;
		}

		private void ReceiveCallback(IAsyncResult result) // (Программа)
		{
			// get a reference to the socket. This is handy if socket has been closed elsewhere in the class
			Socket sock = (Socket)result.AsyncState;

			_peerIP = new IPEndPoint(IPAddress.Any, 0);

			// variable to store received data length
			int inlen;

			try
			{
				EndPoint ep = _peerIP;
				inlen = sock.EndReceiveFrom(result, ref ep);
				_peerIP = (IPEndPoint)ep;
			}
			catch (Exception ex)
			{
				// only post messages if class socket reference is not null
				// in all other cases, user has terminated the socket
				if (_socket != null)
				{
					PostAsyncResult("Receive operation failed with message: " + ex.Message);
				}
				inlen = -1;
			}
			// if socket has been closed, ignore received data and return
			if (_socket == null)
				return;
			// check that received data is long enough
			if (inlen <= 0)
			{
				// request next packet
				RegisterReceiveOperation();
				return;
			}
			int packetVersion = SnmpPacket.GetProtocolVersion(_inbuffer, inlen);
			if (packetVersion == (int)SnmpVersion.Ver1)
			{
				SnmpV1TrapPacket pkt = new SnmpV1TrapPacket();
				try
				{
					pkt.decode(_inbuffer, inlen);
				}
				catch (Exception ex)
				{
					PostAsyncResult("Error parsing SNMPv1 Trap: " + ex.Message);
					pkt = null;
				}
				if (pkt != null)
				{
                    Form1.snmp_result res = new Form1.snmp_result{ Ip = _peerIP.Address, vb = new Vb[pkt.Pdu.VbList.Count] };

                    int i = 0;

					foreach (Vb vb in pkt.Pdu.VbList)
					{
						res.vb[i++] = vb;
					}

					PostAsyncResult(res);
				}
			}
			else if (packetVersion == (int)SnmpVersion.Ver2)
			{
				SnmpV2Packet pkt = new SnmpV2Packet();
				try
				{
					pkt.decode(_inbuffer, inlen);
				}
				catch (Exception ex)
				{
					PostAsyncResult("Error parsing SNMPv1 Trap: " + ex.Message);
					pkt = null;
				}
				if (pkt != null)
				{
					if (pkt.Pdu.Type == PduType.V2Trap)
					{
						PostAsyncResult(string.Format("** SNMPv2 TRAP from {0}", _peerIP.ToString()));
					}
					else if (pkt.Pdu.Type == PduType.Inform)
					{
						PostAsyncResult(string.Format("** SNMPv2 INFORM from {0}", _peerIP.ToString()));
					}
					else
					{
						PostAsyncResult(string.Format("Invalid SNMPv2 packet from {0}", _peerIP.ToString()));
						pkt = null;
					}
					if (pkt != null)
					{
						PostAsyncResult(
							string.Format("*** community {0} sysUpTime: {1} trapObjectID: {2}",
								pkt.Community, pkt.Pdu.TrapSysUpTime.ToString(), pkt.Pdu.TrapObjectID.ToString())
						);
						PostAsyncResult(string.Format("*** PDU count: {0}", pkt.Pdu.VbCount));
						foreach (Vb vb in pkt.Pdu.VbList)
						{
							PostAsyncResult(
								string.Format("**** Vb oid: {0} type: {1} value: {2}",
									vb.Oid.ToString(), SnmpConstants.GetTypeName(vb.Value.Type), vb.Value.ToString())
							);
						}
						if (pkt.Pdu.Type == PduType.V2Trap)
							PostAsyncResult("** End of SNMPv2 TRAP");
						else
						{
							PostAsyncResult("** End of SNMPv2 INFORM");

							// send ACK back to the INFORM sender
							SnmpV2Packet response = pkt.BuildInformResponse();
							byte[] buf = response.encode();
							_socket.SendTo(buf, _peerIP);
						}
					}
				}
			}
			RegisterReceiveOperation();
		}

		protected void PostAsyncResult(string msg)
		{
			localError?.Invoke(msg);
		}

		protected void PostAsyncResult(Form1.snmp_result result)
		{
			localResult?.Invoke(result);
		}

		private void Timer1_Tick(object sender, EventArgs e)
		{
			Start();
		}
	}
}
