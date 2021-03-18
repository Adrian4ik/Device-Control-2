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

        public DeviceInfo(RawDeviceList.Client client)
        {
            cl = client;
        }

        public void Init()
        {

        }

        public void Save()
        {

        }

        private void UpdateInfo()
        {
            //Survey;
        }
    }
}
