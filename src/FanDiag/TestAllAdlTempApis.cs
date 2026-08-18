using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class TestAllAdlTempApis
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
        private static extern int ADL2_OverdriveN_Temperature_Get(IntPtr context, int adapterIndex, int tempType, out int iTemperature);

        [StructLayout(LayoutKind.Sequential)]
        public struct ADLTemperature
        {
            public int iSize;
            public int iTemperature;
        }

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Overdrive5_Temperature_Get(IntPtr context, int adapterIndex, int thermalControllerIndex, out ADLTemperature lpTemperature);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Overdrive6_Temperature_Get(IntPtr context, int adapterIndex, out int lpTemperature);

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

            // 1. OD5 Temp
            var od5Temp = new ADLTemperature { iSize = Marshal.SizeOf(typeof(ADLTemperature)) };
            int rOd5 = ADL2_Overdrive5_Temperature_Get(ctx, adapter, 0, out od5Temp);
            Console.WriteLine($"OD5 Temp: ret={rOd5}, temp={od5Temp.iTemperature / 1000.0f}°C (raw={od5Temp.iTemperature})");

            // 2. OD6 Temp
            int od6Temp = 0;
            int rOd6 = ADL2_Overdrive6_Temperature_Get(ctx, adapter, out od6Temp);
            Console.WriteLine($"OD6 Temp: ret={rOd6}, temp={od6Temp / 1000.0f}°C (raw={od6Temp})");

            // 3. ODN Temp types (0=Edge, 1=HotSpot, 2=Mem, 3=VRVDDC, 4=VRMVDD, 5=Liquid, 6=PLX)
            for (int t = 0; t <= 10; t++)
            {
                int odnTemp = 0;
                int rOdn = ADL2_OverdriveN_Temperature_Get(ctx, adapter, t, out odnTemp);
                if (rOdn == 0)
                {
                    Console.WriteLine($"ODN Temp Type {t}: temp={odnTemp / 1000.0f}°C (raw={odnTemp})");
                }
            }

            // 4. PMLog Temp
            IntPtr pQuery = Marshal.AllocHGlobal(4096);
            Marshal.WriteInt32(pQuery, 0, 4096);
            int rPm = ADL2_New_QueryPMLogData_Get(ctx, adapter, pQuery);
            if (rPm == 0)
            {
                for (int s = 0; s < 30; s++)
                {
                    int val = Marshal.ReadInt32(pQuery, 8 + s * 8);
                    int supp = Marshal.ReadInt32(pQuery, 8 + s * 8 + 4);
                    Console.WriteLine($"PMLog Sensor {s,2}: val={val,6}, supp={supp}");
                }
            }
            Marshal.FreeHGlobal(pQuery);

            ADL2_Main_Control_Destroy(ctx);
        }
    }
}
