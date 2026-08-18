using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class InspectSfExtra
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibrary(string lpFileName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr GetProcAddress(IntPtr hModule, string lpProcName);

        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" INSPECT SFEXTRA.DLL EXPORTS");
            Console.WriteLine("==================================================");

            string dllPath = @"C:\Program Files (x86)\SpeedFan\sfextra.dll";
            if (!File.Exists(dllPath))
            {
                Console.WriteLine("sfextra.dll not found");
                return;
            }

            IntPtr hMod = LoadLibrary(dllPath);
            if (hMod == IntPtr.Zero)
            {
                Console.WriteLine($"Could not load sfextra.dll: {Marshal.GetLastWin32Error()}");
                return;
            }

            Console.WriteLine("Successfully loaded sfextra.dll!");

            // Test known exports
            string[] testExports = { "ReadFan", "SetFan", "SetPwm", "GetFanSpeed", "Init", "Open", "Close", "ReadPort", "WritePort", "GetFans", "GetTemps" };
            foreach (var exp in testExports)
            {
                IntPtr proc = GetProcAddress(hMod, exp);
                if (proc != IntPtr.Zero)
                {
                    Console.WriteLine($"  Found Export: {exp} at 0x{proc.ToInt64():X}");
                }
            }
        }
    }
}
