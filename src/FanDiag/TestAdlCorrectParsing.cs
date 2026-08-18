using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class TestAdlCorrectParsing
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
            Console.WriteLine(" TEST CORRECT AMD ADL PMLOG PARSING");
            Console.WriteLine("==================================================");

            if (ADL2_Main_Control_Create(AllocCallback, 1, out IntPtr context) != 0 || context == IntPtr.Zero) return;

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

            if (pLog == IntPtr.Zero) return;

            float edgeTemp = 0;
            float memTemp = 0;
            float hotspotTemp = 0;
            float gfxClock = 0;
            float totalBoardPower = 0;
            float asicPower = 0;
            float activityGfx = 0;

            for (int offset = 8; offset < 8 + 64 * 8; offset += 8)
            {
                int sensorType = Marshal.ReadInt32(pLog, offset);
                int sensorValue = Marshal.ReadInt32(pLog, offset + 4);

                switch (sensorType)
                {
                    case 1: // GFXCLK
                        gfxClock = sensorValue;
                        break;
                    case 8: // TEMP_EDGE
                        edgeTemp = sensorValue;
                        break;
                    case 9: // TEMP_MEM
                        memTemp = sensorValue;
                        break;
                    case 14: // TEMP_HOTSPOT
                        hotspotTemp = sensorValue;
                        break;
                    case 21: // ACTIVITY_GFX
                        activityGfx = sensorValue;
                        break;
                    case 26: // TOTAL_BOARD_POWER
                        totalBoardPower = sensorValue;
                        break;
                    case 27: // ASIC_POWER
                        asicPower = sensorValue;
                        break;
                }
            }

            Console.WriteLine($"[PARSED TELEMETRY RESULTS]:");
            Console.WriteLine($"  GPU Core (Edge) Temp:    {edgeTemp}°C");
            Console.WriteLine($"  GPU Memory (VRAM) Temp:  {memTemp}°C");
            Console.WriteLine($"  GPU HotSpot Temp:        {hotspotTemp}°C");
            Console.WriteLine($"  GPU Clock:               {gfxClock} MHz");
            Console.WriteLine($"  Total Board Power:       {totalBoardPower} W");
            Console.WriteLine($"  ASIC Chip Power:         {asicPower} W");
            Console.WriteLine($"  GPU Activity:            {activityGfx}%");
        }
    }
}
