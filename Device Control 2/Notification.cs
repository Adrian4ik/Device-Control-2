using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Runtime.Remoting.Messaging;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Device_Control_2
{
    public partial class Notification : Form
    {
        public bool State { get; set; }

        public Notification()
        {
            State = false;

            InitializeComponent();
        }

        public void Update_list(Form1.Notification_message[] message)
        {
            for(int i = 0; i < message.Count(); i++)
            {
                if(message[i].State)
                {
                    switch(message[i].Criticality)
                    {
                        case 0:
                            pictureBox1.Image = Properties.Resources.info32;
                            break;
                        case 1:
                            pictureBox1.Image = Properties.Resources.error32;
                            break;
                        case 2:
                            pictureBox1.Image = Properties.Resources.stop32;
                            break;
                    }

                    label1.Text = message[i].Text;
                    label2.Text = "Ситуация возникла: " + message[i].Time;
                }
            }
        }
    }
}
