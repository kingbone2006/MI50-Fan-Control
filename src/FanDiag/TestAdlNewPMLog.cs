using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class TestAdlNewPMLog
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

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_NumberOfAdapters_Get(IntPtr context, out int numAdapters);

        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" TEST ADL2_NEW_QUERYPMLOGDATA_GET");
            Console.WriteLine("==================================================");

            if (ADL2_Main_Control_Create(AllocCallback, 1, out IntPtr context) != 0 || context == IntPtr.Zero) return;

            // ADLPMLogDataOutput size = 4 + 4 + 256 * 8 = 2056 bytes
            IntPtr pOut = Marshal.AllocHGlobal(4096);
            for (int i = 0; i < 4096; i++) Marshal.WriteByte(pOut, i, 0);
            Marshal.WriteInt32(pOut, 0, 4096); // size

            int ret = ADL2_New_QueryPMLogData_Get(context, 0, pOut);
            Console.WriteLine($"ADL2_New_QueryPMLogData_Get ret = {ret}");

            if (ret == 0)
            {
                int size = Marshal.ReadInt32(pOut, 0);
                int active = Marshal.ReadInt32(pOut, 4);
                Console.WriteLine($"Size: {size}, Active: {active}");

                for (int s = 0; s < 30; s++)
                {
                    int supported = Marshal.ReadInt32(pOut, 8 + s * 8);
                    int val = Marshal.ReadInt32(pOut, 8 + s * 8 + 4);
                    if (supported != 0 || val != 0)
                    {
                        Console.WriteLine($"  Sensor [{s,2}]: Supported={supported}, Value={val}");
                    }
                }
            }

            Marshal.FreeHGlobal(pOut);
        }
    }
}
