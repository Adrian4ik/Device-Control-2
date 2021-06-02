using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net;
using System.Net.NetworkInformation;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Device_Control_2.snmp
{
	class RawDeviceList
	{
		private readonly string path;

		public struct Client
		{
			private string Folder;

			public bool Connect;
			public int id;
			public string Name;
			public IPAddress Ip;

			public string SysTime;
			public string IfName;

			public string[,] Addition;
			public string[,] Temperature;
		}

		Client[] actual_list = new Client[1];

		public RawDeviceList()
		{
			FileInfo fi = new FileInfo(Application.ExecutablePath);
			path = Application.ExecutablePath.Substring(0, Application.ExecutablePath.Length - fi.Name.Length);
		}

		public Client[] ScanDevices
		{
			get
			{
				string[] devices = ReadDeviceList();

				if (!Directory.Exists(path + "devices"))
				{
					Directory.CreateDirectory(path + "devices");

					CheckExample();
				}
				else
				{
					int j = 0;
					bool[] actuals = new bool[devices.Length];

					for (int i = 0; i < devices.Length; i++)
						if (CheckFiles(devices[i]) && CheckDevice(devices[i]))
						{
							actuals[i] = true;
							j++;
						}

					actual_list = new Client[j];
					j = 0;

					for (int i = 0; i < devices.Length; i++)
						if (actuals[i])
						{
							actual_list[j] = GetClient(devices[i]);
							actual_list[j].id = j;
							j++;
						}
				}

				return actual_list;
			}
		}

		string[] ReadDeviceList()
		{
			string[] folders = { };

			if (!File.Exists(path + "devlist.xml"))
				File.WriteAllText(path + "devlist.xml", "folder names:");
			else
			{
				string[] devices = File.ReadAllLines(path + "devlist.xml");

				if (devices.Length > 1)
				{
					for (int i = 1, j = 1; i < devices.Length; i++)
						if (devices[i].Length != 0)
							folders = new string[j++];

					for (int i = 1, j = 0; i < devices.Length; i++)
						if (devices[i].Length != 0)
							folders[j++] = devices[i];
				}
			}

			return folders;
		}

		void CheckExample()
		{
			string[] example_config = { "Имя: Localhost", "ip: 127.0.0.1", "autoconnect: true" };

			string[] example_optlist = { "Важные", "",
									 "Температура текущая, °С (temperature): 1.2.3.4.5.6.7.8.9.0",
									 "Температура максимально допустимая, °С (max temperature): 1.2.3.4.5.6.7.8.9.0",
									 "Температура минимально допустимая, °С (min temperature): 1.2.3.4.5.6.7.8.9.0",
									 "Питание #1 (power state 1): 1.2.3.4.5.6.7.8.9.0",
									 "Питание #2 (power state 2): 1.2.3.4.5.6.7.8.9.0",
									 "Питание #3 (power state 3): 1.2.3.4.5.6.7.8.9.0",
									 "Вентилятор #1 (fan speed 1): 1.2.3.4.5.6.7.8.9.0",
									 "Вентилятор #2 (fan speed 2): 1.2.3.4.5.6.7.8.9.0",
									 "Вентилятор #3 (fan speed 3): 1.2.3.4.5.6.7.8.9.0",
									 "Дополнительные", "",
									 "Дополнительный параметр #1 (parameter 1): 1.2.3.4.5.6.7.8.9.0",
									 "Дополнительный параметр #2 (parameter 2): 1.2.3.4.5.6.7.8.9.0",
									 "Дополнительный параметр #3 (parameter 3): 1.2.3.4.5.6.7.8.9.0",
									 "", "Описание устройства", "",
									 "Системное время (system time): 1.2.3.4.5.6.7.8.9.0",
									 "", "Интерфейсы", "",
									 "Имена интерфейсов (ifNames): 1.2.3.4.5.6.7.8.9.0.ifIndex" };

			if (!Directory.Exists(path + "devices\\example"))
				Directory.CreateDirectory(path + "devices\\example");

			if (!File.Exists(path + "devices\\example\\config.xml"))
				File.WriteAllLines(path + "devices\\example\\config.xml", example_config);


			if (!File.Exists(path + "devices\\example\\optlist.xml"))
				File.WriteAllLines(path + "devices\\example\\optlist.xml", example_optlist);
		}

		bool CheckFiles(string folder_name)
		{
			bool is_created = true;

			string[] std_config = { "Имя: ", "ip: ", "autoconnect: false" };

			string[] std_optlist = { "Важные", "",
									 "Температура текущая, °С (temperature): 1.",
									 "Температура максимально допустимая, °С (max temperature): 1.",
									 "Температура минимально допустимая, °С (min temperature): 1.",
									 "Питание (power state): 1.",
									 "Вентилятор (fan speed): 1.",
									 "", "Описание устройства", "",
									 "Системное время (system time): 1.",
									 "", "Интерфейсы", "",
									 "Имена интерфейсов (ifNames): 1...ifIndex" };

			if (!Directory.Exists(path + "devices\\" + folder_name))
			{
				Directory.CreateDirectory(path + "devices\\" + folder_name);
				is_created = false;
			}

			if (!File.Exists(path + "devices\\" + folder_name + "\\config.xml"))
			{
				File.WriteAllLines(path + "devices\\" + folder_name + "\\config.xml", std_config);
				is_created = false;
			}

			if (!File.Exists(path + "devices\\" + folder_name + "\\optlist.xml"))
			{
				File.WriteAllLines(path + "devices\\" + folder_name + "\\optlist.xml", std_optlist);
				is_created = false;
			}

			return is_created;
		}

		bool CheckDevice(string folder_name)
		{
			bool accept = true;
			string[] config = ConfigParse(folder_name);
			string[,] optlist = OptlistParse(folder_name);

			if (config[0] == "")
				accept = false;

			try { IPAddress ip = IPAddress.Parse(config[1]); }
			catch { accept = false; }

			return accept;
		}

		Client GetClient(string folder_name)
		{
			Client client = new Client();

			string[] config = ConfigParse(folder_name);
			string[,] optlist = OptlistParse(folder_name);

			client.Name = config[0];
			client.Ip = IPAddress.Parse(config[1]);
			client.Connect = GetBoolFromString(config[2]);

			if (optlist.Length > 0)
			{
				int mod = int.Parse(optlist[0, 1]),
					add = int.Parse(optlist[0, 2]),
				   time = int.Parse(optlist[0, 3]),
				ifnames = int.Parse(optlist[0, 4]);

				client.SysTime = optlist[0, 0];
				client.IfName = optlist[1, 0];

				client.Temperature = new string[mod, 3];
				client.Addition = new string[add, 6];
				// [2, 0, 2] т.е. критичность, минимальное значение для выдачи уведомления нештатки, максимальное значение для выдачи уведомления нештатки
				// [1, 1000, 1500] данный пункт необходимо прописывать для всех значений в 3й вкладке кроме температуры, так как она записывается отдельно
				// [0] критичность имеет значения 0-2, 2 - высокая, вырубает закрытие уведомлялки, 1 - низкая, выдаёт стандартные уведомления, 0 - не выдаёт никакую информацию

				for (int i = 0; i < mod; i++)
				{
					client.Temperature[i, 0] = optlist[i + 2, 0];
					client.Temperature[i, 1] = optlist[i + 2, 1];
					client.Temperature[i, 2] = optlist[i + 2, 2];
				}

				for (int i = 0, j = mod + time + ifnames; i < add; i++)
					for (int k = 0; k < 6; k++)
						client.Addition[i, k] = optlist[i + j, k];
			}

			return client;
		}

		string[] ConfigParse(string folder_name)
		{
			string[] parsed_config = new string[3];

			string[] config = File.ReadAllLines(path + "devices\\" + folder_name + "\\config.xml");

			for(int i = 0; i < config.Length; i++)
			{
				if (config[i].Length > 4 && config[i].Substring(0, 4) == "ip: ")
					parsed_config[1] = config[i].Substring(4);
				else if (config[i].Length > 5 && config[i].Substring(0, 5) == "Имя: ")
					parsed_config[0] = config[i].Substring(5);
				else if (config[i].Length > 13 && config[i].Substring(0, 13) == "autoconnect: ")
					parsed_config[2] = config[i].Substring(13);
			}

			return parsed_config;
		}

		string[,] OptlistParse(string folder_name)
		{
			int mod = 0, add = 0, time = 0, ifnames = 0;
			string[] optlist = File.ReadAllLines(path + "devices\\" + folder_name + "\\optlist.xml");

			string[,] parsed_optlist = new string[optlist.Length, 6];

			for (int i = 0, j = 5; i < optlist.Length; i++)
			{
				if (optlist[i].Length > 25 && optlist[i].Substring(optlist[i].IndexOf("1.")) != "1." && optlist[i].Substring(optlist[i].IndexOf("1.")) != "1...ifIndex")
				{
					if (optlist[i].Substring(optlist[i].IndexOf('('), 13) == "(system time)")
					{
						parsed_optlist[0, 0] = optlist[i].Substring(optlist[i].IndexOf("1."));
						time++;
					}
					else if (optlist[i].Substring(optlist[i].IndexOf('('), 9) == "(ifNames)")
					{
						parsed_optlist[1, 0] = optlist[i].Substring(optlist[i].IndexOf("1."), optlist[i].IndexOf("ifIndex") - optlist[i].IndexOf("1."));
						ifnames++;
					}
					else if (optlist[i].Substring(optlist[i].IndexOf('('), 13) == "(temperature)")
					{
						parsed_optlist[2, 0] = optlist[i].Substring(optlist[i].IndexOf("1."));
						parsed_optlist[2, 1] = "temperature";
						parsed_optlist[2, 2] = "Температура текущая, °С";
						mod++;
					}
					else if (optlist[i].Substring(optlist[i].IndexOf('('), 17) == "(max temperature)")
					{
						parsed_optlist[3, 0] = optlist[i].Substring(optlist[i].IndexOf("1."));
						parsed_optlist[3, 1] = "max temperature";
						parsed_optlist[3, 2] = "Температура максимально\r\nдопустимая, °С";
						mod++;
					}
					else if (optlist[i].Substring(optlist[i].IndexOf('('), 17) == "(min temperature)")
					{
						parsed_optlist[4, 0] = optlist[i].Substring(optlist[i].IndexOf("1."));
						parsed_optlist[4, 1] = "min temperature";
						parsed_optlist[4, 2] = "Температура минимально\r\nдопустимая, °С";
						mod++;
					}
					else
					{
						parsed_optlist[j, 0] = optlist[i].Substring(optlist[i].IndexOf("1."));
						parsed_optlist[j, 1] = optlist[i].Substring(optlist[i].IndexOf('(') + 1, optlist[i].IndexOf(')') - optlist[i].IndexOf('(') - 1);
						parsed_optlist[j, 2] = optlist[i].Substring(0, optlist[i].IndexOf('(') - 1);

						string[] configs = GetAddData(optlist[i].Substring(optlist[i].IndexOf('[') + 1, optlist[i].IndexOf(']') - optlist[i].IndexOf('[') - 1));

						parsed_optlist[j, 3] = configs[0];
						parsed_optlist[j, 4] = configs[1];
						parsed_optlist[j, 5] = configs[2];
						add++;
						j++;
					}
					/*else if (optlist[i].Substring(optlist[i].IndexOf('('), 12) == "(power state")
					{
						parsed_optlist[j, 0] = optlist[i].Substring(optlist[i].IndexOf("1."));
						parsed_optlist[j, 1] = optlist[i].Substring(optlist[i].IndexOf('(') + 1, optlist[i].IndexOf(')') - optlist[i].IndexOf('(') - 1);
						parsed_optlist[j, 2] = optlist[i].Substring(0, optlist[i].IndexOf('(') - 1);
						add++;
						j++;
					}
					else if (optlist[i].Substring(optlist[i].IndexOf('('), 10) == "(fan speed")
					{
						parsed_optlist[j, 0] = optlist[i].Substring(optlist[i].IndexOf("1."));
						parsed_optlist[j, 1] = optlist[i].Substring(optlist[i].IndexOf('(') + 1, optlist[i].IndexOf(')') - optlist[i].IndexOf('(') - 1);
						parsed_optlist[j, 2] = optlist[i].Substring(0, optlist[i].IndexOf('(') - 1);
						add++;
						j++;
					}*/

					parsed_optlist[0, 1] = mod.ToString();
					parsed_optlist[0, 2] = add.ToString();
					parsed_optlist[0, 3] = time.ToString();
					parsed_optlist[0, 4] = ifnames.ToString();
				}
			}

			string[,] res_optlist = new string[mod + add + time + ifnames, 6];

			for(int i = 0; i < res_optlist.Length / 6; i++)
				for(int k = 0; k < 6; k++)
					res_optlist[i, k] = parsed_optlist[i, k];

			return res_optlist;
		}

		string[] GetAddData(string text)
        {
			string[] configs = new string[3];

			for (int i = 0, j = 0; i < text.Length; i++)
            {
				if (text[i] == ',')
					j++;
				else if (text[i] != ' ')
					configs[j] += text[i];
            }

			return configs;
        }

		bool GetBoolFromString(string text)
		{
			if (text == "true" || text == "1" || text == "yes" || text == "y")
				return true;
			else
				return false;
		}
	}
}
