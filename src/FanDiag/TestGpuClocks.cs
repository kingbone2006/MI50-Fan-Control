using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class TestGpuClocks
    {
        private const string AtiAdlDll = "atiadlxx.dll";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr ADL_Main_Memory_AllocDelegate(int size);
        private static IntPtr ADL_Main_Memory_Alloc(int size) => Marshal.AllocHGlobal(size);
        private static readonly ADL_Main_Memory_AllocDelegate AllocCallback = ADL_Main_Memory_Alloc;

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Main_Control_Create(ADL_Main_Memory_AllocDelegate callback, int enumConnectedAdapters, out IntPtr context);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Main_Control_Destroy(IntPtr context);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_New_QueryPMLogData_Get(IntPtr context, int adapterIndex, IntPtr pPMLogDataOutput);

        public static void Run()
        {
            if (ADL2_Main_Control_Create(AllocCallback, 1, out IntPtr ctx) != 0 || ctx == IntPtr.Zero) return;

            IntPtr pQuery = Marshal.AllocHGlobal(4096);
            Marshal.WriteInt32(pQuery, 0, 4096);

            int ret = ADL2_New_QueryPMLogData_Get(ctx, 0, pQuery);
            if (ret == 0)
            {
                int gfxClock = Marshal.ReadInt32(pQuery, 8 + 1 * 8);
                int memClock = Marshal.ReadInt32(pQuery, 8 + 2 * 8);
                int coreTemp = Marshal.ReadInt32(pQuery, 8 + 8 * 8);

                Console.WriteLine($"=== GPU Telemetry ===");
                Console.WriteLine($"GPU Core Temp : {coreTemp}°C");
                Console.WriteLine($"GPU Clock     : {gfxClock} MHz");
                Console.WriteLine($"VRAM Clock    : {memClock} MHz");
            }

            Marshal.FreeHGlobal(pQuery);
            ADL2_Main_Control_Destroy(ctx);
        }
    }
}
