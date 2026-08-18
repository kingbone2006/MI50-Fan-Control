using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class TestOpenFileMapping
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        private const uint FILE_MAP_READ = 0x0004;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SpeedFanSharedMem
        {
            public ushort version;
            public ushort flags;
            public int MemSize;
            public int handle;
            public ushort NumTemps;
            public ushort NumFans;
            public ushort NumVolts;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] temps;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] fans;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] volts;
        }

        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" WIN32 OPEN FILE MAPPING FOR SPEEDFAN");
            Console.WriteLine("==================================================");

            string[] names = { "SFSharedMemory_ALM", @"Global\SFSharedMemory_ALM", @"Local\SFSharedMemory_ALM", @"Session\1\BaseNamedObjects\SFSharedMemory_ALM" };

            foreach (var name in names)
            {
                IntPtr hMap = OpenFileMapping(FILE_MAP_READ, false, name);
                if (hMap != IntPtr.Zero)
                {
                    Console.WriteLine($"[SUCCESS!] Opened mapping: '{name}'");
                    IntPtr pView = MapViewOfFile(hMap, FILE_MAP_READ, 0, 0, UIntPtr.Zero);
                    if (pView != IntPtr.Zero)
                    {
                        var data = Marshal.PtrToStructure<SpeedFanSharedMem>(pView);
                        Console.WriteLine($"  Ver={data.version}, NumFans={data.NumFans}, NumTemps={data.NumTemps}");
                        for (int f = 0; f < data.NumFans; f++)
                        {
                            Console.WriteLine($"    Fan #{f + 1}: {data.fans[f]} RPM");
                        }
                        for (int t = 0; t < data.NumTemps; t++)
                        {
                            Console.WriteLine($"    Temp #{t + 1}: {data.temps[t] / 100.0}°C");
                        }
                        UnmapViewOfFile(pView);
                    }
                    CloseHandle(hMap);
                    return;
                }
                else
                {
                    Console.WriteLine($"Failed to open '{name}': Win32 Error {Marshal.GetLastWin32Error()}");
                }
            }
        }
    }
}
