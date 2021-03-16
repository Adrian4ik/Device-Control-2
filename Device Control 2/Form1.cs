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
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using System.Windows.Forms.VisualStyles;
using Device_Control_2.Features;
using Microsoft.Win32;
using SnmpSharpNet;

namespace Device_Control_2
{
	public partial class Form1 : Form
	{
		// Version: 2.1.3
		// Patch: 3.9

		const string vCore = "2";
		const string vInterface = "1";
		const string vUpdate = "3";

		#region Переменные
		int current_client = 0,
			selected_client = 0, // выбранный клиент
			ping_interval = 6, // периодичность быстрого (1 пакет ICMP и 1 пакет SNMP) опроса устройств (сек)
			snmp_interval = 1, // периодичность полного опроса всех устройств (мин)
			conn_state = 0; // текущее состояние связи по кабелю Ethernet:
							// 0 - отсутствие связи при запуске программы
							// 1 - потеря связи (может появиться только после хотя бы 1 успешного опроса)
							// 2 - присутствие связи

		int[,] connection = new int[1024, 2]; // связь с каждым устройством: 0 - ICMP, 1 - SNMP

		string[,] interfaces; // = new string[1024, 6];
		#endregion Переменные

		#region Структуры
		public struct message
		{
			public bool State { get; set; }
			public int DeviceId { get; set; }
			public int MessageId { get; set; }
		}

		public struct Notification_message
		{
			public int Criticality { get; set; }
			public bool State { get; set; }
			public string Text { get; set; }
			public string Time { get; set; }
		}
		#endregion

		#region Структурные объекты
		message[] note;

		Notification_message[] notifications/* = new Message[10240]*/;

		Devices.Client[] cl; // список клиентов (не более 1024 клиентов)
		#endregion

		#region Классовые объекты
		AutoResetEvent waiter = new AutoResetEvent(false);

		Pdu std = new Pdu(PduType.Get);

		Button[] buttons;

		Notification notify;
		#endregion

		#region Внешние классы
		Logs log = new Logs();
		Devices devs = new Devices();
		Startup_run sr = new Startup_run();
		Display display = new Display();
        #endregion

        #region Методы
        public Form1()
		{
			InitializeComponent();

			Preprocess();

			InitNotifier();

			FillConstants();

			SimpleSurvey();

			Survey();
		}

		private void Preprocess()
		{
			label9.Text = "v2.1.3";

			if (sr.minimized)
				WindowState = FormWindowState.Minimized;

			Change_SNMP_Status(4);
			Change_Ping_Status(4);

			cl = devs.ScanDevices;
		}

		private void InitNotifier()
		{
			string[] devlist = new string[cl.Length];

			for (int i = 0; i < cl.Length; i++)
				devlist[i] = cl[i].Name;

			notify = new Notification(devlist);
		}
		// Доделать
		private void FillConstants()
		{
			std.VbList.Add("1.3.6.1.2.1.1.1.0"); // sysDescr
			std.VbList.Add("1.3.6.1.2.1.1.3.0"); // sysUpTime
			std.VbList.Add("1.3.6.1.2.1.1.5.0"); // sysName
			std.VbList.Add("1.3.6.1.2.1.1.6.0"); // sysLocation
			std.VbList.Add("1.3.6.1.2.1.2.1.0"); // ifNumber

			notifications = new Notification_message[cl.Count() * 10];

			for (int i = 0; i < notifications.Count(); i++)
			{
				int n_id = i % 10;

				switch (n_id)
				{
					case 0:
						//notifications[i].Text = "Нештатное состояние системы питания устройства " + cl[(i / 10) + 0].Name;
						notifications[i].Text = "Прервана связь с устройством " + cl[(i / 10) + 0].Name;
						notifications[i].Criticality = 2;
						break;
					case 1:
						notifications[i].Text = "Нештатное состояние системы питания устройства " + cl[(i / 10) + 0].Name;
						notifications[i].Criticality = 2;
						break;
					case 2:
						notifications[i].Text = "Нештатное значение температуры устройства " + cl[(i / 10) + 0].Name;
						notifications[i].Criticality = 2;
						break;
					case 3:
						notifications[i].Text = "Нештатное состояние вентилятора устройства " + cl[(i / 10) + 0].Name;
						notifications[i].Criticality = 2;
						break;
				}
			}

			InitInterface();

			timer1.Interval = ping_interval * 1000;
			timer2.Interval = snmp_interval * 60000;

			if (!timer1.Enabled)
				timer1.Start();

			if (!timer2.Enabled)
				timer2.Start();
		}

