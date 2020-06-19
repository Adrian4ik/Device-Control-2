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
        string[,] lolkekcheburek = new string[63, 6];

        public Form1()
        {
            InitializeComponent();
        }

        private void Form1_Load(object sender, EventArgs e)
        {
            dataGridView1.Rows.Add(64);

            lolkekcheburek[0, 0] = "48";
            lolkekcheburek[1, 0] = "Module: 5 Port: 5 - 10/100 Mbit TX";
            lolkekcheburek[2, 0] = "LAPTOP RSS1";
            lolkekcheburek[3, 0] = "Связь есть";
            lolkekcheburek[4, 0] = "100";
            lolkekcheburek[5, 0] = "Ethernet";

            Fill_Grid();
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
    }
}
