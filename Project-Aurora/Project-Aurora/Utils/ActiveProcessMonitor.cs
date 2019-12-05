using System;
using System.IO;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Threading;

namespace Aurora.Utils
{
	public struct tagLASTINPUTINFO
	{
		public uint cbSize;
		public Int32 dwTime;
	}

	public sealed class ActiveProcessMonitor
	{
		private const uint WINEVENT_OUTOFCONTEXT = 0;
		private const uint EVENT_SYSTEM_FOREGROUND = 3;
		private const uint EVENT_SYSTEM_MINIMIZESTART = 0x0016;
		private const uint EVENT_SYSTEM_MINIMIZEEND = 0x0017;
        private const string SandBoxAppName = "ApplicationFrameHost";
        delegate void WinEventDelegate(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime);
		private string processPath = string.Empty;
		public string ProcessPath { get { return processPath; } private set { processPath = value; ActiveProcessChanged?.Invoke(this, null); } }
		public event EventHandler ActiveProcessChanged;

		static WinEventDelegate dele;

		public ActiveProcessMonitor()
		{
			try
			{
				dele = new WinEventDelegate(WinEventProc);
				SetWinEventHook(EVENT_SYSTEM_FOREGROUND, EVENT_SYSTEM_FOREGROUND, IntPtr.Zero, dele, 0, 0, WINEVENT_OUTOFCONTEXT);
				SetWinEventHook(EVENT_SYSTEM_MINIMIZESTART, EVENT_SYSTEM_MINIMIZEEND, IntPtr.Zero, dele, 0, 0, WINEVENT_OUTOFCONTEXT);
			}
			catch (Exception exc)
			{

			}
		}

		public void WinEventProc(IntPtr hWinEventHook, uint eventType, IntPtr hwnd, int idObject, int idChild, uint dwEventThread, uint dwmsEventTime)
		{
			if (Global.Configuration.detection_mode == Settings.ApplicationDetectionMode.WindowsEvents)
			{
				GetActiveWindowsProcessname();
			}
		}

		[DllImport("user32.dll")]
		public static extern IntPtr GetForegroundWindow();

		[DllImport("user32.dll")]
		static extern IntPtr SetWinEventHook(uint eventMin, uint eventMax, IntPtr hmodWinEventProc, WinEventDelegate lpfnWinEventProc, uint idProcess, uint idThread, uint dwFlags);

		// TODO: Move this to own util
		[DllImport("user32.dll")]
		public static extern Boolean GetLastInputInfo(ref tagLASTINPUTINFO plii);

		[DllImport("user32.dll", SetLastError = true)]
		static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        static extern int GetWindowText(IntPtr hWnd, StringBuilder text, int count);

		[DllImport("Oleacc.dll")]
		static extern IntPtr GetProcessHandleFromHwnd(IntPtr whandle);

		[DllImport("psapi.dll", CallingConvention = CallingConvention.StdCall, CharSet = CharSet.Unicode)]
		static extern uint GetModuleFileNameExW(IntPtr hProcess, IntPtr hModule, [Out] StringBuilder lpBaseName, [In] [MarshalAs(UnmanagedType.U4)] int nSize);

		public void GetActiveWindowsProcessname()
		{
			string active_process = getActiveWindowsProcessname();

			if (!String.IsNullOrWhiteSpace(active_process))
				ProcessPath = active_process;
		}

		[Flags]
		private enum ProcessAccessFlags : uint
		{
			All = 0x001F0FFF,
			Terminate = 0x00000001,
			CreateThread = 0x00000002,
			VirtualMemoryOperation = 0x00000008,
			VirtualMemoryRead = 0x00000010,
			DuplicateHandle = 0x00000040,
			CreateProcess = 0x000000080,
			SetQuota = 0x00000100,
			SetInformation = 0x00000200,
			QueryInformation = 0x00000400,
			QueryLimitedInformation = 0x00001000,
			Synchronize = 0x00100000
		}

		[DllImport("kernel32.dll")]
		private static extern bool QueryFullProcessImageName(IntPtr hprocess, int dwFlags,
			StringBuilder lpExeName, out int size);
		[DllImport("kernel32.dll")]
		private static extern IntPtr OpenProcess(ProcessAccessFlags dwDesiredAccess,
			bool bInheritHandle, int dwProcessId);

