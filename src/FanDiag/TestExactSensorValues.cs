using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class TestExactSensorValues
    {
        private const string AtiAdlDll = "atiadlxx.dll";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr ADL_Main_Memory_AllocDelegate(int size);
        private static IntPtr ADL_Main_Memory_Alloc(int size) => Marshal.AllocHGlobal(size);
        private static readonly ADL_Main_Memory_AllocDelegate AllocCallback = ADL_Main_Memory_Alloc;

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Main_Control_Create(ADL_Main_Memory_AllocDelegate callback, int enumConnectedAdapters, out IntPtr context);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_New_QueryPMLogData_Get(IntPtr context, int adapterIndex, IntPtr pPMLogDataOutput);

        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" TEST EXACT SENSOR VALUES (MATCHING FURMARK)");
            Console.WriteLine("==================================================");

            if (ADL2_Main_Control_Create(AllocCallback, 1, out IntPtr context) != 0 || context == IntPtr.Zero) return;

            IntPtr pOut = Marshal.AllocHGlobal(4096);
            Marshal.WriteInt32(pOut, 0, 4096);

            int ret = ADL2_New_QueryPMLogData_Get(context, 0, pOut);
            if (ret == 0)
            {
                int coreTemp = Marshal.ReadInt32(pOut, 8 + 8 * 8);
                int memTemp = Marshal.ReadInt32(pOut, 8 + 9 * 8);
                int hotspotTemp = Marshal.ReadInt32(pOut, 8 + 14 * 8);
                int totalPower = Marshal.ReadInt32(pOut, 8 + 26 * 8);
                int asicPower = Marshal.ReadInt32(pOut, 8 + 27 * 8);
                int gfxClock = Marshal.ReadInt32(pOut, 8 + 1 * 8);
                int activity = Marshal.ReadInt32(pOut, 8 + 21 * 8);

                Console.WriteLine($"GPU Core Temperature:    {coreTemp}°C");
                Console.WriteLine($"GPU VRAM Temperature:    {memTemp}°C");
                Console.WriteLine($"GPU HotSpot Temperature: {hotspotTemp}°C");
                Console.WriteLine($"Total Board Power:       {totalPower} W (Chip/Total Power)");
                Console.WriteLine($"ASIC Power:              {asicPower} W");
                Console.WriteLine($"GPU Clock:               {gfxClock} MHz");
                Console.WriteLine($"GPU Activity:            {activity}%");
            }

            Marshal.FreeHGlobal(pOut);
        }
    }
}
