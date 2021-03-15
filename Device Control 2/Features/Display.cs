using System;
using System.Runtime.InteropServices;

namespace Device_Control_2.Features
{
    class Display
	{
		private const uint WM_SYSCOMMAND = 0x0112;

		private const int SC_DISPLAYPOWER = 0xF170;
		private const int HWND_BROADCAST = 0xFFFF;
		private const int DISPLAY_ON = -1;
		private const int DISPLAY_OFF = 2;
		private const int DISPLAY_STANDBY = 1;

		[DllImport("user32.dll")]
		static extern IntPtr SendMessage(IntPtr hWnd, UInt32 Msg, IntPtr wParam, IntPtr IParam);

		public void On()
        {
			SendMessage((IntPtr)HWND_BROADCAST, WM_SYSCOMMAND, (IntPtr)SC_DISPLAYPOWER, (IntPtr)DISPLAY_ON);
		}

		public void Off()
		{
			SendMessage((IntPtr)HWND_BROADCAST, WM_SYSCOMMAND, (IntPtr)SC_DISPLAYPOWER, (IntPtr)DISPLAY_OFF);
		}
	}
}
