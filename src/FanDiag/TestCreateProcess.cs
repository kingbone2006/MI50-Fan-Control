using System;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace FanDiag
{
    public class TestCreateProcess
    {
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        public struct STARTUPINFO
        {
            public int cb;
            public string lpReserved;
            public string lpDesktop;
            public string lpTitle;
            public int dwX;
            public int dwY;
            public int dwXSize;
            public int dwYSize;
            public int dwXCountChars;
            public int dwYCountChars;
            public int dwFillAttribute;
            public int dwFlags;
            public short wShowWindow;
            public short cbReserved2;
            public IntPtr lpReserved2;
            public IntPtr hStdInput;
            public IntPtr hStdOutput;
            public IntPtr hStdError;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct PROCESS_INFORMATION
        {
            public IntPtr hProcess;
            public IntPtr hThread;
            public int dwProcessId;
            public int dwThreadId;
        }

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        public static extern bool CreateProcess(
            string lpApplicationName,
            string lpCommandLine,
            IntPtr lpProcessAttributes,
            IntPtr lpThreadAttributes,
            bool bInheritHandles,
            uint dwCreationFlags,
            IntPtr lpEnvironment,
            string lpCurrentDirectory,
            ref STARTUPINFO lpStartupInfo,
            out PROCESS_INFORMATION lpProcessInformation);

        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        private const uint CREATE_NO_WINDOW = 0x08000000;
        private const int STARTF_USESHOWWINDOW = 0x00000001;
        private const short SW_HIDE = 0;
        private const uint FILE_MAP_READ = 0x0004;

        public static void Run()
        {
            string exe = @"C:\Users\MI50\Desktop\fancontrol\src\MI50FanControl\Engine\speedfan.exe";
            string cmd = $"\"{exe}\" /NOSMARTSCAN /NOSMBSCAN /NOPCISCAN /MINIMIZED";
            string dir = Path.GetDirectoryName(exe)!;

            var si = new STARTUPINFO();
            si.cb = Marshal.SizeOf(si);
            si.dwFlags = STARTF_USESHOWWINDOW;
            si.wShowWindow = SW_HIDE;

            bool success = CreateProcess(null!, cmd, IntPtr.Zero, IntPtr.Zero, false, 0, IntPtr.Zero, dir, ref si, out PROCESS_INFORMATION pi);
            Console.WriteLine($"CreateProcess success = {success}, PID = {pi.dwProcessId}, Error = {Marshal.GetLastWin32Error()}");

            for (int i = 0; i < 15; i++)
            {
                Thread.Sleep(500);
                IntPtr hMap = OpenFileMapping(FILE_MAP_READ, false, "SFSharedMemory_ALM");
                Console.WriteLine($"[{i * 0.5:F1}s] OpenFileMapping = 0x{hMap.ToInt64():X}");
                if (hMap != IntPtr.Zero)
                {
                    Console.WriteLine("SUCCESS: CONNECTED TO PORTABLE SPEEDFAN VIA CREATEPROCESS!");
                    break;
                }
            }
        }
    }
}
