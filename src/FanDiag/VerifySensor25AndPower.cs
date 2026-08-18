using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class VerifySensor25AndPower
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
            if (ADL2_Main_Control_Create(AllocCallback, 1, out IntPtr ctx) != 0 || ctx == IntPtr.Zero)
            {
                Console.WriteLine("Failed to create ADL context");
                return;
            }

            int adapter = 0;
            IntPtr pQuery = Marshal.AllocHGlobal(4096);
            Marshal.WriteInt32(pQuery, 0, 4096);

            int ret = ADL2_New_QueryPMLogData_Get(ctx, adapter, pQuery);
            if (ret == 0)
            {
                int core = Marshal.ReadInt32(pQuery, 8 + 8 * 8);
                int mem = Marshal.ReadInt32(pQuery, 8 + 9 * 8);
                int vrVddc = Marshal.ReadInt32(pQuery, 8 + 10 * 8);
                int vrVddio = Marshal.ReadInt32(pQuery, 8 + 24 * 8);
                int hotspot = Marshal.ReadInt32(pQuery, 8 + 25 * 8);
                int vrVddci = Marshal.ReadInt32(pQuery, 8 + 26 * 8);
                int asicPower = Marshal.ReadInt32(pQuery, 8 + 27 * 8);

                Console.WriteLine($"=== AMD Radeon Pro VII Live Telemetry ===");
                Console.WriteLine($"GPU Temperature (Core)    : {core}°C");
                Console.WriteLine($"GPU HBM Temperature (Mem) : {mem}°C");
                Console.WriteLine($"GPU VR VDDC Temperature   : {vrVddc}°C");
                Console.WriteLine($"GPU VR VDDIO Temperature  : {vrVddio}°C");
                Console.WriteLine($"GPU Hot Spot Temperature  : {hotspot}°C (HWiNFO Match!)");
                Console.WriteLine($"GPU VR VDDCI Temperature  : {vrVddci}°C");
                Console.WriteLine($"GPU ASIC Power            : {asicPower}W");
            }

            Marshal.FreeHGlobal(pQuery);
            ADL2_Main_Control_Destroy(ctx);
        }
    }
}
