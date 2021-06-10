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
using Device_Control_2.snmp;

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
// Группы методов, которые используются сразу после событий или в других методах,
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

		public struct snmp_result
		{
			public IPAddress Ip;
			public Vb[] vb;
		}

		public struct note
		{
			public int id;

			public bool[] type; // список уведомлений (false означает, что всё нормально)
								// если связь плохая, то в ячейке утеранной связи должно стоять false
								// можно перевести в одну int переменную
								// 
								// 0 - связь утеряна
								// 1 - связь icmp утеряна
								// 2 - связь snmp утеряна
								// 3 - нештатка по температуре
								// 4 - нештатка по питанию

			public bool[] add_type; // доп. нештатка, если присутствует
		}
		#endregion Структуры

		#region Структурные объекты
		message[] mess;
		public static Notification_message[] notifications/* = new Message[10240]*/;
		note notif;
		RawDeviceList.Client[] cl; // список клиентов (не более 1024 клиентов)
		#endregion Структурные объекты

		#region Классовые объекты
		AutoResetEvent waiter = new AutoResetEvent(false);

		Pdu std = new Pdu(PduType.Get);

		public static Notification notify;

		Traps traps;

		DeviceInfo[] deviceInfo;

		Label[] UI_labels = new Label[20];
		Label[] Add_names;
		Label[] Add_labels;
		#endregion Классовые объекты

		#region Внешние классы
		Logs log = new Logs();
		RawDeviceList devs = new RawDeviceList();
		#endregion Внешние классы

		const string version = "2.1.4", patch = " (29.1)";

		public Form1()
		{
			InitializeComponent();

			Preprocess();

			Start();
		}

		#region Form1
		void Preprocess()
		{
			Startup_run sr = new Startup_run();
			WindowState = sr.WindowState();

			InitStandartLabels();

			toolTip1.SetToolTip(UI_labels[0], "Версия: " + version + patch);

			cl = devs.ScanDevices;

			if (cl.Length > 0)
			{
				dataGridView2.Rows.Add(cl.Length + 1);
				traps = new Traps(GetTrap, GetError);

				log.WriteEvent("Программа запущена");

				InitAdditionalLabels();

				FillConstants();

				InitClientList();

				InitNotifier();

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

		public delegate void PostAsyncResultDelegate(snmp_result res);

		void GetError(string msg)
		{
			//Console.WriteLine($"{txt}");

			if (InvokeRequired)
				Invoke(new PostAsyncMessageDelegate(GetError), new object[] { msg });
			//else
				//listBox1.Items.Add(msg);
		}

		void GetTrap(snmp_result res)
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

		void DecryptTrap(snmp_result trap) //------------------------------------------
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

		int[] ChangeVar(Vb[] vbs, string[,] array, int counter)
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

		void CheckTemperature(int[] values, int counter)
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

		void InitStandartLabels()
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
			UI_labels[0].Text = "v " + version;
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

		void InitAdditionalLabels()
        {
			int i = 1, j = 0, k = 0;

			if (cl.Length > 2)
			{
				while (i < cl.Length)
				{
					if (cl[i].Addition != null)
					{
						if (cl[j].Addition != null)
							k = cl[i].Addition.Length > cl[j].Addition.Length ? cl[i].Addition.Length / 6 : cl[j].Addition.Length / 6;

						i++;
						j++;
					}
					else
						i++;
				}
			}
			else if (cl.Length == 2)
			{
				if (cl[j].Addition != null)
				{
					k = cl[j].Addition.Length / 6;

					if (cl[i].Addition != null)
						k = cl[i].Addition.Length > cl[j].Addition.Length ? cl[i].Addition.Length / 6 : cl[j].Addition.Length / 6;
				}
				else
                {
					if (cl[i].Addition != null)
						k = cl[i].Addition.Length / 6;
					else
						k = 0;
				}
			}
			else if (cl[0].Addition != null)
				k = cl[0].Addition.Length / 6;

			if(k > 0)
			{
				Add_names = new Label[k];
				Add_labels = new Label[k];

				for (int l = 0; l < k; l++)
				{
					Add_names[l] = new Label();
					Add_names[l].AutoSize = true;
					Add_names[l].Name = "Add_name" + l;
					Add_names[l].TabIndex = l + 40;
					Add_names[l].Size = new Size(0, 15);

					Add_labels[l] = new Label();
					Add_labels[l].AutoSize = true;
					Add_labels[l].Name = "Add_label" + l;
					Add_labels[l].TabIndex = l + 50;
					Add_labels[l].Size = new Size(0, 15);

					tabPage3.Controls.Add(Add_names[l]);
					tabPage3.Controls.Add(Add_labels[l]);

					Add_names[l].Location = new Point(20, (l * 30) + 105);
					Add_names[l].Text = "";
					Add_names[l].Visible = true;

					Add_labels[l].Location = new Point(190, (l * 30) + 105);
					Add_labels[l].Text = "";
					Add_labels[l].Visible = true;
				}
			}
		}

		void InitClientList()
		{
			deviceInfo = new DeviceInfo[cl.Length];

			enabled = new int[cl.Length + 1];
			connection = new int[cl.Length, 2];

			GetConnList();

			for (int i = 0; i < cl.Length; i++) { deviceInfo[i] = new DeviceInfo(cl[i], ShowClientStatus, ShowClientNotification); }
		}

		delegate void ShowClientStatusDelegate(DeviceInfo.Status status);

		void ShowClientStatus(DeviceInfo.Status status)
		{
			if (InvokeRequired)
				Invoke(new ShowClientStatusDelegate(ShowClientStatus), new object[] { status });
			else if (cl[status.id].Connect)
			{
				Change_SNMP_Status(status.snmp_conn, status.id);

				if (panel1.Visible && status.id == selected_client)
					ShowInfo(status);
			}
		}

		void ShowClientNotification(note result)
		{
			//notifications[(result.id * 10) + 2].State;
			//notifications[0].
			notify.Update_list(notifications);
			Focus();
		}


		void GetConnList()
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

		void InitNotifier()
		{
			string[] devlist = new string[cl.Length];

			for (int i = 0; i < cl.Length; i++)
				devlist[i] = cl[i].Name;

			notify = new Notification(devlist);
		}
		
		void FillConstants()
		{
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

		void InitDeviceList()
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

		void Start()
		{
			if (cl.Length <= 0 )
				MessageBox.Show("Пожалуйста добавьте список устройств в файл:\n\n" + Environment.CurrentDirectory + "\\devlist.xml, и перезапустите программу.", "Устройства не найдены");
		}

        #region Start

		#region SimpleSurvey

		#region Fill_main
		SnmpV1Packet SurveyList(IPAddress ip, Pdu list)
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

		void Change_SNMP_Status(int stat, int client)
		{
			switch (stat)
			{
				case 0:
					dataGridView2[0, FindRowFromClient(client)].Value = Properties.Resources.device_ok48;
					break;
				case 1:
					dataGridView2[0, FindRowFromClient(client)].Value = Properties.Resources.device_warning48;
					break;
				case 2:
					dataGridView2[0, FindRowFromClient(client)].Value = Properties.Resources.device_fail48;
					break;
				case 5:
					dataGridView2[0, FindRowFromClient(client)].Value = Properties.Resources.device48;
					break;
				/*case 0:
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
					break;*/
			}
		}

		int FindRowFromClient(int client)
		{
			//if (cl[client].Connect)
				//return client;

			int row = 0;

			for (int i = 0; i < enabled.Length; i++)
			{
				if (enabled[i] == client)
				{
					row = i;
					break;
				}
			}

			return row;
		}
		#endregion Fill_main

		void Survey_grid(int ifNum)
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

		private void Fill_grid(string[,] iftable, string[] ifnames, int if_count)
		{
			dataGridView1.Rows.Clear();

			if (iftable != null && iftable.Length != 0)
			{
				dataGridView1.Rows.Add(if_count);

				for (int i = 0; i < if_count; i++)
				{
					dataGridView1.Rows[i].HeaderCell.Value = iftable[i, 0];
					dataGridView1[0, i].Value = iftable[i, 1];

					for (int j = 2; j < 5; j++) { dataGridView1[j, i].Value = iftable[i, j]; }

					if (iftable[i, 2] == "Отключен")
						for(int j = 0; j < 5; j++)
							dataGridView1[j, i].Style.BackColor = Color.LightGray;
				}

				if(ifnames != null && ifnames.Length != 0)
                {
					for(int i = 0; i < if_count; i++)
                    {
						dataGridView1[1, i].Value = ifnames[i];
                    }
                }
			}
		}
		#endregion Survey_grid
		#endregion SimpleSurvey

		#endregion Start
		#endregion Form1

		#region События

		#region Received_simple_reply
		private void Change_Ping_Status(int stat)
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
					/*case 0:
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
						break;*/
			}
		}
		#endregion Received_simple_reply

        void Form1_Resize(object sender, EventArgs e)
		{
			if (WindowState == FormWindowState.Minimized)
			{
				WindowState = FormWindowState.Normal;

				Hide();
			}
			else
				Resize_form();
		}

		#region Form1_Resize
		private void Resize_form()
		{
			if (ClientSize.Width > 777)
			{
				int width = (ClientSize.Width - 556) / 2;
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

		void Form1_FormClosing(object sender, FormClosingEventArgs e)
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
		void TrayIcon_MouseDoubleClick(object sender, MouseEventArgs e)
		{
			ShowWindow();
		}

		void openTSM_Click(object sender, EventArgs e)
		{
			ShowWindow();
		}

		void ShowWindow()
		{
			Show();

			WindowState = FormWindowState.Normal;

			Focus();
		}

		void commentTSM_Click(object sender, EventArgs e)
		{

		}

		void aboutTSM_Click(object sender, EventArgs e)
		{

		}

		void exitTSM_Click(object sender, EventArgs e)
		{
			TrayIcon.Visible = false;

			Application.Exit();
		} // Метка старости (Изменить)
		#endregion Tray

        void dataGridView2_CellMouseClick(object sender, MouseEventArgs e)
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
		void ChangeInfo() //---------------------------------------------------------------------------
		{
			UI_labels[1].Text = cl[selected_client].Name;

			if (cl[selected_client].Connect) // && deviceInfo[selected_client].status.snmp_conn != 3
				ShowInfo(deviceInfo[selected_client].status);
			else
				ClearInfo();
		}

		void ShowInfo(DeviceInfo.Status status)
        {
			Change_Ping_Status(status.icmp_conn);
			//Change_SNMP_Status(status.snmp_conn, selected_client);

			UI_labels[4].Visible = true;
			UI_labels[4].Text = status.info_updated_time;
			UI_labels[8].Visible = true;
			UI_labels[9].Visible = cl[selected_client].SysTime != null;
			UI_labels[15].Visible = cl[selected_client].Temperature != null;

			if (status.standart != null && status.standart.Length != 0)
				for (int i = 0; i < 4; i++) { UI_labels[i + 10].Text = status.standart[i] != null ? status.standart[i] : ""; }
			else
				for (int i = 0; i < 4; i++) { UI_labels[i + 10].Text = ""; }

			if(status.interface_count != 0)
				Fill_grid(status.interface_table, status.ifnames, status.interface_count);
			else
				dataGridView1.Rows.Clear();

			UI_labels[14].Text = status.SysTime != null ? status.SysTime : "";

			if (status.temperatures != null && status.temperatures.Length != 0)
				for(int i = 0; i < 3; i++) { UI_labels[i + 16].Text = status.temperatures[i] != null ? status.temperatures[i] : ""; }
			else
				for (int i = 0; i < 3; i++) { UI_labels[i + 16].Text = ""; }

			if (Add_names != null && Add_names.Length != 0)
			{
				if (cl[selected_client].Addition != null)// && status.additional.Length != 0)
				{
					for (int i = 0; i < cl[selected_client].Addition.Length / 6; i++)
					{
						Add_names[i].Text = cl[selected_client].Addition[i, 2];

						if(status.additional.Length != 0)
							Add_labels[i].Text = cl[selected_client].Addition[i, 1].Contains("state") && status.additional[i] == "1" ? "Ok" : status.additional[i];
						else
							Add_labels[i].Text = "";
					}
				}
				else
				{
					for (int i = 0; i < Add_names.Length; i++)
					{
						Add_names[i].Text = "";
						Add_labels[i].Text = "";
					}
				}
			}

			//if (!timer1.Enabled)
				//timer1.Start();
		}

		void ClearInfo()
		{
			//Change_SNMP_Status(5, selected_client);
			Change_Ping_Status(5);
			dataGridView1.Rows.Clear();

			UI_labels[4].Text = "";
			UI_labels[8].Visible = false;
			UI_labels[9].Visible = false;

			for (int i = 10; i < 15; i++) { UI_labels[i].Text = ""; }
			UI_labels[15].Visible = cl[selected_client].Temperature != null;
			for (int i = 16; i < 19; i++) { UI_labels[i].Text = ""; }

			if (Add_names != null && Add_names.Length != 0)
			{
				for (int i = 0; i < Add_names.Length; i++)
				{
					Add_names[i].Text = "";
					Add_labels[i].Text = "";
				}
			}

			//if (timer1.Enabled)
				//timer1.Stop();
		}

		void Switch_UI_visibility(bool show_UI)
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

			if (cl[selected_client].Connect)
				ShowInfo(deviceInfo[selected_client].status);
			else
				Switch_UI_visibility(false);
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
