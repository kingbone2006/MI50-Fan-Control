using System;
using System.Diagnostics;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using System.Threading;

namespace FanDiag
{
    public class SpeedFanIpcEngine
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

        public static void TestIpc()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" SPEEDFAN SHARED MEMORY IPC TEST");
            Console.WriteLine("==================================================");

            try
            {
                using var mmf = MemoryMappedFile.OpenExisting("SFSharedMemory_ALM", MemoryMappedFileRights.Read);
                using var accessor = mmf.CreateViewAccessor(0, Marshal.SizeOf(typeof(SFSharedMemory)), MemoryMappedFileAccess.Read);

                accessor.Read(0, out SFSharedMemory data);
                Console.WriteLine($"SpeedFan Memory Ver: {data.Version}, Fans: {data.NumFans}, Temps: {data.NumTemps}");

                for (int i = 0; i < data.NumFans; i++)
                {
                    Console.WriteLine($"  Fan [{i + 1}]: {data.Fans[i]} RPM");
                }
                for (int i = 0; i < data.NumTemps; i++)
                {
                    Console.WriteLine($"  Temp [{i + 1}]: {data.Temps[i] / 100.0}°C");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"SpeedFan is not currently running: {ex.Message}");
            }
        }
    }
}
