using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;

using Device_Control_2.Features;

namespace Device_Control_2
{
	public partial class Notification : Form
	{
		bool[] states;
		string[] devlist;

		GroupBox[] gb;
		PictureBox[] pbx;
		Label[] ttl;
		Label[] txt;

		Task th;

		Display display = new Display();

		Form1.Notification_message[] msg;

		private struct Notification_message
		{
			public bool State { get; set; }
			public bool Criticality { get; set; }
			public string Time { get; set; }
			public string[] Text { get; set; }
		}

		private Notification_message[] note;

		public Notification(string[] device_list)
		{
			InitializeComponent();

			devlist = device_list;

			states = new bool[device_list.Length * 4];

			for (int i = 0; i < device_list.Length; i++)
				states[i] = false;

			//th = new Task(Update);
		}

		public void InitMessages(int count)
        {
			note = new Notification_message[count];

			for(int i = 0; i < count; i++)
            {
				note[i].State = false;
				note[i].Criticality = true;
				note[i].Time = "Ситуация возникла: ";
				
				note[i].Text = new string[4];
				note[i].Text[0] = "Прервана связь с устройством ";
				note[i].Text[1] = "Нештатное состояние системы питания устройства ";
				note[i].Text[2] = "Нештатное значение температуры устройства ";
				note[i].Text[3] = "Нештатное состояние вентилятора устройства ";
			}
        }

		private void InitControls(int count)
		{
			if (gb != null)
				for(int i = 0; i < gb.Length; i++)
				{
					gb[i].Dispose();
					pbx[i].Dispose();
					ttl[i].Dispose();
					txt[i].Dispose();
				}

			gb = new GroupBox[count];
			pbx = new PictureBox[count];
			ttl = new Label[count];
			txt = new Label[count];

			for (int i = 0; i < count; i++)
			{
				pbx[i] = new PictureBox();
				pbx[i].Location = new Point(6, 12);
				pbx[i].Name = "pb" + i.ToString();
				pbx[i].Size = new Size(32, 32);
				pbx[i].TabIndex = 10 + i;
				pbx[i].SizeMode = PictureBoxSizeMode.Zoom;

				ttl[i] = new Label();
				ttl[i].Location = new Point(44, 12);
				ttl[i].Name = "lb" + i.ToString();
				ttl[i].Size = new Size(300, 16);
				ttl[i].AutoSize = true;
				//ttl[i].BorderStyle = BorderStyle.FixedSingle;
				ttl[i].TabIndex = 20 + i;
				ttl[i].Text = "";
				ttl[i].Font = new Font("Microsoft Sans Serif", 9.75F, FontStyle.Bold, GraphicsUnit.Point, 204);

				txt[i] = new Label();
				txt[i].Location = new Point(44, 31);
				txt[i].Name = "tx" + i.ToString();
				txt[i].Size = new Size(300, 13);
				txt[i].TabIndex = 30 + i;
				txt[i].Text = "";

				gb[i] = new GroupBox();
				gb[i].Location = new Point(0, 0 + i * 50);
				gb[i].Name = "gb" + i.ToString();
				gb[i].Size = new Size(490, 50);
				gb[i].TabIndex = i;
				gb[i].Text = "";
				gb[i].Controls.Add(pbx[i]);
				gb[i].Controls.Add(ttl[i]);
				gb[i].Controls.Add(txt[i]);
				Controls.Add(gb[i]);
			}
		}

		public void Update_list(Form1.Notification_message[] message) // Form1.message info)
		{
			msg = message;

			Update_form();
			//Task.Factory.StartNew(() => { Update(); }).ContinueWith(t => { Show_or_Hide(); });
		}

		private void Update_form()
		{
			int states_count = 0;

			for (int i = 0; i < msg.Count(); i++)
				if(msg[i].State)
					states_count++;

			int[] notifies = new int[states_count];
			states_count = 0;

			for (int i = 0; i < msg.Count(); i++)
				if (msg[i].State)
					notifies[states_count++] = i;

			InitControls(notifies.Count());

			for (int i = 0; i < notifies.Count(); i++)
			{
				switch (msg[notifies[i]].Criticality)
				{
					case 0:
						pbx[i].Image = Properties.Resources.info32;
						break;
					case 1:
						pbx[i].Image = Properties.Resources.error32;
						break;
					case 2:
						pbx[i].Image = Properties.Resources.stop32;
						break;
				}

				ttl[i].Text = msg[notifies[i]].Text;
				txt[i].Text = "Ситуация возникла: " + msg[notifies[i]].Time;
			}

			Show_or_Hide(notifies.Count());

			/*if (states_count > 0)
            {
				Console.Beep(2000, 1000);

				display.On();

				Show();
			}
			else
			{
				if (gb != null)
					for (int i = 0; i < gb.Length; i++)
					{
						pbx[i].Dispose();
						ttl[i].Dispose();
						txt[i].Dispose();
						gb[i].Dispose();
					}

				Hide();
			}*/

			//th.Interrupt();
		}

		void Show_or_Hide(int count)
		{
			if (count > 0)
			{
				Console.Beep(2000, 1000);

				display.On();

				Show();
			}
			else
			{
				if (gb != null)
					for (int i = 0; i < gb.Length; i++)
					{
						pbx[i].Dispose();
						ttl[i].Dispose();
						txt[i].Dispose();
						gb[i].Dispose();
					}

				Hide();
			}
		}
	}
}
