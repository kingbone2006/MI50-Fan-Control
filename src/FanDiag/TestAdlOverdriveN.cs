using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class TestAdlOverdriveN
    {
        private const string AtiAdlDll = "atiadlxx.dll";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr ADL_Main_Memory_AllocDelegate(int size);
        private static IntPtr ADL_Main_Memory_Alloc(int size) => Marshal.AllocHGlobal(size);
        private static readonly ADL_Main_Memory_AllocDelegate AllocCallback = ADL_Main_Memory_Alloc;

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Main_Control_Create(ADL_Main_Memory_AllocDelegate callback, int enumConnectedAdapters, out IntPtr context);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_OverdriveN_Temperature_Get(IntPtr context, int iAdapterIndex, int iTemperatureType, out int iTemperature);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Overdrive6_CurrentPower_Get(IntPtr context, int iAdapterIndex, int iPowerType, out int iCurrentValue);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_NumberOfAdapters_Get(IntPtr context, out int numAdapters);

        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" TEST ADL2_OVERDRIVEN_TEMPERATURE_GET & POWER");
            Console.WriteLine("==================================================");

            if (ADL2_Main_Control_Create(AllocCallback, 1, out IntPtr context) != 0 || context == IntPtr.Zero) return;

            // Temperature Types in ADL SDK:
            // 1 = ADL_ODN_TEMPERATURE_EDGE
            // 2 = ADL_ODN_TEMPERATURE_HOTSPOT
            // 3 = ADL_ODN_TEMPERATURE_MEM
            // 4 = ADL_ODN_TEMPERATURE_VRVDDC
            // 5 = ADL_ODN_TEMPERATURE_VRMVDD
            // 6 = ADL_ODN_TEMPERATURE_LIQUID
            // 7 = ADL_ODN_TEMPERATURE_PLX

            for (int t = 0; t <= 10; t++)
            {
                int ret = ADL2_OverdriveN_Temperature_Get(context, 0, t, out int temp);
                if (ret == 0)
                {
                    Console.WriteLine($"  [ODN Temp Type {t}]: {temp / 1000.0}°C ({temp})");
                }
            }

            // Power Types:
            // 0 = Default/Current Power
            // 1 = Total Board Power
            // 2 = Chip/ASIC Power
            for (int p = 0; p <= 5; p++)
            {
                int ret = ADL2_Overdrive6_CurrentPower_Get(context, 0, p, out int power);
                if (ret == 0)
                {
                    Console.WriteLine($"  [OD6 Power Type {p}]: {power / 256.0:F1} W or {power} W (raw={power})");
                }
            }
        }
    }
}
