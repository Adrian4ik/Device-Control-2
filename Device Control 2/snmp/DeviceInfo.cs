﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Forms;

namespace Device_Control_2.snmp
{
    class DeviceInfo
    {
        RawDeviceList.Client cl;

        public struct Status
        {
            int[] interface_list;

            public string info_updated_time;
            public string SysTime;

            public string[] temperatures;

            public string[,] additional;
            public string[,] interface_table;
        }

        public Status status = new Status();

        Timer timer = new Timer();

        public DeviceInfo(RawDeviceList.Client client)
        {
            cl = client;

            if (cl.Connect)
            {
                Inspect();
                timer.Interval = 6000;
                timer.Tick += new EventHandler(InspectTimer);
            }
        }

        public void Init()
        {

        }

        public void Save()
        {

        }

        private void Inspect()
        {
            // Первый раз опрашивает устройство и записывает его таблицу интерфейсов для исключения в дальнейшем пустых опросов
        }

        private void UpdateInfo()
        {
            //Survey;

            string time = (DateTime.Now.Hour < 10) ? "0" + DateTime.Now.Hour + ":" : DateTime.Now.Hour + ":";
            time += (DateTime.Now.Minute < 10) ? "0" + DateTime.Now.Minute : DateTime.Now.Minute.ToString();
            //info_updated_time = "Последний раз обновлено: " + time;
        }

        private void InspectTimer(object sender, EventArgs e)
        {
            Inspect();
        }

        private void TimerTick(object sender, EventArgs e)
        {
            UpdateInfo();
        }
    }
}