		[DllImport("kernel32.dll", SetLastError = true)]
		private static extern bool CloseHandle(IntPtr hHandle);

        [DllImport("user32.dll", CharSet = CharSet.Auto, SetLastError = true)]
        static extern int GetWindowTextLength(IntPtr hWnd);

        [DllImport("user32.dll", SetLastError = false)]
        static extern IntPtr GetDesktopWindow();

        [DllImport("psapi.dll")]
        private static extern uint GetProcessImageFileName(IntPtr hProcess, [Out] StringBuilder lpImageFileName, [In] [MarshalAs(UnmanagedType.U4)] int nSize);

        [DllImport("Kernel32.dll", CharSet = CharSet.Unicode)]
        private static extern uint QueryDosDevice([In] string lpDeviceName, [Out] StringBuilder lpTargetPath, [In] int ucchMax);

        [DllImport("user32.dll")]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool EnumChildWindows(IntPtr hwnd, WindowEnumProc callback, IntPtr lParam);

        private delegate bool WindowEnumProc(IntPtr hwnd, IntPtr lparam);

        private static WindowEnumProc _winEnumCallback = null;

        private static WindowEnumProc WinEnumCallback
        {
            get
            {
                if (_winEnumCallback == null)
                {
                    _winEnumCallback = new WindowEnumProc(ChildWindowCallback);
                }

                return _winEnumCallback;
            }
        }

        static private bool ChildWindowCallback(IntPtr hWnd, IntPtr lparam)
        {
            GCHandle gcChildhandlesList = GCHandle.FromIntPtr(lparam);

            if (gcChildhandlesList == null || gcChildhandlesList.Target == null)
            {
                return false;
            }

            List<IntPtr> childHandles = gcChildhandlesList.Target as List<IntPtr>;
            childHandles.Add(hWnd);

            return true;
        }

        public static TimeSpan GetTimeSinceLastInput() {
            var inf = new tagLASTINPUTINFO { cbSize = (uint)Marshal.SizeOf<tagLASTINPUTINFO>() };
            if (!GetLastInputInfo(ref inf)) return new TimeSpan(0);
            return new TimeSpan(0, 0, 0, 0, Environment.TickCount - (int)inf.dwTime);
        }

        private static string GetExecutablePath(Process Process)
		{
			//If running on Vista or later use the new function
			if (Environment.OSVersion.Version.Major >= 6)
			{
				return GetExecutablePathAboveVista(Process.Id);
			}

			return Process.MainModule.FileName;
		}

		private static string GetExecutablePathAboveVista(int ProcessId)
		{
			var buffer = new StringBuilder(1024);
			IntPtr hprocess = OpenProcess(ProcessAccessFlags.QueryLimitedInformation,
				false, ProcessId);
			if (hprocess != IntPtr.Zero)
			{
                try
                {
                    uint res = GetProcessImageFileName(hprocess, buffer, buffer.Capacity + 1);
                    if (res != 0)
                    {
                        string pattern = @"^.*HarddiskVolume\d+";
                        Regex r = new Regex(pattern, RegexOptions.IgnoreCase);

                        Match m = r.Match(buffer.ToString());
                        if (m.Success)
                        {
                            return r.Replace(buffer.ToString(), GetLogicalDiskLetter(m.Value));
                        }
                    }
                }
                finally
                {
                    CloseHandle(hprocess);
                }
            }

			throw new Win32Exception(Marshal.GetLastWin32Error());
        }

		private string getActiveWindowsProcessname()
		{
			IntPtr windowHandle = IntPtr.Zero;

			try
			{
				if (windowHandle.Equals(IntPtr.Zero))
					windowHandle = GetForegroundWindow();
				uint pid;
				if ((pid = GetProcessIdByWnd(windowHandle)) > 0)
				{
					Process proc = Process.GetProcessById((int)pid);
                    Process realProc = GetRealProcess(proc, windowHandle);
                    if (realProc == null) return string.Empty;

                    string path = GetExecutablePath(realProc);
                    if (!System.IO.File.Exists(path))
                        throw new Exception($"Found file path does not exist! '{path}'");
                    return path;
                }
			}
			catch (Exception exc)
			{
				Global.logger.Error("Exception in GetActiveWindowsProcessname" + exc);
			}

			return string.Empty;
		}

