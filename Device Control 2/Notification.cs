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
        public Notification(int notification_id, int status, string text, string client, string time)
        {
            InitializeComponent();
        }
    }
}