		private void InitInterface()
		{
			buttons = new Button[cl.Length];

			int e = -1;

			for (int i = 0; i < cl.Length; i++)
			{
				buttons[i] = new Button();

				buttons[i].BackColor = SystemColors.ControlLightLight;
				buttons[i].ImageAlign = System.Drawing.ContentAlignment.MiddleLeft;
				buttons[i].Name = "button" + i;

				if (cl[i].Connect)
				{
					buttons[i].Location = new Point(5, i * 54 + 46);
					++e;
				}

				buttons[i].Size = new Size(149, 54);
				buttons[i].Text = cl[i].Name;
				buttons[i].TextImageRelation = TextImageRelation.ImageBeforeText;
				buttons[i].UseVisualStyleBackColor = false;
				buttons[i].Image = Properties.Resources.device48;
				buttons[i].Click += new EventHandler(button_Click);
			}

			label5.Location = new Point(5, buttons[e].Location.Y + 54);

			for (int i = 0; i < cl.Length; i++)
			{
				panel2.Controls.Add(buttons[i]);

				if (!cl[i].Connect)
					buttons[i].Location = new Point(5, i * 54 + buttons[e].Location.Y + 90);
			}
		}

		private void SimpleSurvey()
		{
			label1.Text = cl[selected_client].Name;

			/*try // if (NetworkInterface.GetIsNetworkAvailable())
			{
				Ping ping = new Ping();
				ping.PingCompleted += new PingCompletedEventHandler(Received_simple_reply);
				ping.SendAsync(cl[current_client].Ip, 3000, waiter);

				Change_Ping_Status(0);
			}
			catch // else
			{
				Console.WriteLine("Network is unavailable, check connection and restart program.");

				Console.Beep(2000, 1000);

				log.Write("Соединение отсутствует");

				CheckPingConnectionChanges(connection[current_client, 0], 0, current_client);

				Change_SNMP_Status(4);
			}*/

			Survey_grid(Fill_main());

			//TryPing(cl[choosed_client].Ip);
		}

		private void Survey()
		{
			if (timer1.Enabled)
				timer1.Stop();

			try
			{
				Ping ping = new Ping();
				ping.PingCompleted += new PingCompletedEventHandler(Received_ping_reply);
				ping.SendAsync(cl[current_client].Ip, 3000, waiter);

				buttons[current_client].Image = Properties.Resources.big_snake_loader;
			}
			catch
			{
				Console.WriteLine("Network is unavailable, check connection and restart program.");

				display.On();

				Console.Beep(2000, 1000);

				log.Write("Соединение отсутствует");

				CheckPingConnectionChanges(connection[current_client, 0], 0, current_client);
			}


		}

