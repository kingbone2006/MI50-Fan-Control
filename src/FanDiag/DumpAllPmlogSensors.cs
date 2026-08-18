using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class DumpAllPmlogSensors
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
            if (ADL2_Main_Control_Create(AllocCallback, 1, out IntPtr context) != 0 || context == IntPtr.Zero) return;

            IntPtr pOut = Marshal.AllocHGlobal(4096);
            Marshal.WriteInt32(pOut, 0, 4096);

            int ret = ADL2_New_QueryPMLogData_Get(context, 0, pOut);
            if (ret == 0)
            {
                Console.WriteLine("DUMP ALL ACTIVE SENSORS IN ADLPMLogDataOutput:");
                for (int s = 0; s < 64; s++)
                {
                    int val = Marshal.ReadInt32(pOut, 8 + s * 8);
                    int supp = Marshal.ReadInt32(pOut, 8 + s * 8 + 4);
                    if (val > 0 || supp > 0)
                    {
                        Console.WriteLine($"  Index [{s,2}]: Value = {val,6}, Supported = {supp}");
                    }
                }
            }
            Marshal.FreeHGlobal(pOut);
        }
    }
}
