using System;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class SpeedFanSharedMemoryTest
    {
        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SFSharedMemory
        {
            public ushort Version;
            public ushort Flags;
            public int MemSize;
            public int Handle;
            public ushort NumTemps;
            public ushort NumFans;
            public ushort NumVoltages;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] Temps;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] Fans;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] Voltages;
        }

        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" SPEEDFAN SHARED MEMORY TEST (LOCAL & GLOBAL)");
            Console.WriteLine("==================================================");

            string[] mapNames = { "SFSharedMemory_ALM", @"Global\SFSharedMemory_ALM", "SpeedFanSharedMemory", @"Global\SpeedFanSharedMemory" };

            foreach (var name in mapNames)
            {
                try
                {
                    using var mmf = MemoryMappedFile.OpenExisting(name, MemoryMappedFileRights.Read);
                    using var accessor = mmf.CreateViewAccessor(0, Marshal.SizeOf(typeof(SFSharedMemory)), MemoryMappedFileAccess.Read);

                    accessor.Read(0, out SFSharedMemory data);
                    Console.WriteLine($"[FOUND!] MapName: '{name}' | Ver: {data.Version}, Fans Count: {data.NumFans}, Temps Count: {data.NumTemps}");

                    for (int i = 0; i < data.NumFans; i++)
                    {
                        Console.WriteLine($"   Fan #{i + 1}: {data.Fans[i]} RPM");
                    }
                    for (int i = 0; i < data.NumTemps; i++)
                    {
                        Console.WriteLine($"   Temp #{i + 1}: {data.Temps[i] / 100.0}°C");
                    }
                    return;
                }
                catch
                {
                    Console.WriteLine($"MapName '{name}': Not found (SpeedFan not active)");
                }
            }
        }
    }
}