		private void TryPing(string ip)
		{
			try // if (NetworkInterface.GetIsNetworkAvailable())
			{
				Ping ping = new Ping();
				//ping.PingCompleted += new PingCompletedEventHandler(Received_simple_reply);
				ping.SendAsync(ip, 3000, waiter);
			}
			catch // else
			{
				display.On();

				Console.Beep(2000, 1000);

				Connection(false);
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

			Connection(true);

			if (e.Reply.Status == IPStatus.Success)
			{
				CheckPingConnectionChanges(connection[current_client, 0], 2, current_client);

				buttons[current_client].Image = Properties.Resources.device_ok48;

				connection[current_client, 0] = 2;
			}
			else
			{
				CheckPingConnectionChanges(connection[current_client, 0], 1, current_client);

				buttons[current_client].Image = Properties.Resources.device_red48;

				connection[current_client, 0] = 1;

				display.On();

				Console.Beep(2000, 1000);
			}

			SurveyUpdate();

			if (++current_client == cl.Length)
			{
				current_client = 0;

				if (!timer1.Enabled)
					timer1.Start();
			}
			else
				Survey();
		}

		private void Received_simple_reply(object sender, PingCompletedEventArgs e)
		{
			if (e.Cancelled)
				((AutoResetEvent)e.UserState).Set();

			if (e.Error != null)
				((AutoResetEvent)e.UserState).Set();

			// Let the main thread resume.
			((AutoResetEvent)e.UserState).Set();

			Connection(true);

			if (e.Reply.Status == IPStatus.Success)
			{
				CheckPingConnectionChanges(connection[selected_client, 0], 2, selected_client);

				Change_Ping_Status(1);
				Change_SNMP_Status(0);

				connection[selected_client, 0] = 2;

				buttons[selected_client].Image = Properties.Resources.device_ok48;

				//WriteLog(true, "Связь присутствует");

				Survey_grid(Fill_main());
			}
			else
			{
				CheckPingConnectionChanges(connection[selected_client, 0], 1, selected_client);

				Change_Ping_Status(3);

				buttons[selected_client].Image = Properties.Resources.device_red48;

				connection[selected_client, 0] = 1;

				display.On();

				Console.Beep(2000, 1000);

				//WriteLog(true, "Связь отсутствует");
			}

			SurveyUpdate();
		}

		private void Connection(bool success)
		{
			if (success)
			{
				if (conn_state == 0)
					log.WriteEvent("Соединение присутствует");
				else if (conn_state == 1)
					log.WriteEvent("Соединение восстановлено");

				conn_state = 2;
			}
			else
			{
				if (conn_state == 0)
					log.WriteEvent("Соединение отсутствует");
				else if (conn_state == 2)
					log.WriteEvent("Соединение утеряно");

				conn_state = 1;
			}
		}

		private void CheckPingConnectionChanges(int original, int status, int client)
		{
			string connection = "";

			if (status != original)
			{
				if (original == 0)
				{
					if (status == 2)
						connection = "присутствует";
					else
					{
						connection = "отсутствует";

						notifications[(client * 10) + 0].State = true;
						if (notifications[(client * 10) + 0].Time == "" || notifications[(client * 10) + 0].Time == null)
							notifications[(client * 10) + 0].Time = DateTime.Now.ToString();
					}
				}
				else
				{
					if (status == 2)
					{
						connection = "восстановлена";

						notifications[(client * 10) + 0].State = false;
						if (notifications[(client * 10) + 0].Time != "" && notifications[(client * 10) + 0].Time != null)
							notifications[(client * 10) + 0].Time = "";
					}
					else
					{
						connection = "утеряна";

						notifications[(client * 10) + 0].State = true;
						if (notifications[(client * 10) + 0].Time == "" || notifications[(client * 10) + 0].Time == null)
							notifications[(client * 10) + 0].Time = DateTime.Now.ToString();
					}
				}

				notify.Update_list(notifications);

				log.WriteEvent(cl[client].Name + " / " + cl[client].Ip, "Связь с устройством: [Ping]= " + connection);
			}
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
					Change_SNMP_Status(3);
					break;
				case 4:
					pictureBox2.Image = Properties.Resources.gray24;
					Change_SNMP_Status(4);
					break;
			}
		}

		private int Fill_main()
		{
			SnmpV1Packet result = SurveyList(cl[selected_client].Ip, std);

			CheckStdOIDChanges(label11.Text, 0, result.Pdu.VbList[0].Value.ToString());
			label11.Text = result.Pdu.VbList[0].Value.ToString();
			CheckStdOIDChanges(label12.Text, 1, result.Pdu.VbList[1].Value.ToString());
			label12.Text = result.Pdu.VbList[1].Value.ToString();
			CheckStdOIDChanges(label13.Text, 2, result.Pdu.VbList[2].Value.ToString());
			label13.Text = result.Pdu.VbList[2].Value.ToString();
			CheckStdOIDChanges(label14.Text, 3, result.Pdu.VbList[3].Value.ToString());
			label14.Text = result.Pdu.VbList[3].Value.ToString();

			int ifNumber = Convert.ToInt32(result.Pdu.VbList[4].Value.ToString());

			GetTime();
			GetMod();
			GetAdd();

			Change_SNMP_Status(1);

			return ifNumber;
		}

