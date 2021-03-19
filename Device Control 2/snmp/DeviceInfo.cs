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
        }
    }
}
