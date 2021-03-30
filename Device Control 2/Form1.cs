﻿using System;
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
using Device_Control_2.snmp;
using Microsoft.Win32;
using SnmpSharpNet;

// Поиск по ключевым словам:
//
// Метка старости (Пересмотреть)
// Метка старости (Изменить)
// Метка старости (Удалить)
//
//
// Подсказки:
//
// Группы методов, которые используются сразу после событий или в других методах
// объединены в регионы и идут сразу после событий или методов в которых используются

namespace Device_Control_2
{
	public partial class Form1 : Form
	{
        #region Переменные
        int current_client = 0,
			selected_client = 0, // выбранный клиент
			ping_interval = 6, // периодичность быстрого (1 пакет ICMP и 1 пакет SNMP) опроса устройств (сек)
			snmp_interval = 1, // периодичность полного опроса всех устройств (мин)
			conn_state = 0; // текущее состояние связи по кабелю Ethernet:
							// 0 - отсутствие связи при запуске программы
							// 1 - потеря связи (может появиться только после хотя бы 1 успешного опроса)
							// 2 - присутствие связи
		int[] enabled; // включенные и выключенные устройства для расположения кнопок

		int[,] connection; // связь с каждым устройством: 0 - ICMP, 1 - SNMP

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

		public struct trap_result
		{
			public IPAddress Ip;
			public Vb[] vb;
		}
		#endregion Структуры

		#region Структурные объекты
		message[] note;
		Notification_message[] notifications/* = new Message[10240]*/;
		RawDeviceList.Client[] cl; // список клиентов (не более 1024 клиентов)
		#endregion Структурные объекты

		#region Классовые объекты
		AutoResetEvent waiter = new AutoResetEvent(false);

		Pdu std = new Pdu(PduType.Get);

		Notification notify;

		DeviceInfo[] deviceInfo;

		Label[] UI_labels = new Label[20];
		#endregion Классовые объекты

		#region Внешние классы
		Logs log = new Logs();
		RawDeviceList devs = new RawDeviceList();
		Traps traps = new Traps();
		Display display = new Display();
		#endregion Внешние классы

		public Form1()
		{
			InitializeComponent();

			Preprocess();

			Start();
		}

		#region Form1
		private void Preprocess()
		{
			Startup_run sr = new Startup_run();
			WindowState = sr.WindowState();

			InitStandartLabels();

			toolTip1.SetToolTip(UI_labels[0], "Версия: 2.1.4 (3)");

			cl = devs.ScanDevices;

			if (cl.Length > 0)
			{
				dataGridView2.Rows.Add(cl.Length + 1);
				traps.RegisterCallback(GetError);
				traps.RegisterCallback(GetTrap);

				InitClientList();

				InitNotifier();

				FillConstants();

				InitDeviceList();
			}
            else
            {
				UI_labels[1].Visible = false;
				UI_labels[2].Visible = false;
				UI_labels[4].Visible = false;
				UI_labels[6].Visible = false;
				UI_labels[7].Visible = false;
				UI_labels[8].Visible = false;

				dataGridView2.Visible = false;

				UI_labels[5].Visible = true;

				log.WriteEvent("Список устройств пуст");
			}
		}

		#region Preprocess
		protected delegate void PostAsyncMessageDelegate(string msg);

		public delegate void PostAsyncResultDelegate(trap_result res);

		void GetError(string msg)
		{
			//Console.WriteLine($"{txt}");

			if (InvokeRequired)
				Invoke(new PostAsyncMessageDelegate(GetError), new object[] { msg });
			//else
				//listBox1.Items.Add(msg);
		}

		void GetTrap(trap_result res)
		{
			if (InvokeRequired)
				Invoke(new PostAsyncResultDelegate(GetTrap), new object[] { res });
			else
			{
				//listBox1.Items.Add(res.Ip);
				/*dataGridView1.Rows.Add(res.Ip, "");

				foreach (Vb vb in res.vb)
				{
					//listBox1.Items.Add(vb.Oid);
					//listBox1.Items.Add(vb.Value);
					//listBox1.Items.Add(vb.Type);
					dataGridView1.Rows.Add(vb.Oid, vb.Value);
				}*/

				DecryptTrap(res);
			}
		}

