using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using SnmpSharpNet;

namespace Device_Control_2
{
    class Survey
    {
        Pdu std = new Pdu(PduType.Get);

		private void FillConstants()
		{
			std.VbList.Add("1.3.6.1.2.1.1.1.0"); // sysDescr
			std.VbList.Add("1.3.6.1.2.1.1.3.0"); // sysUpTime
			std.VbList.Add("1.3.6.1.2.1.1.5.0"); // sysName
			std.VbList.Add("1.3.6.1.2.1.1.6.0"); // sysLocation
			std.VbList.Add("1.3.6.1.2.1.2.1.0"); // ifNumber
		}
	}
}
