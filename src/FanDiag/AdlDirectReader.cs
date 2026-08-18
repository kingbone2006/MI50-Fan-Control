using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class AdlDirectReader
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

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_NumberOfAdapters_Get(IntPtr context, out int numAdapters);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        public struct ADLAdapterInfo
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
        public struct ADLPMLogSupportInfo
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] usSensors;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public int[] iReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ADLPMLogStartInput
        {
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public ushort[] usSensors;
            public uint ulSampleRate;
            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
            public int[] iReserved;
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ADLPMLogStartOutput
        {
            public IntPtr pLoggingAddress;
        }

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_PMLog_Support_Get(IntPtr context, int adapterIndex, ref ADLPMLogSupportInfo pPMLogSupportInfo);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Device_PMLog_Device_Create(IntPtr context, int adapterIndex, out uint device);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Device_PMLog_Device_Destroy(IntPtr context, uint device);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_PMLog_Start(IntPtr context, int adapterIndex, ref ADLPMLogStartInput pPMLogStartInput, out ADLPMLogStartOutput pPMLogStartOutput, uint device);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_PMLog_Stop(IntPtr context, int adapterIndex, uint device);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_OverdriveN_Temperature_Get(IntPtr context, int adapterIndex, int temperatureType, out int temperature);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Overdrive6_Temperature_Get(IntPtr context, int adapterIndex, out int temperature);

        public enum ADLSensorType
        {
            SENSOR_MAXTYPES = 0,
            PMLOG_CLK_GFXCLK = 1,
            PMLOG_CLK_MEMCLK = 2,
            PMLOG_CLK_SOCCLK = 3,
            PMLOG_CLK_UVDCLK1 = 4,
            PMLOG_CLK_UVDCLK2 = 5,
            PMLOG_CLK_VCECLK = 6,
            PMLOG_CLK_VCNCLK = 7,
            PMLOG_TEMPERATURE_EDGE = 8,
            PMLOG_TEMPERATURE_MEM = 9,
            PMLOG_TEMPERATURE_VRVDDC = 10,
            PMLOG_TEMPERATURE_VRMVDD = 11,
            PMLOG_TEMPERATURE_LIQUID = 12,
            PMLOG_TEMPERATURE_PLX = 13,
            PMLOG_TEMPERATURE_HOTSPOT = 14,
            PMLOG_TEMPERATURE_SOC = 15,
            PMLOG_FAN_RPM = 16,
            PMLOG_FAN_PERCENTAGE = 17,
            PMLOG_SOC_VOLTAGE = 18,
            PMLOG_SOC_POWER = 19,
            PMLOG_SOC_CURRENT = 20,
            PMLOG_INFO_ACTIVITY_GFX = 21,
            PMLOG_INFO_ACTIVITY_MEM = 22,
            PMLOG_INFO_ACTIVITY_UVD = 23,
            PMLOG_INFO_ACTIVITY_VCE = 24,
            PMLOG_INFO_ACTIVITY_VCN = 25,
            PMLOG_INFO_TOTAL_BOARD_POWER = 26,
            PMLOG_INFO_ASIC_POWER = 27
        }

        public static void TestDirectPMLog()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" DIRECT AMD ADL PMLOG TELEMETRY TEST ");
            Console.WriteLine("==================================================");

            IntPtr context = IntPtr.Zero;
            int ret = ADL2_Main_Control_Create(allocDelegate, 1, out context);
            if (ret != 0 || context == IntPtr.Zero)
            {
                Console.WriteLine($"ADL2_Main_Control_Create failed: {ret}");
                return;
            }

            try
            {
                int numAdapters = 0;
                ADL2_Adapter_NumberOfAdapters_Get(context, out numAdapters);

                int adapterInfoSize = Marshal.SizeOf(typeof(ADLAdapterInfo));
                IntPtr ptr = Marshal.AllocHGlobal(adapterInfoSize * numAdapters);
                for (int i = 0; i < numAdapters; i++)
                {
                    Marshal.WriteInt32(new IntPtr(ptr.ToInt64() + i * adapterInfoSize), adapterInfoSize);
                }
                ADL2_Adapter_AdapterInfo_Get(context, ptr, adapterInfoSize * numAdapters);

                for (int i = 0; i < numAdapters; i++)
                {
                    ADLAdapterInfo info = Marshal.PtrToStructure<ADLAdapterInfo>(new IntPtr(ptr.ToInt64() + i * adapterInfoSize));
                    if (info.Exist == 0) continue;

                    Console.WriteLine($"\n[Adapter {i}] Index: {info.AdapterIndex}, Name: '{info.AdapterName}'");

                    ADLPMLogSupportInfo supp = new ADLPMLogSupportInfo
                    {
                        usSensors = new ushort[256],
                        iReserved = new int[256]
                    };
                    ADL2_Adapter_PMLog_Support_Get(context, info.AdapterIndex, ref supp);

                    // Test individual temperature APIs on this adapter
                    int edgeOdN = 0, hotOdN = 0, od6Temp = 0;
                    int rEdgeN = ADL2_OverdriveN_Temperature_Get(context, info.AdapterIndex, 1, out edgeOdN);
                    int rHotN = ADL2_OverdriveN_Temperature_Get(context, info.AdapterIndex, 2, out hotOdN);
                    int rOd6 = ADL2_Overdrive6_Temperature_Get(context, info.AdapterIndex, out od6Temp);
                    Console.WriteLine($"  ODN Edge Temp (1): res={rEdgeN}, val={edgeOdN/1000.0:F1} C");
                    Console.WriteLine($"  ODN Hotspot Temp (2): res={rHotN}, val={hotOdN/1000.0:F1} C");
                    Console.WriteLine($"  OD6 Temp: res={rOd6}, val={od6Temp/1000.0:F1} C");

                    uint device = 0;
                    int rDev = ADL2_Device_PMLog_Device_Create(context, info.AdapterIndex, out device);
                    if (rDev == 0)
                    {
                        ADLPMLogStartInput startInput = new ADLPMLogStartInput
                        {
                            usSensors = new ushort[256],
                            ulSampleRate = 500,
                            iReserved = new int[256]
                        };
                        for (int s = 0; s < 256; s++)
                        {
                            startInput.usSensors[s] = (ushort)s;
                        }

                        ADLPMLogStartOutput startOutput;
                        int rStart = ADL2_Adapter_PMLog_Start(context, info.AdapterIndex, ref startInput, out startOutput, device);

                        if (rStart == 0 && startOutput.pLoggingAddress != IntPtr.Zero)
                        {
                            for (int poll = 0; poll < 3; poll++)
                            {
                                System.Threading.Thread.Sleep(600);

                                int edgeTemp = -1, hotspotTemp = -1, gfxClock = -1, asicPower = -1, boardPower = -1;

                                for (int offset = 16; offset < 16 + 128 * 8; offset += 8)
                                {
                                    int sensorType = Marshal.ReadInt32(startOutput.pLoggingAddress, offset);
                                    int sensorValue = Marshal.ReadInt32(startOutput.pLoggingAddress, offset + 4);

                                    if (sensorType == (int)ADLSensorType.PMLOG_TEMPERATURE_EDGE) edgeTemp = sensorValue;
                                    else if (sensorType == (int)ADLSensorType.PMLOG_TEMPERATURE_HOTSPOT) hotspotTemp = sensorValue;
                                    else if (sensorType == (int)ADLSensorType.PMLOG_CLK_GFXCLK) gfxClock = sensorValue;
                                    else if (sensorType == (int)ADLSensorType.PMLOG_INFO_ASIC_POWER) asicPower = sensorValue;
                                    else if (sensorType == (int)ADLSensorType.PMLOG_INFO_TOTAL_BOARD_POWER) boardPower = sensorValue;
                                }

                                Console.WriteLine($"[Telemetry #{poll+1}] Edge/Core: {edgeTemp}°C | HotSpot: {hotspotTemp}°C | GFX Clock: {gfxClock} MHz | ASIC Power: {asicPower} W | Board Power: {boardPower} W");
                            }

                            ADL2_Adapter_PMLog_Stop(context, info.AdapterIndex, device);
                        }
                        ADL2_Device_PMLog_Device_Destroy(context, device);
                    }
                }
                Marshal.FreeHGlobal(ptr);
            }
            finally
            {
                ADL2_Main_Control_Destroy(context);
            }
        }
    }
}
