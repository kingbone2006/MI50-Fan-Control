using System;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class DeepDiagnostics
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
        private static extern int ADL2_Adapter_NumberOfAdapters_Get(IntPtr context, out int numAdapters);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct ADLAdapterInfo
        {
            public int Size;
            public int AdapterIndex;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string UDID;
            public int BusNumber;
            public int DeviceNumber;
            public int FunctionNumber;
            public int VendorID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string AdapterName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DisplayName;
            public int Present;
            public int Exist;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DriverPath;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DriverPathExt;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string PNPString;
            public int OSDisplayIndex;
        }

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_AdapterInfo_Get(IntPtr context, IntPtr info, int inputSize);

        [StructLayout(LayoutKind.Sequential)]
        private struct ADLTemperature
        {
            public int iSize;
            public int iTemperature;
        }

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Overdrive5_Temperature_Get(IntPtr context, int adapterIndex, int thermalControllerIndex, out ADLTemperature temperature);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Overdrive6_Temperature_Get(IntPtr context, int adapterIndex, out int temperature);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_OverdriveN_Temperature_Get(IntPtr context, int adapterIndex, int temperatureType, out int temperature);

        [StructLayout(LayoutKind.Sequential)]
        private struct ADLPMLogStartInput
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] usSensors;
            public uint ulSampleRate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public int[] iReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        private struct ADLPMLogStartOutput
        {
            public IntPtr pLoggingAddress;
        }

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Device_PMLog_Device_Create(IntPtr context, int adapterIndex, out uint device);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_PMLog_Start(IntPtr context, int adapterIndex, ref ADLPMLogStartInput pPMLogStartInput, out ADLPMLogStartOutput pPMLogStartOutput, uint device);

        public static void RunAll()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" 1. AMD ADL SENSOR & HOTSPOT DIAGNOSTICS");
            Console.WriteLine("==================================================");

            IntPtr context = IntPtr.Zero;
            try
            {
                ADL2_Main_Control_Create(AllocCallback, 1, out context);
                ADL2_Adapter_NumberOfAdapters_Get(context, out int numAdapters);

                int adapterInfoSize = Marshal.SizeOf(typeof(ADLAdapterInfo));
                IntPtr ptr = Marshal.AllocHGlobal(adapterInfoSize * numAdapters);
                for (int i = 0; i < numAdapters; i++)
                {
                    Marshal.WriteInt32(new IntPtr(ptr.ToInt64() + i * adapterInfoSize), adapterInfoSize);
                }
                ADL2_Adapter_AdapterInfo_Get(context, ptr, adapterInfoSize * numAdapters);

                int targetAdapter = -1;
                for (int i = 0; i < numAdapters; i++)
                {
                    ADLAdapterInfo info = Marshal.PtrToStructure<ADLAdapterInfo>(new IntPtr(ptr.ToInt64() + i * adapterInfoSize));
                    if (info.Exist == 0) continue;
                    if (info.VendorID == 0x1002 || info.AdapterName.Contains("Radeon", StringComparison.OrdinalIgnoreCase))
                    {
                        targetAdapter = info.AdapterIndex;
                        Console.WriteLine($"Found AMD Adapter: Index={info.AdapterIndex}, Name='{info.AdapterName}'");
                        break;
                    }
                }
                Marshal.FreeHGlobal(ptr);

                if (targetAdapter >= 0)
                {
                    // Test Overdrive 5
                    if (ADL2_Overdrive5_Temperature_Get(context, targetAdapter, 0, out ADLTemperature od5) == 0)
                    {
                        Console.WriteLine($"  [OD5 Temp] {od5.iTemperature / 1000.0:F1} °C (Raw: {od5.iTemperature})");
                    }

                    // Test Overdrive 6
                    if (ADL2_Overdrive6_Temperature_Get(context, targetAdapter, out int od6) == 0)
                    {
                        Console.WriteLine($"  [OD6 Temp] {od6 / 1000.0:F1} °C (Raw: {od6})");
                    }

                    // Test OverdriveN Temperature Types (0..10)
                    for (int t = 0; t <= 10; t++)
                    {
                        int retN = ADL2_OverdriveN_Temperature_Get(context, targetAdapter, t, out int tempN);
                        if (retN == 0)
                        {
                            Console.WriteLine($"  [ODN Temp Type {t}] {tempN / 1000.0:F1} °C (Raw: {tempN})");
                        }
                    }

                    // PMLog Raw Dump
                    ADL2_Device_PMLog_Device_Create(context, targetAdapter, out uint dev);
                    ADLPMLogStartInput input = new ADLPMLogStartInput
                    {
                        usSensors = new ushort[256],
                        ulSampleRate = 500,
                        iReserved = new int[256]
                    };
                    for (int s = 0; s < 256; s++) input.usSensors[s] = (ushort)s;
                    ADL2_Adapter_PMLog_Start(context, targetAdapter, ref input, out ADLPMLogStartOutput output, dev);

                    if (output.pLoggingAddress != IntPtr.Zero)
                    {
                        Console.WriteLine("\n  --- PMLOG NON-ZERO RAW SENSORS ---");
                        for (int offset = 16; offset < 16 + 128 * 8; offset += 8)
                        {
                            int type = Marshal.ReadInt32(output.pLoggingAddress, offset);
                            int val = Marshal.ReadInt32(output.pLoggingAddress, offset + 4);
                            if (val != 0 || type != 0)
                            {
                                Console.WriteLine($"    Sensor Type {type,3}: Value = {val,6}");
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ADL Error: {ex.Message}");
            }
            finally
            {
                if (context != IntPtr.Zero) ADL2_Main_Control_Destroy(context);
            }

            Console.WriteLine("\n==================================================");
            Console.WriteLine(" 2. HARDWARE MONITOR & SUPERIO LPC SCAN");
            Console.WriteLine("==================================================");

            try
            {
                var computer = new Computer
                {
                    IsMotherboardEnabled = true,
                    IsControllerEnabled = true,
                    IsCpuEnabled = true,
                    IsGpuEnabled = true,
                    IsMemoryEnabled = true,
                    IsStorageEnabled = true
                };

                computer.Open();

                foreach (var hw in computer.Hardware)
                {
                    hw.Update();
                    Console.WriteLine($"[Hardware] '{hw.Name}' ({hw.HardwareType}) - ID: {hw.Identifier}");
                    foreach (var sub in hw.SubHardware)
                    {
                        sub.Update();
                        Console.WriteLine($"  [SubHardware] '{sub.Name}' ({sub.HardwareType}) - ID: {sub.Identifier}");
                        foreach (var sens in sub.Sensors)
                        {
                            Console.WriteLine($"    -> Sensor: [{sens.SensorType}] '{sens.Name}' = {sens.Value} (Index: {sens.Index})");
                        }
                    }

                    foreach (var sens in hw.Sensors)
                    {
                        Console.WriteLine($"  -> Sensor: [{sens.SensorType}] '{sens.Name}' = {sens.Value} (Index: {sens.Index})");
                    }
                }

                computer.Close();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Hardware Scan Error: {ex.Message}");
            }
        }
    }
}