        static private string GetLogicalDiskLetter(string device)
        {
            var drvInfos = DriveInfo.GetDrives();

            foreach (var d in drvInfos)
            {
                var devicePathBuilder = new StringBuilder(128);
                string letter = d.Name.Substring(0, 2);
                var res = QueryDosDevice(letter, devicePathBuilder, devicePathBuilder.Capacity);
                if (res == 0)
                {
                    continue;
                }

                if (devicePathBuilder.ToString() == device)
                {
                    return letter;
                }
            }

            return string.Empty;
        }

        static private uint GetProcessIdByWnd(IntPtr hWnd)
        {
            uint pid = 0;
            GetWindowThreadProcessId(hWnd, out pid);

            return pid;
        }

        static private Process GetRealProcess(Process fgProcess, IntPtr fgWinHandle)
        {
            if (fgProcess.ProcessName != SandBoxAppName)
            {
                return fgProcess;
            }

            List<IntPtr> childHandles = new List<IntPtr>();

            GCHandle gcChildhandlesList = GCHandle.Alloc(childHandles);
            IntPtr pointerChildHandlesList = GCHandle.ToIntPtr(gcChildhandlesList);
            Process retProcess = null;
            var fgWinTitle = GetWindowTitle(fgWinHandle);
            try
            {
                int i = 0;
                do
                {
                    EnumChildWindows(fgWinHandle, WinEnumCallback, pointerChildHandlesList);
                    List<IntPtr> handles = gcChildhandlesList.Target as List<IntPtr>;
                    foreach (var hWnd in handles)
                    {
                        var process = Process.GetProcessById((int)GetProcessIdByWnd(hWnd));
                        if (process.Id != fgProcess.Id && fgWinTitle == GetWindowTitle(hWnd))
                        {
                            retProcess = process;
                        }
                    }
                    if (retProcess != null)
                    {
                        break;
                    }
                    Thread.Sleep(100);
                    handles.Clear();
                } while (i++ < 10);
            }
            finally
            {
                gcChildhandlesList.Free();
            }

            if (retProcess == null)
            {
                IntPtr otherHandle = GetMatchingId(fgWinTitle, fgProcess.MainWindowHandle);
                if (otherHandle != IntPtr.Zero)
                {
                    return Process.GetProcessById((int)GetProcessIdByWnd(otherHandle));
                }
            }

            return retProcess;
        }

        private static string GetWindowTitle(IntPtr hwnd)
        {
            int intLength = GetWindowTextLength(hwnd) + 1;
            StringBuilder stringBuilder = new StringBuilder(intLength);
            if (GetWindowText(hwnd, stringBuilder, intLength) > 0)
            {
                return stringBuilder.ToString();
            }
            return string.Empty;
        }

        private static IntPtr GetMatchingId(string nameToMatch, IntPtr ignorehWnd)
        {
            IntPtr deskTop = GetDesktopWindow();
            List<IntPtr> childHandles = new List<IntPtr>();

            GCHandle gcChildhandlesList = GCHandle.Alloc(childHandles);
            IntPtr pointerChildHandlesList = GCHandle.ToIntPtr(gcChildhandlesList);
            try
            {
                int i = 0;
                do
                {
                    EnumChildWindows(deskTop, WinEnumCallback, pointerChildHandlesList);
                    List<IntPtr> handles = gcChildhandlesList.Target as List<IntPtr>;
                    foreach (var hWnd in handles)
                    {
                        if (ignorehWnd != hWnd && GetWindowTitle(hWnd) == nameToMatch)
                        {
                            return hWnd;
                        }
                    }
                    Thread.Sleep(100);
                    handles.Clear();
                } while (i++ < 10);
            }
            finally
            {
                gcChildhandlesList.Free();
            }
            return IntPtr.Zero;
        }

        public string GetActiveWindowsProcessTitle() {
            try {
                // Based on https://stackoverflow.com/a/115905
                return GetWindowTitle(GetForegroundWindow());
            } catch (Exception exc) {
                Global.logger.Error("Exception in GetActiveWindowsProcessTitle" + exc);
            }

            return string.Empty;
        }

    }
}
