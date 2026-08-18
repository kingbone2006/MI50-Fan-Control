using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Threading;

namespace FanDiag
{
    public class TestPortableEngineLaunch
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        private const uint FILE_MAP_READ = 0x0004;

        public static void Run()
        {
            string exe = @"C:\Users\MI50\Desktop\fancontrol\src\MI50FanControl\Engine\speedfan.exe";
            Console.WriteLine($"Starting: {exe}");

            var psi = new ProcessStartInfo
            {
                FileName = exe,
                Arguments = "/NOSMARTSCAN /NOSMBSCAN /NOPCISCAN /MINIMIZED",
                WorkingDirectory = Path.GetDirectoryName(exe),
                UseShellExecute = true,
                Verb = "runas",
                WindowStyle = ProcessWindowStyle.Minimized
            };

            try
            {
                var p = Process.Start(psi);
                Console.WriteLine($"Process started! PID = {p?.Id}");

                for (int i = 0; i < 15; i++)
                {
                    Thread.Sleep(500);
                    IntPtr hMap = OpenFileMapping(FILE_MAP_READ, false, "SFSharedMemory_ALM");
                    Console.WriteLine($"[{i * 0.5:F1}s] OpenFileMapping = 0x{hMap.ToInt64():X}");
                    if (hMap != IntPtr.Zero)
                    {
                        Console.WriteLine("SUCCESSFULLY CONNECTED TO PORTABLE SPEEDFAN MEMORY!");
                        break;
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Exception: {ex.Message}");
            }
        }
    }
}
