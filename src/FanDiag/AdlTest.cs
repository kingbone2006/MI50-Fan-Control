using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class AdlTest
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

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_OverdriveN_Temperature_Get(IntPtr context, int adapterIndex, int temperatureType, out int temperature);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Overdrive6_Temperature_Get(IntPtr context, int adapterIndex, out int temperature);

        public static void Run()
        {
            Console.WriteLine("=== TESTING ADL OVERDRIVEN & OVERDRIVE6 TEMPERATURES ===");
            if (ADL2_Main_Control_Create(AllocCallback, 1, out IntPtr context) != 0 || context == IntPtr.Zero)
            {
                Console.WriteLine("ADL Create failed");
                return;
            }

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
                    Console.WriteLine($"Matched Adapter [{i}]: Index={info.AdapterIndex}, Name='{info.AdapterName}'");
                    break;
                }
            }
            Marshal.FreeHGlobal(ptr);

            if (targetAdapter >= 0)
            {
                int r6 = ADL2_Overdrive6_Temperature_Get(context, targetAdapter, out int temp6);
                Console.WriteLine($"Overdrive6 Temp: ret={r6}, val={temp6} ({temp6 / 1000.0:F1}°C)");

                for (int t = 0; t <= 15; t++)
                {
                    int rN = ADL2_OverdriveN_Temperature_Get(context, targetAdapter, t, out int tempN);
                    Console.WriteLine($"OverdriveN Temp [Type {t,2}]: ret={rN,2}, val={tempN,6} ({tempN / 1000.0:F1}°C)");
                }
            }

            ADL2_Main_Control_Destroy(context);
        }
    }
}