		private void DecryptTrap(trap_result trap)
		{
			for (int i = 0; i < cl.Length; i++)
			{
				if (cl[i].Ip.ToString() == trap.Ip.ToString() && cl[i].Connect)
				{
					for(int j = 0; j < cl[i].Temperature.Length / 3; j++)
					{
						if(trap.vb.Length == 3)
						{
							int[] vals = new int[3];

							for (int k = 0; k < trap.vb.Length; k++)
							{
								log.WriteEvent(cl[i].Name + " / " + cl[i].Ip, "Значение переменной было изменено" + ": [" + cl[i].Temperature[j, 1] + "]=" + trap.vb[k].Value);

								vals[k] = Convert.ToInt32(trap.vb[k].Value.ToString());
							}

							foreach (Vb v in trap.vb)
                            {
								
                            }

							CheckTemperature(ChangeVar(trap.vb, cl[i].Temperature, i), i);
						}
						else
						{
							for (int k = 0; k < trap.vb.Length; k++)
							{
								if (cl[i].Temperature[j, 0] == trap.vb[k].Oid.ToString())
									log.WriteEvent(cl[i].Name + " / " + cl[i].Ip, "Значение переменной было изменено" + ": [" + cl[i].Temperature[j, 1] + "]=" + trap.vb[k].Value);
							}
						}
					}

					for (int j = 0; j < cl[i].Addition.Length / 3; j++)
					{
						foreach (Vb v in trap.vb)
						{
							if (cl[i].Addition[j, 0] == v.Oid.ToString())
								log.WriteEvent(cl[i].Name + " / " + cl[i].Ip, "Значение переменной было изменено" + ": [" + cl[i].Addition[j, 1] + "]=" + v.Value);
						}
					}
				}
			}
		}

		private int[] ChangeVar(Vb[] vbs, string[,] array, int counter)
		{
			int[] vals = new int[vbs.Length];

			for (int j = 0; j < array.Length / 3; j++)
			{
				int k = 0;

				foreach (Vb v in vbs)
				{
					if (array[j, 0] == v.Oid.ToString())
					{
						log.WriteEvent(cl[counter].Name + " / " + cl[counter].Ip, "Значение переменной было изменено" + ": [" + array[j, 1] + "]=" + v.Value);

						vals[k++] = Convert.ToInt32(v.Value.ToString());
					}
				}
			}

			return vals;
		}

		private void CheckTemperature(int[] values, int counter)
		{
			if (values[0] >= values[1] || values[0] <= values[2])
			{
				notifications[(counter * 10) + 2].State = true;
				if (notifications[(counter * 10) + 2].Time == "" || notifications[(counter * 10) + 2].Time == null)
				{
					notifications[(counter * 10) + 2].Time = DateTime.Now.ToString();

					log.WriteEvent(cl[counter].Name + " / " + cl[counter].Ip, "Нештатное значение температуры: [max temperature]= " + values[1] + " / [temperature]= " + values[0]);
				}
			}
			else
			{
				notifications[(counter * 10) + 2].State = false;
				if (notifications[(counter * 10) + 2].Time != "" && notifications[(counter * 10) + 2].Time != null)
				{
					notifications[(counter * 10) + 2].Time = "";

					log.WriteEvent(cl[counter].Name + " / " + cl[counter].Ip, "Возврат температуры к норме: [max temperature]= " + values[1] + " / [temperature]= " + values[0]);
				}
			}

			notify.Update_list(notifications);
			Focus();
		}

		private void InitStandartLabels()
        {
			for (int i = 0; i < 20; i++)
            {
				UI_labels[i] = new Label();
				UI_labels[i].AutoSize = true;
				UI_labels[i].Name = "label" + i;
				UI_labels[i].TabIndex = i + 20;
				UI_labels[i].Size = new Size(0, 15);
			}

			int j = 0;

			Controls.Add(UI_labels[j++]);

			while (j < 4) { panel1.Controls.Add(UI_labels[j++]); }

			Controls.Add(UI_labels[j++]);

			while (j < 8) { panel2.Controls.Add(UI_labels[j++]); }

			while (j < 15) { tabPage1.Controls.Add(UI_labels[j++]); }

			while (j < 19) { tabPage3.Controls.Add(UI_labels[j++]); }

			Controls.Add(UI_labels[j++]);

			UI_labels[5].BringToFront();
			UI_labels[7].BringToFront();

			UI_labels[0].Anchor = AnchorStyles.Bottom | AnchorStyles.Right;
			UI_labels[0].Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 204);
			UI_labels[0].Location = new Point(507, 279);
			UI_labels[0].Text = "v2.1.3";
			UI_labels[0].TextAlign = System.Drawing.ContentAlignment.MiddleRight;

