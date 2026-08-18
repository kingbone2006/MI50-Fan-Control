using System;
using System.IO;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class InspectSpeedfanExe
    {
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadLibraryEx(string lpFileName, IntPtr hFile, uint dwFlags);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr FindResource(IntPtr hModule, string lpName, string lpType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr FindResource(IntPtr hModule, IntPtr lpName, IntPtr lpType);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LoadResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr LockResource(IntPtr hResData);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern uint SizeofResource(IntPtr hModule, IntPtr hResInfo);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FreeLibrary(IntPtr hModule);

        private const uint LOAD_LIBRARY_AS_DATAFILE = 0x00000002;

        public static void Run()
        {
            string exePath = @"C:\Users\MI50\Desktop\fancontrol\src\MI50FanControl\Engine\speedfan.exe";
            if (!File.Exists(exePath))
            {
                Console.WriteLine("File not found");
                return;
            }

            byte[] bytes = File.ReadAllBytes(exePath);
            Console.WriteLine($"Read speedfan.exe: {bytes.Length} bytes");

            // Look for embedded MZ / PE headers inside speedfan.exe (speedfan.sys is inside!)
            for (int i = 1000; i < bytes.Length - 1000; i++)
            {
                if (bytes[i] == 'M' && bytes[i + 1] == 'Z' && bytes[i + 2] == 0x90 && bytes[i + 3] == 0x00)
                {
                    Console.WriteLine($"Found embedded PE/SYS at offset 0x{i:X} ({i})");
                    // Let's check size
                    int peOffset = BitConverter.ToInt32(bytes, i + 0x3C);
                    if (peOffset > 0 && peOffset < 500 && i + peOffset + 4 < bytes.Length)
                    {
                        if (bytes[i + peOffset] == 'P' && bytes[i + peOffset + 1] == 'E' && bytes[i + peOffset + 2] == 0 && bytes[i + peOffset + 3] == 0)
                        {
                            ushort machine = BitConverter.ToUInt16(bytes, i + peOffset + 4);
                            Console.WriteLine($"  -> Valid PE! Machine: 0x{machine:X4} (0x014C=x86, 0x8664=x64)");
                        }
                    }
                }
            }
        }
    }
}
