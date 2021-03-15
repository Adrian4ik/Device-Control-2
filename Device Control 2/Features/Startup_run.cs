using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Windows.Forms;

namespace Device_Control_2.Features
{
	class Startup_run
	{
		FileInfo fi = new FileInfo(Application.ExecutablePath);
		string name;

		public bool minimized { get; set; }

		public Startup_run()
		{
			name = fi.Name;

			SetConfigs(ReadConfig());
		}

		private bool[] ReadConfig()
		{
			bool[] cfgs = new bool[2];

			string path = Application.ExecutablePath.Substring(0, Application.ExecutablePath.Length - fi.Name.Length);

			if (!File.Exists(path + "config.xml"))
			{
				string[] example = { "run from system start: yes", "run minimized: no" };

				File.WriteAllLines(path + "config.xml", example);

				cfgs[0] = true;
				cfgs[1] = false;
			}
			else
			{
				string[] config = File.ReadAllLines(path + "config.xml");

				for (int i = 0; i < config.Length; i++)
				{
					if (config[i].Length > 23 && config[i][4] == 'f')
						cfgs[0] = GetBoolFromString(config[i].Substring(config[i].IndexOf(": ") + 2));

					if (config[i].Length > 15 && config[i][4] == 'm')
						cfgs[1] = GetBoolFromString(config[i].Substring(config[i].IndexOf(": ") + 2));
				}
			}

			return cfgs;
		}

		private void SetConfigs(bool[] configs)
		{
			string Startup_folder = Environment.GetFolderPath(Environment.SpecialFolder.Startup);

			if (configs[0])
				ShortCut.Create(Application.ExecutablePath, Startup_folder + "\\" + name.Substring(0, name.Length - 4) + ".lnk", "", "");

			minimized = configs[1];
		}

		private bool GetBoolFromString(string text)
		{
			if (text == "true" || text == "1" || text == "yes" || text == "y")
				return true;
			else
				return false;
		}

		private static class ShellLink
		{
			[ComImport,
			Guid("000214F9-0000-0000-C000-000000000046"),
			InterfaceType(ComInterfaceType.InterfaceIsIUnknown)]
			internal interface IShellLinkW
			{
				[PreserveSig]
				int GetPath(
					[Out, MarshalAs(UnmanagedType.LPWStr)]
				StringBuilder pszFile,
					int cch, ref IntPtr pfd, uint fFlags);

				[PreserveSig]
				int GetIDList(out IntPtr ppidl);

				[PreserveSig]
				int SetIDList(IntPtr pidl);

				[PreserveSig]
				int GetDescription(
					[Out, MarshalAs(UnmanagedType.LPWStr)]
				StringBuilder pszName, int cch);

				[PreserveSig]
				int SetDescription(
					[MarshalAs(UnmanagedType.LPWStr)]
				string pszName);

				[PreserveSig]
				int GetWorkingDirectory(
					[Out, MarshalAs(UnmanagedType.LPWStr)]
				StringBuilder pszDir, int cch);

				[PreserveSig]
				int SetWorkingDirectory(
					[MarshalAs(UnmanagedType.LPWStr)]
				string pszDir);

				[PreserveSig]
				int GetArguments(
					[Out, MarshalAs(UnmanagedType.LPWStr)]
				StringBuilder pszArgs, int cch);

				[PreserveSig]
				int SetArguments(
					[MarshalAs(UnmanagedType.LPWStr)]
				string pszArgs);

				[PreserveSig]
				int GetHotkey(out ushort pwHotkey);

				[PreserveSig]
				int SetHotkey(ushort wHotkey);

				[PreserveSig]
				int GetShowCmd(out int piShowCmd);

				[PreserveSig]
				int SetShowCmd(int iShowCmd);

				[PreserveSig]
				int GetIconLocation(
					[Out, MarshalAs(UnmanagedType.LPWStr)]
				StringBuilder pszIconPath, int cch, out int piIcon);

				[PreserveSig]
				int SetIconLocation(
					[MarshalAs(UnmanagedType.LPWStr)]
				string pszIconPath, int iIcon);

				[PreserveSig]
				int SetRelativePath(
					[MarshalAs(UnmanagedType.LPWStr)]
				string pszPathRel, uint dwReserved);

				[PreserveSig]
				int Resolve(IntPtr hwnd, uint fFlags);

				[PreserveSig]
				int SetPath(
					[MarshalAs(UnmanagedType.LPWStr)]
				string pszFile);
			}

			[ComImport,
			Guid("00021401-0000-0000-C000-000000000046"),
			ClassInterface(ClassInterfaceType.None)]
			private class shl_link { }

			internal static IShellLinkW CreateShellLink()
			{
				return (IShellLinkW)(new shl_link());
			}
		}

		private static class ShortCut
		{
			public static void Create(
				string PathToFile, string PathToLink,
				string Arguments, string Description)
			{
				ShellLink.IShellLinkW shlLink = ShellLink.CreateShellLink();

				Marshal.ThrowExceptionForHR(shlLink.SetDescription(Description));
				Marshal.ThrowExceptionForHR(shlLink.SetPath(PathToFile));
				Marshal.ThrowExceptionForHR(shlLink.SetArguments(Arguments));

				((System.Runtime.InteropServices.ComTypes.IPersistFile)shlLink).Save(PathToLink, false);
			}
		}
	}
}