		private void CheckStdOIDChanges(string original, int oid_id, string oid_result)
		{
			string was_changed = "";
			string[] std_oid_names = { "sysDescr", "sysUpTime", "sysName", "sysLocation" };

			if (oid_result != original)
			{
				if (original != "" && original != null)
					was_changed = " было изменено";

				log.Write(cl[selected_client].Name + " / " + cl[selected_client].Ip, "Значение переменной" + was_changed + ": [" + std_oid_names[oid_id] + "]=" + oid_result);
			}
		}

		private void GetTime()
		{
			if (cl[selected_client].SysTime != null)
			{
				Pdu systime = new Pdu(PduType.Get);

				systime.VbList.Add(cl[selected_client].SysTime);

				SnmpV1Packet result = SurveyList(cl[selected_client].Ip, systime);

				string time = "0";

				try { time = result.Pdu.VbList[0].Value.ToString(); }
				catch { Console.WriteLine("time = " + time); }

				if (result.Pdu.VbList[0].Value.Type == SnmpVariableType.TimeTicks)
					time = Decrypt_Time(time);

				CheckModOIDChanges(label15.Text, 0, time, selected_client);
				long convertedTime = Convert.ToInt64(time); //сконвертированное в long время из string
				label15.Text = DateTimeOffset.FromUnixTimeSeconds(convertedTime).ToString().Substring(0, 19);
			}
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

		private void GetMod()
		{
			if (cl[selected_client].Modified != null)
			{
				Pdu modified = new Pdu(PduType.Get);

				for(int i = 0; i < cl[selected_client].Modified.Length / 3; i++)
					modified.VbList.Add(cl[selected_client].Modified[i, 0]);

				SnmpV1Packet result = SurveyList(cl[selected_client].Ip, modified);

				CheckTemperature(Convert.ToInt32(result.Pdu.VbList[0].Value.ToString()), Convert.ToInt32(result.Pdu.VbList[1].Value.ToString()), Convert.ToInt32(result.Pdu.VbList[2].Value.ToString()));
				//CheckPower();
				//CheckFan();

				CheckModOIDChanges(label21.Text, 0, result.Pdu.VbList[0].Value.ToString(), selected_client);
				label21.Text = result.Pdu.VbList[0].Value.ToString();
				CheckModOIDChanges(label22.Text, 1, result.Pdu.VbList[1].Value.ToString(), selected_client);
				label22.Text = result.Pdu.VbList[1].Value.ToString();
				CheckModOIDChanges(label23.Text, 2, result.Pdu.VbList[2].Value.ToString(), selected_client);
				label23.Text = result.Pdu.VbList[2].Value.ToString();
				CheckModOIDChanges(label24.Text, 3, result.Pdu.VbList[3].Value.ToString(), selected_client);
				label24.Text = result.Pdu.VbList[3].Value.ToString();
			}
		}

		private void CheckTemperature(int cur_t, int max_t, int min_t)
		{
			if (cur_t >= max_t || cur_t <= min_t)
			{
				notifications[(selected_client * 10) + 2].State = true;
				if (notifications[(selected_client * 10) + 2].Time == "" || notifications[(selected_client * 10) + 2].Time == null)
				{
					notifications[(selected_client * 10) + 2].Time = DateTime.Now.ToString();

					log.WriteEvent(cl[selected_client].Name + " / " + cl[selected_client].Ip, "Нештатное значение температуры: [max temperature]= " + max_t + " / [temperature]= " + cur_t);
				}
			}
			else
			{
				notifications[(selected_client * 10) + 2].State = false;
				if (notifications[(selected_client * 10) + 2].Time != "" && notifications[(selected_client * 10) + 2].Time != null)
				{
					notifications[(selected_client * 10) + 2].Time = "";

					log.WriteEvent(cl[selected_client].Name + " / " + cl[selected_client].Ip, "Возврат температуры к норме: [max temperature]= " + max_t + " / [temperature]= " + cur_t);
				}
			}

			notify.Update_list(notifications);
		}

		private void GetAdd()
		{
			if (cl[selected_client].Addition != null)
			{
				Pdu addition = new Pdu(PduType.Get);

				for (int i = 0; i < cl[selected_client].Addition.Length / 3; i++)
					addition.VbList.Add(cl[selected_client].Modified[i, 0]);

				SnmpV1Packet result = SurveyList(cl[selected_client].Ip, addition);

				/*CheckModOIDChanges(label25.Text, 7, result.Pdu.VbList[7].Value.ToString());
				label25.Text = result.Pdu.VbList[7].Value.ToString();
				CheckModOIDChanges(label26.Text, 9, result.Pdu.VbList[9].Value.ToString());
				label26.Text = result.Pdu.VbList[9].Value.ToString();*/
			}
		}

		private void CheckModOIDChanges(string original, int oid_id, string oid_result, int client)
		{
			//oid_id = (selected_client * 10) + oid_id;
			//oid_id += 2;

			if (oid_result != original)
				if (original == "" || original == null)
				{
					string jopa = cl[client].Modified[oid_id, 1];
					log.Write(cl[selected_client].Name + " / " + cl[selected_client].Ip, "Значение переменной: [" + cl[client].Modified[oid_id, 1] + "]=" + oid_result);
				}
				else
					log.Write(cl[selected_client].Name + " / " + cl[selected_client].Ip, "Значение переменной было изменено: [" + cl[client].Modified[oid_id, 1] + "]=" + oid_result);
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
			int fi = 0, ri = 0;

			// Изменить под пропуски ifIndex
			//for (int i = 1; i <= ifNum; i++) // строки

			int i = 1, k = 0, empty = 0;

			interfaces = new string[ifNum, 6];

			while (/*flag == */true) // бред, но работает только в том случае, если оиды из одной подсетки (из разных запрещено делать запросы)
			{
				Pdu list = new Pdu(PduType.Get);
								
				list.VbList.Add("1.3.6.1.2.1.2.2.1.1." + i); // 1 столбец
				list.VbList.Add("1.3.6.1.2.1.2.2.1.2." + i); // 2 столбец
				list.VbList.Add("1.3.6.1.2.1.2.2.1.3." + i); // 6 столбец
				list.VbList.Add("1.3.6.1.2.1.2.2.1.5." + i); // 5 столбец
				list.VbList.Add("1.3.6.1.2.1.2.2.1.8." + i); // 4 столбец

				SnmpV1Packet result = SurveyList(cl[selected_client].Ip, list);

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
					case "23":
						type = "ppp";
						break;
					case "24":
						type = "softwareLoopback";
						break;
					case "71":
						type = "ieee80211";
						break;
					case "131":
						type = "tunnel";
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

				if (type == "Ethernet")
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
					ri++;
					empty = 0;

					if (k == ifNum || ri == ifNum)
						break;
				}
				else if (type == "Other" || type == "l2vlan" || type == "ieee8023AdLag")
					break;
				else if (empty >= 100)
					break;
				else if (result.Pdu.VbList[2].Value.ToString() != "Null")
				{
					ri++;
					empty = 0;
				}
				else
					empty++;
				
				i++;
			}

			if (cl[selected_client].IfName != null)
			{
				int ilimit = i;

				i = 1;
				k = 0;

				while (true)
				{
					Pdu list = new Pdu(PduType.Get);

					list.VbList.Add(cl[selected_client].IfName + i++); // 3 столбец

					SnmpV1Packet result = SurveyList(cl[selected_client].Ip, list);

					if (result.Pdu.VbList[0].Value.ToString() != "Null")
					{
						CheckINamesChanges(interfaces[k, 2], k, result.Pdu.VbList[0].Value.ToString());
						interfaces[k++, 2] = result.Pdu.VbList[0].Value.ToString();
					}

					if (i == ilimit || k == ifNum)
						break;
				}
			}

			Fill_grid(fi);
		}

