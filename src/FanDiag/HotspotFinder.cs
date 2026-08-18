using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class HotspotFinder
    {
        private const string AtiAdlDll = "atiadlxx.dll";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr ADL_Main_Memory_AllocDelegate(int size);
        private static IntPtr ADL_Main_Memory_Alloc(int size) => Marshal.AllocHGlobal(size);
        private static ADL_Main_Memory_AllocDelegate allocDelegate = ADL_Main_Memory_Alloc;

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Main_Control_Create(ADL_Main_Memory_AllocDelegate callback, int enumConnectedAdapters, out IntPtr context);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Main_Control_Destroy(IntPtr context);

        [StructLayout(LayoutKind.Sequential)]
        public struct ADLTemperature
        {
            public int iSize;
            public int iTemperature;
        }

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Overdrive5_Temperature_Get(IntPtr context, int adapterIndex, int thermalControllerIndex, out ADLTemperature temperature);

        [StructLayout(LayoutKind.Sequential)]
        public struct ADLOD8CurrentSetting
        {
            public int Count;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 64)]
            public int[] Od8SettingTable;
        }

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Overdrive8_Current_Setting_Get(IntPtr context, int adapterIndex, out ADLOD8CurrentSetting currentSetting);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Overdrive8_Init_Setting_Get(IntPtr context, int adapterIndex, out ADLOD8CurrentSetting initSetting);

        public static void FindHotspot()
        {
            Console.WriteLine("\n--- SEARCHING FOR ALL AVAILABLE TEMPERATURE SENSORS ---");
            IntPtr context = IntPtr.Zero;
            ADL2_Main_Control_Create(allocDelegate, 1, out context);
            if (context == IntPtr.Zero) return;

            try
            {
                for (int adapter = 0; adapter <= 6; adapter++)
                {
                    for (int thermalIdx = 0; thermalIdx < 5; thermalIdx++)
                    {
                        ADLTemperature temp = new ADLTemperature { iSize = Marshal.SizeOf(typeof(ADLTemperature)) };
                        int r = ADL2_Overdrive5_Temperature_Get(context, adapter, thermalIdx, out temp);
                        if (r == 0)
                        {
                            Console.WriteLine($"Adapter {adapter}, Overdrive5 ThermalIdx {thermalIdx}: {temp.iTemperature / 1000.0:F1} °C");
                        }
                    }

                    // OD8 settings
                    try
                    {
                        ADLOD8CurrentSetting cur = new ADLOD8CurrentSetting { Od8SettingTable = new int[64] };
                        int rCur = ADL2_Overdrive8_Current_Setting_Get(context, adapter, out cur);
                        if (rCur == 0 && cur.Count > 0)
                        {
                            Console.WriteLine($"Adapter {adapter} OD8 Current Settings count={cur.Count}:");
                            for (int i = 0; i < cur.Count && i < cur.Od8SettingTable.Length; i++)
                            {
                                if (cur.Od8SettingTable[i] != 0)
                                {
                                    Console.WriteLine($"   OD8 [{i}] = {cur.Od8SettingTable[i]}");
                                }
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        Console.WriteLine($"OD8 err: {ex.Message}");
                    }
                }
            }
            finally
            {
                ADL2_Main_Control_Destroy(context);
            }
        }
    }
}