			UI_labels[1].Font = new Font("Microsoft Sans Serif", 14.25F, FontStyle.Bold | FontStyle.Underline, GraphicsUnit.Point, 204);
			UI_labels[1].Location = new Point(30, 6);

			UI_labels[2].Location = new Point(31, 30);

			UI_labels[3].Location = new Point(320, 17);
			UI_labels[3].Text = "Ping:";
			UI_labels[3].Visible = false;

			UI_labels[4].Anchor = AnchorStyles.Bottom | AnchorStyles.Left;
			UI_labels[4].Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 204);
			UI_labels[4].Location = new Point(157, 279);
			UI_labels[4].Visible = false;

			UI_labels[5].Anchor = AnchorStyles.Top | AnchorStyles.Bottom| AnchorStyles.Left;
			UI_labels[5].AutoSize = false;
			UI_labels[5].Enabled = false;
			UI_labels[5].Location = new Point(43, 137);
			UI_labels[5].Size = new Size(69, 26);
			UI_labels[5].Text = "Устройства\r\nотсутствуют";
			UI_labels[5].TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
			UI_labels[5].Visible = false;

			for(int i = 6; i < 8; i++)
			{
				UI_labels[i].Font = new Font("Microsoft Sans Serif", 11.25F, FontStyle.Regular, GraphicsUnit.Point, 204);
				UI_labels[i].ForeColor = SystemColors.HotTrack;
				UI_labels[i].Location = new Point(5, 5);
			}

			UI_labels[6].Text = "Контролируемые\r\nустройства";

			UI_labels[7].AutoSize = false;
			UI_labels[7].Size = new Size(146, 50);
			UI_labels[7].Text = "Неконтролируемые\r\nустройства";
			UI_labels[7].TextAlign = System.Drawing.ContentAlignment.MiddleLeft;

			UI_labels[8].Location = new Point(20, 15);
			UI_labels[8].Text = "Тип устройства\r\n\r\nВремя от включения\r\n\r\nСистемное имя\r\n\r\nМестоположение";

			UI_labels[9].Location = new Point(20, 135);
			UI_labels[9].Text = "Cистемное время";
			UI_labels[9].Visible = false;

			for(int i = 10; i < 15; i++) { UI_labels[i].Location = new Point(190, ((i - 10) * 30) + 15); }

			UI_labels[15].Location = new Point(20, 15);
			UI_labels[15].Text = "Температура текущая, °С\r\n\r\nТемпература максимально\r\nдопустимая, °С\r\nТемпература минимально\r\nдопустимая, °С";
			UI_labels[15].Visible = false;

			for(int i = 16; i < 19; i++) { UI_labels[i].Location = new Point(190, ((i - 16) * 30) + 15); }