		private void CheckITableChanges(string original, int ifindex, string oid_result, string oid_name)
		{
			if (oid_result != original)
				if (original == "" || original == null)
					log.Write(cl[selected_client].Name + " / " + cl[selected_client].Ip, "Значение переменной: [" + oid_name + ":" + ++ifindex + "]=" + oid_result);
				else
					log.Write(cl[selected_client].Name + " / " + cl[selected_client].Ip, "Значение переменной было изменено: [" + oid_name + ":" + ++ifindex + "]=" + oid_result);
		}

		private void CheckINamesChanges(string original, int oid_id, string oid_result)
		{
			if (oid_result != original)
				if (original == "" || original == null)
					log.Write(cl[selected_client].Name + " / " + cl[selected_client].Ip, "Значение переменной: [ifname:" + ++oid_id + "]=" + oid_result);
				else
					log.Write(cl[selected_client].Name + " / " + cl[selected_client].Ip, "Значение переменной было изменено: [ifname:" + ++oid_id + "]=" + oid_result);
		}

		private void Fill_grid(int rows_count)
		{
			dataGridView1.Rows.Clear();
			dataGridView1.Rows.Add(rows_count);

			for (int i = 0; i < rows_count; i++)
				for(int j = 0; j < 6; j++)
					dataGridView1[j, i].Value = interfaces[i, j];

			for (int i = 0; i < rows_count; i++)
				if (interfaces[i, 3] == "Отключен")
					for (int j = 0; j < 6; j++)
						dataGridView1[j, i].Style.BackColor = Color.FromArgb(223, 223, 223);
		}

