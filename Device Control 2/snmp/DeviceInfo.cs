using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Device_Control_2.snmp
{
    class DeviceInfo
    {
        RawDeviceList.Client cl;

        public struct Status
        {
            public string info_updated_time;
        }

        public DeviceInfo(RawDeviceList.Client client)
        {
            cl = client;

            if (cl.Connect)
            {
                Inspect();
            }
        }

        public void Init()
        {

        }

        public void Save()
        {

        }

        public Status GetStatus()
        {
            return new Status();
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
    }
}