			UI_labels[19].Anchor = AnchorStyles.Top | AnchorStyles.Bottom | AnchorStyles.Left | AnchorStyles.Right;
			UI_labels[19].AutoSize = false;
			UI_labels[19].Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Regular, GraphicsUnit.Point, 204);
			UI_labels[19].Location = new Point(276, 142);
			UI_labels[19].Size = new Size(153, 16);
			UI_labels[19].Text = "Выберите устройство";
			UI_labels[19].TextAlign = System.Drawing.ContentAlignment.MiddleCenter;
		}

		private void InitClientList()
		{
			deviceInfo = new DeviceInfo[cl.Length];

			for (int i = 0; i < cl.Length; i++)
			{
				deviceInfo[i] = new DeviceInfo(cl[i]);
			}

			enabled = new int[cl.Length + 1];
			connection = new int[cl.Length, 2];

			GetConnList();
		}

		private void GetConnList()
		{
			bool is_firsttime = true;
			int disabled = 0;

			for (int i = 0, j = 0; i < cl.Length; i++)
			{
				if (cl[i].Connect)
					enabled[j++] = i;
				else
					disabled++;
			}

			enabled[cl.Length - disabled] = -1;

			if(disabled > 0)
			{
				for (int i = 0, j = cl.Length + 1 - disabled, k = 0; (i < cl.Length + 1) && (j < enabled.Length); i++)
				{
					if (enabled[i] == -1 && is_firsttime)
					{
						k = 1;
						is_firsttime = false;
					}

					if (!cl[i - k].Connect)
						enabled[j++] = i - k;
				}
			}
		}

		private void InitNotifier()
		{
			string[] devlist = new string[cl.Length];

			for (int i = 0; i < cl.Length; i++)
				devlist[i] = cl[i].Name;

			notify = new Notification(devlist);
		}
		
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
		} // Метка  старости (Удалить)

		private void InitDeviceList()
		{
			for (int i = 0, j = 0; i <= cl.Length; i++)
            {
				if (enabled[i] != -1)
				{
					if(dataGridView2[0, i].Value == null)
						dataGridView2[0, i].Value = Properties.Resources.device48;

					dataGridView2[1, i].Value = cl[enabled[i]].Name;
				}
				else
				{
					if(selected_client == 0)
						dataGridView2[0, i].Selected = true;

					UI_labels[7].Location = new Point(5, (i + 1) * 50);
				}
			}
		}
		#endregion Preprocess

		private void Start()
		{
			if (cl.Length > 0)
			{
				timer1.Interval = ping_interval * 1000;
				timer2.Interval = snmp_interval * 60000;

				if (!timer1.Enabled)
					timer1.Start();

				//if (!timer2.Enabled)
					//timer2.Start();

				SimpleSurvey();

				//Survey();
			}
			else
			{
				MessageBox.Show("Пожалуйста добавьте список устройств в файл:\n\n" + Environment.CurrentDirectory + "\\devlist.xml, и перезапустите программу.", "Устройства не найдены");
			}
		} // Метка старости (Изменить)

        #region Start
        private void SimpleSurvey()
		{
			//UI_labels[1].Text = cl[selected_client].Name;

			try // if (NetworkInterface.GetIsNetworkAvailable())
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
			}

			//Survey_grid(Fill_main());

			//TryPing(cl[choosed_client].Ip);
		} // Метка старости (Изменить)

		#region SimpleSurvey
		private int Fill_main()
		{
			SnmpV1Packet result = SurveyList(cl[selected_client].Ip, std);

			CheckStdOIDChanges(UI_labels[10].Text, 0, result.Pdu.VbList[0].Value.ToString());
			UI_labels[10].Text = result.Pdu.VbList[0].Value.ToString();
			CheckStdOIDChanges(UI_labels[11].Text, 1, result.Pdu.VbList[1].Value.ToString());
			UI_labels[11].Text = result.Pdu.VbList[1].Value.ToString();
			CheckStdOIDChanges(UI_labels[12].Text, 2, result.Pdu.VbList[2].Value.ToString());
			UI_labels[12].Text = result.Pdu.VbList[2].Value.ToString();
			CheckStdOIDChanges(UI_labels[13].Text, 3, result.Pdu.VbList[3].Value.ToString());
			UI_labels[13].Text = result.Pdu.VbList[3].Value.ToString();

			int ifNumber = Convert.ToInt32(result.Pdu.VbList[4].Value.ToString());

			GetTime();
			GetMod();
			GetAdd();

			Change_SNMP_Status(1);

			return ifNumber;
		} // Метка старости (Пересмотреть)

		#region Fill_main
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
		} // Метка старости (Удалить)

		private void GetTime()
		{
			if (cl[selected_client].SysTime != null)
			{
				UI_labels[9].Visible = true;

				Pdu systime = new Pdu(PduType.Get);

				systime.VbList.Add(cl[selected_client].SysTime);

				SnmpV1Packet result = SurveyList(cl[selected_client].Ip, systime);

				string time = "0";

				try { time = result.Pdu.VbList[0].Value.ToString(); }
				catch { Console.WriteLine("time = " + time); }

				if (result.Pdu.VbList[0].Value.Type == SnmpVariableType.TimeTicks)
					time = Decrypt_Time(time);

				CheckModOIDChanges(UI_labels[14].Text, 0, time, selected_client);
				long convertedTime = Convert.ToInt64(time); //сконвертированное в long время из string
				UI_labels[14].Text = DateTimeOffset.FromUnixTimeSeconds(convertedTime).ToString().Substring(0, 19);
			}
			else
			{
				UI_labels[9].Visible = false;
				UI_labels[14].Text = "";
			}
		}

		#region GetTime
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

		private void CheckModOIDChanges(string original, int oid_id, string oid_result, int client)
		{
			//oid_id = (selected_client * 10) + oid_id;
			//oid_id += 2;

			if (oid_result != original)
				if (original == "" || original == null)
				{
					string jopa = cl[client].Temperature[oid_id, 1];
					log.Write(cl[selected_client].Name + " / " + cl[selected_client].Ip, "Значение переменной: [" + cl[client].Temperature[oid_id, 1] + "]=" + oid_result);
				}
				else
					log.Write(cl[selected_client].Name + " / " + cl[selected_client].Ip, "Значение переменной было изменено: [" + cl[client].Temperature[oid_id, 1] + "]=" + oid_result);
		}
		#endregion GetTime

		private void GetMod()
		{
			if (cl[selected_client].Temperature != null)
			{
				UI_labels[15].Visible = true;
				//label16.Visible = true;
				//label17.Visible = true;

				Pdu modified = new Pdu(PduType.Get);

				for (int i = 0; i < cl[selected_client].Temperature.Length / 3; i++)
					modified.VbList.Add(cl[selected_client].Temperature[i, 0]);

				SnmpV1Packet result = SurveyList(cl[selected_client].Ip, modified);

				CheckTemperature(Convert.ToInt32(result.Pdu.VbList[0].Value.ToString()), Convert.ToInt32(result.Pdu.VbList[1].Value.ToString()), Convert.ToInt32(result.Pdu.VbList[2].Value.ToString()));
				//CheckPower();
				//CheckFan();

				CheckModOIDChanges(UI_labels[16].Text, 0, result.Pdu.VbList[0].Value.ToString(), selected_client);
				UI_labels[16].Text = result.Pdu.VbList[0].Value.ToString();
				CheckModOIDChanges(UI_labels[17].Text, 1, result.Pdu.VbList[1].Value.ToString(), selected_client);
				UI_labels[17].Text = result.Pdu.VbList[1].Value.ToString();
				CheckModOIDChanges(UI_labels[18].Text, 2, result.Pdu.VbList[2].Value.ToString(), selected_client);
				UI_labels[18].Text = result.Pdu.VbList[2].Value.ToString();
				//CheckModOIDChanges(label24.Text, 3, result.Pdu.VbList[3].Value.ToString(), selected_client);
				//label24.Text = result.Pdu.VbList[3].Value.ToString();

				//label16.Visible = label24.Text != "";
				//label17.Visible = label25.Text != "";
				//label18.Visible = label26.Text != "";
			}
			else
			{
				UI_labels[15].Visible = false;
				//label16.Visible = false;
				//label17.Visible = false;
				//label18.Text = "";

				//label21.Text = "";
				//label22.Text = "";
				//label23.Text = "";
				//label24.Text = "";
				//label25.Text = "";
				//label26.Text = "";
			}
		}

		#region GetMod
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
			Focus();
		}
		#endregion GetMod

		private void GetAdd()
		{
			if (cl[selected_client].Addition != null)
			{
				Pdu addition = new Pdu(PduType.Get);

				for (int i = 0; i < cl[selected_client].Addition.Length / 3; i++)
					addition.VbList.Add(cl[selected_client].Temperature[i, 0]);

				SnmpV1Packet result = SurveyList(cl[selected_client].Ip, addition);

				/*CheckModOIDChanges(label25.Text, 7, result.Pdu.VbList[7].Value.ToString());
				label25.Text = result.Pdu.VbList[7].Value.ToString();
				CheckModOIDChanges(label26.Text, 9, result.Pdu.VbList[9].Value.ToString());
				label26.Text = result.Pdu.VbList[9].Value.ToString();*/
			}
		}

		private void Change_SNMP_Status(int stat)
		{
			switch (stat)
			{
				case 0:
					pictureBox1.Image = Properties.Resources.ajax_loader;
					UI_labels[2].Text = "Соединение";
					break;
				case 1:
					pictureBox1.Image = Properties.Resources.green24;
					UI_labels[2].Text = "Режим опроса";
					break;
				case 2:
					pictureBox1.Image = Properties.Resources.orange24;
					UI_labels[2].Text = "Режим опроса";
					break;
				case 3:
					pictureBox1.Image = Properties.Resources.red24;
					UI_labels[2].Text = "Автономный";
					break;
				case 4:
					pictureBox1.Image = Properties.Resources.gray24;
					UI_labels[2].Text = "Автономный";
					break;
				case 5:
					pictureBox1.Image = Properties.Resources.gray24;
					UI_labels[2].Text = "Неактивный";
					break;
			}
		}
        #endregion Fill_main

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
		} // Метка старости (Удалить)

        #region Survey_grid
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
			{
				dataGridView1.Rows[i].HeaderCell.Value = interfaces[i, 0];

				for (int j = 0; j < 5; j++)
				{
					dataGridView1[j, i].Value = interfaces[i, j + 1];

					if (interfaces[i, 3] == "Отключен")
						dataGridView1[j, i].Style.BackColor = Color.LightGray;
				}
			}
		}
		#endregion Survey_grid
		#endregion SimpleSurvey

		private void Survey()
		{
			if (timer1.Enabled)
				timer1.Stop();

			try
			{
				Ping ping = new Ping();
				ping.PingCompleted += new PingCompletedEventHandler(Received_ping_reply);
				ping.SendAsync(cl[current_client].Ip, 3000, waiter);

				//buttons[current_client].Image = Properties.Resources.big_snake_loader;
			}
			catch
			{
				Console.WriteLine("Network is unavailable, check connection and restart program.");

				display.On();

				Console.Beep(2000, 1000);

				log.Write("Соединение отсутствует");

				CheckPingConnectionChanges(connection[current_client, 0], 0, current_client);
			}
		} // Метка старости (Удалить)

		#region Survey
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
				Focus();

				log.WriteEvent(cl[client].Name + " / " + cl[client].Ip, "Связь с устройством: [Ping]= " + connection);
			}
		} // Метка старости (Пересмотреть)
		#endregion Survey

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
		}// Метка старости (Пересмотреть)
		#endregion Start
		#endregion Form1

		#region События
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

				//buttons[current_client].Image = Properties.Resources.device_ok48;

				connection[current_client, 0] = 2;
			}
			else
			{
				CheckPingConnectionChanges(connection[current_client, 0], 1, current_client);

				//buttons[current_client].Image = Properties.Resources.device_red48;

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

		#region Received_ping_reply
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

		private void SurveyUpdate()
		{
			// Тернарная операция: z = (x > y) ? x : y;
			string time = (DateTime.Now.Hour < 10) ? "0" + DateTime.Now.Hour + ":" : DateTime.Now.Hour + ":";
			time += (DateTime.Now.Minute < 10) ? "0" + DateTime.Now.Minute : DateTime.Now.Minute.ToString();
			UI_labels[4].Text = "Последний раз обновлено: " + time;
		}
		#endregion Received_ping_reply

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

				//buttons[selected_client].Image = Properties.Resources.device_ok48;

				//WriteLog(true, "Связь присутствует");

				Survey_grid(Fill_main());
			}
			else
			{
				CheckPingConnectionChanges(connection[selected_client, 0], 1, selected_client);

				Change_Ping_Status(3);

				//buttons[selected_client].Image = Properties.Resources.device_red48;

				connection[selected_client, 0] = 1;

				display.On();

				Console.Beep(2000, 1000);

				//WriteLog(true, "Связь отсутствует");
			}

			SurveyUpdate();
		}

		#region Received_simple_reply
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
		#endregion Received_simple_reply

        private void Form1_Resize(object sender, EventArgs e)
		{
			Resize_form();
		}

		#region Form1_Resize
		private void Resize_form()
		{
			if (ClientSize.Width > 777)
			{
				int width = (ClientSize.Width - 564) / 2;
				Column2.Width = width;
				Column3.Width = width;
			}
			else
            {
				Column2.Width = 110;
				Column3.Width = 103;
            }

			//label3.Text = ClientSize.Width + ":" + ClientSize.Height; // 539 (+16) / 276 (+39)
		}
		#endregion Form1_Resize

		#region Timers
		private void timer1_Tick(object sender, EventArgs e)
		{
			Survey();
		}

		private void timer2_Tick(object sender, EventArgs e)
		{
			SimpleSurvey();
		}
		#endregion Timers

		private void Form1_FormClosing(object sender, FormClosingEventArgs e)
		{
			if (TrayIcon.Visible && cl.Length > 0)
			{
				e.Cancel = true;

				Hide();
			}
			else
			{
				TrayIcon.Visible = false;

				log.WriteEvent("Программа завершена");
			}
		}

		#region Tray
		private void openTSM_Click(object sender, EventArgs e)
		{
			Show();

			WindowState = FormWindowState.Normal;
		}

		private void commentTSM_Click(object sender, EventArgs e)
		{

		}

		private void aboutTSM_Click(object sender, EventArgs e)
		{

		}

		private void exitTSM_Click(object sender, EventArgs e)
		{
			TrayIcon.Visible = false;

			Application.Exit();
		} // Метка старости (Изменить)
		#endregion Tray

        private void dataGridView2_CellMouseClick(object sender, MouseEventArgs e)
		{
			DataGridView.HitTestInfo hit = dataGridView2.HitTest(e.X, e.Y);

			if(hit.RowIndex >= 0)
			{
				selected_client = enabled[dataGridView2.Rows[hit.RowIndex].Index];

				Switch_UI_visibility(true);

				if (e.Button == MouseButtons.Right)
				{

					dataGridView2.Rows[hit.RowIndex].Selected = true;

					//MessageBox.Show("Right click" + dataGridView2.Rows[hit.RowIndex].Index);
					if (selected_client != 0)
					{
						panel3.Location = new Point(e.X + 6, e.Y + 51);

						button1.Text = cl[selected_client].Connect ? "Завершить сканирование" : "Начать сканирование";
					}
				}

				ChangeInfo();
			}
			else
				Switch_UI_visibility(false);
		}

		#region dataGridView2_CellMouseClick
		private void ChangeInfo()
		{
			UI_labels[1].Text = cl[selected_client].Name;

			if (cl[selected_client].Connect)
				ShowInfo(deviceInfo[selected_client].status);
			else
				ClearInfo();
		}

		private void ShowInfo(DeviceInfo.Status status)
        {
			UI_labels[4].Visible = true;
			UI_labels[4].Text = status.info_updated_time;
			UI_labels[8].Visible = true;
			UI_labels[9].Visible = cl[selected_client].SysTime != null;
			UI_labels[9].Text = cl[selected_client].SysTime != null ? status.SysTime : "";
			UI_labels[10].Text = "";
			UI_labels[11].Text = "";
			UI_labels[12].Text = "";
			UI_labels[13].Text = "";
			UI_labels[14].Text = "";
			UI_labels[15].Visible = cl[selected_client].Temperature != null;
			UI_labels[16].Text = "";
			UI_labels[17].Text = "";
			UI_labels[18].Text = "";
		}

		private void ClearInfo()
		{
			Change_SNMP_Status(5);
			dataGridView1.Rows.Clear();

			UI_labels[4].Text = "";
			UI_labels[8].Visible = false;
			UI_labels[9].Visible = false;

			for (int i = 10; i < 15; i++) { UI_labels[i].Text = ""; }
			UI_labels[15].Visible = cl[selected_client].Temperature != null;
			for (int i = 16; i < 19; i++) { UI_labels[i].Text = ""; }
		}

		private void Switch_UI_visibility(bool show_UI)
		{
			panel1.Visible = show_UI;
			tabControl1.Visible = show_UI;
			UI_labels[4].Visible = show_UI;

			if (dataGridView2.Rows.Count > 0 && !show_UI)
			{
				for (int i = 0; i < enabled.Length; i++)
				{
					if (enabled[i] == -1)
						dataGridView2[0, i].Selected = true;
				}
			}
		}
		#endregion dataGridView2_CellMouseClick

		#region Button
		private void button1_MouseEnter(object sender, EventArgs e)
		{
			panel3.BackColor = SystemColors.Highlight;
		}

		private void button1_MouseLeave(object sender, EventArgs e)
        {
			//HideButton();
		}

        private void button_Click(object sender, EventArgs e)
		{
			//int button = FindGroup(sender, buttons);

			//if (cl[button].Connect)
			//selected_client = button;

			//ChangeInfo(button);

			//SimpleSurvey();

			HideButton();

			cl[selected_client].Connect = cl[selected_client].Connect ? false : true;

			GetConnList();

			InitDeviceList();
		}

		#region button_Click
		private void HideButton()
		{
			panel3.Location = new Point(0, -100);
			panel3.BackColor = Color.FromArgb(173, 173, 173);
		}
		#endregion button_Click
		#endregion Button
		#endregion

		#region Доп. Методы
		private int FindGroup(object sender, object[] compare_with)
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
