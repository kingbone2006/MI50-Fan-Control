using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class TestAdlSensors
    {
        private const string AtiAdlDll = "atiadlxx.dll";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr ADL_Main_Memory_AllocDelegate(int size);
        private static IntPtr ADL_Main_Memory_Alloc(int size) => Marshal.AllocHGlobal(size);
        private static readonly ADL_Main_Memory_AllocDelegate AllocCallback = ADL_Main_Memory_Alloc;

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Main_Control_Create(ADL_Main_Memory_AllocDelegate callback, int enumConnectedAdapters, out IntPtr context);

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

        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" TEST AMD ADL PMLOG SENSOR VALUES");
            Console.WriteLine("==================================================");

            if (ADL2_Main_Control_Create(AllocCallback, 1, out IntPtr context) != 0 || context == IntPtr.Zero)
            {
                Console.WriteLine("Could not create ADL context");
                return;
            }

            ADL2_Adapter_NumberOfAdapters_Get(context, out int numAdapters);
            int adapterInfoSize = Marshal.SizeOf(typeof(ADLAdapterInfo));
            IntPtr ptr = Marshal.AllocHGlobal(adapterInfoSize * numAdapters);
            for (int i = 0; i < numAdapters; i++) Marshal.WriteInt32(new IntPtr(ptr.ToInt64() + i * adapterInfoSize), adapterInfoSize);
            ADL2_Adapter_AdapterInfo_Get(context, ptr, adapterInfoSize * numAdapters);

            int targetAdapter = -1;
            for (int i = 0; i < numAdapters; i++)
            {
                var info = Marshal.PtrToStructure<ADLAdapterInfo>(new IntPtr(ptr.ToInt64() + i * adapterInfoSize));
                if (info.Exist != 0 && (info.VendorID == 0x1002 || info.AdapterName.Contains("Radeon")))
                {
                    targetAdapter = info.AdapterIndex;
                    Console.WriteLine($"Found GPU: {info.AdapterName} at AdapterIndex {targetAdapter}");
                    break;
                }
            }
            Marshal.FreeHGlobal(ptr);

            if (targetAdapter < 0) return;

            ADL2_Device_PMLog_Device_Create(context, targetAdapter, out uint dev);
            ADLPMLogStartInput input = new ADLPMLogStartInput
            {
                usSensors = new ushort[256],
                ulSampleRate = 500,
                iReserved = new int[256]
            };
            for (int s = 0; s < 256; s++) input.usSensors[s] = (ushort)s;

            ADL2_Adapter_PMLog_Start(context, targetAdapter, ref input, out ADLPMLogStartOutput output, dev);
            IntPtr pLog = output.pLoggingAddress;

            if (pLog == IntPtr.Zero)
            {
                Console.WriteLine("pLoggingAddress is null");
                return;
            }

            Console.WriteLine($"pLoggingAddress: 0x{pLog.ToInt64():X}");

            // Structure of ADLPMLogDataOutput in ADL SDK:
            // ulSize (4 bytes), ulActiveSample (4 bytes)
            // Array of 256 sensor values, each is ADLSingleSensorData (uint ulSupported, int ulValue) = 8 bytes each!
            // Sensor index 0 is SENSOR_MAXTYPES (0)
            // Sensor index 1 is PMLOG_CLK_GFXCLK
            // Sensor index 8 is PMLOG_TEMPERATURE_EDGE
            // Sensor index 9 is PMLOG_TEMPERATURE_MEM
            // Sensor index 14 is PMLOG_TEMPERATURE_HOTSPOT
            // Sensor index 26 is PMLOG_INFO_TOTAL_BOARD_POWER
            // Sensor index 27 is PMLOG_INFO_ASIC_POWER

            string[] sensorNames = new string[35];
            sensorNames[1] = "GFXCLK (MHz)";
            sensorNames[2] = "MEMCLK (MHz)";
            sensorNames[8] = "TEMP_EDGE (°C)";
            sensorNames[9] = "TEMP_MEM (°C)";
            sensorNames[10] = "TEMP_VRVDDC (°C)";
            sensorNames[11] = "TEMP_VRMVDD (°C)";
            sensorNames[12] = "TEMP_LIQUID (°C)";
            sensorNames[13] = "TEMP_PLX (°C)";
            sensorNames[14] = "TEMP_HOTSPOT (°C)";
            sensorNames[15] = "TEMP_SOC (°C)";
            sensorNames[16] = "FAN_RPM";
            sensorNames[17] = "FAN_PERCENTAGE";
            sensorNames[21] = "ACTIVITY_GFX (%)";
            sensorNames[26] = "TOTAL_BOARD_POWER (W)";
            sensorNames[27] = "ASIC_POWER (W)";

            for (int sensorId = 0; sensorId < 32; sensorId++)
            {
                int offset = 8 + sensorId * 8;
                uint supported = (uint)Marshal.ReadInt32(pLog, offset);
                int val = Marshal.ReadInt32(pLog, offset + 4);

                string name = sensorId < sensorNames.Length && !string.IsNullOrEmpty(sensorNames[sensorId]) ? sensorNames[sensorId] : $"Sensor {sensorId}";
                if (supported != 0 || val != 0)
                {
                    Console.WriteLine($"  [{sensorId,2}] {name,-25}: Supported={supported}, Value={val}");
                }
            }
        }
    }
}
