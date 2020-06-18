using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Drawing;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Device_Control_2
{
    public partial class Form1 : Form
    {
        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            dataGridView1.Rows.Add(63);
            dataGridView1[0, 0].Value = "48";
            dataGridView1[1, 0].Value = "Module: 5 Port: 5 - 10/100 Mbit TX";
            dataGridView1[2, 0].Value = "LAPTOP RSS1";
            dataGridView1[3, 0].Value = "Связь есть";
            dataGridView1[4, 0].Value = "100";
            dataGridView1[5, 0].Value = "Ethernet";
        }
    }
}
