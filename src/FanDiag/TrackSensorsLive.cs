using System;
using System.Runtime.InteropServices;
using System.Threading;

namespace FanDiag
{
    public class TrackSensorsLive
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

            Console.WriteLine("Logging all non-zero sensors every 1s for 10 seconds...");
            for (int iter = 0; iter < 10; iter++)
            {
                Marshal.WriteInt32(pQuery, 0, 4096);
                int ret = ADL2_New_QueryPMLogData_Get(ctx, adapter, pQuery);
                if (ret == 0)
                {
                    Console.Write($"[{iter + 1}] ");
                    for (int s = 0; s < 40; s++)
                    {
                        int val = Marshal.ReadInt32(pQuery, 8 + s * 8);
                        int supp = Marshal.ReadInt32(pQuery, 8 + s * 8 + 4);
                        if (val > 10 && val < 200)
                        {
                            Console.Write($"S{s}={val}°C ");
                        }
                    }
                    Console.WriteLine();
                }
                Thread.Sleep(1000);
            }

            Marshal.FreeHGlobal(pQuery);
            ADL2_Main_Control_Destroy(ctx);
        }
    }
}