		private SnmpV1Packet SurveyList(IPAddress ip, Pdu list)
		{
			// SNMP community name
			//OctetString comm;

			/*if (community == 1)
				comm = new OctetString("private");
			else*/
			OctetString comm = new OctetString("public");

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
			UdpTarget target = new UdpTarget(ip, 161, 2000, 1);

			// Pdu class used for all requests
			Pdu pdu = list;

			SnmpV1Packet result = null;

			try
			{
				// Make SNMP request
				result = (SnmpV1Packet)target.Request(list, param);
			}
			catch
			{
				//MessageBox.Show("пропадание связи по SNMP");
				log.WriteEvent(cl[selected_client].Name + " / " + cl[selected_client].Ip, "Связь с устройством: [SNMP]= утеряна");
			}

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
			string time = (DateTime.Now.Hour < 10) ? "0" + DateTime.Now.Hour + ":" : DateTime.Now.Hour + ":";
			time += (DateTime.Now.Minute < 10) ? "0" + DateTime.Now.Minute : DateTime.Now.Minute.ToString();
			label3.Text = "Последний раз обновлено: " + time;
		}
        #endregion

        #region События
        private void Form1_Resize(object sender, EventArgs e)
		{
			Resize_form();
		}

		private void Resize_form()
		{
			if (ClientSize.Width > 777)
			{
				int width = (ClientSize.Width - 564) / 2;
				Column2.Width = width;
				Column3.Width = width;
			}

			//label3.Text = ClientSize.Width + ":" + ClientSize.Height; // 539 / 276
		}

		private void timer1_Tick(object sender, EventArgs e)
		{
			Survey();
		}

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if(TrayIcon.Visible)
			{
				e.Cancel = true;

				Hide();
			}
			else
				log.WriteEvent("Программа завершена");
		}

		private void openTSM_Click(object sender, EventArgs e)
		{
			Show();

			WindowState = FormWindowState.Normal;
		}

		private void exitTSM_Click(object sender, EventArgs e)
		{
			TrayIcon.Visible = false;

			Application.Exit();
		}

		private void timer2_Tick(object sender, EventArgs e)
		{
			SimpleSurvey();
		}

		private void Form1_ClientSizeChanged(object sender, EventArgs e)
		{
			Resize_form();
		}

		private void button_Click(object sender, EventArgs e)
		{
			int button = WhatGroup(sender, buttons);

			if (cl[button].Connect)
				selected_client = button;

			SimpleSurvey();
		}

		private void button_client_Click(object sender, EventArgs e)
		{

		}

		private int WhatGroup(object sender, object[] compare_with)
		{
			int result;

			for (result = 0; result < cl.Length; result++)
				if (sender.Equals(compare_with[result]))
					break;

			return result;
		}
        #endregion
    }
}
