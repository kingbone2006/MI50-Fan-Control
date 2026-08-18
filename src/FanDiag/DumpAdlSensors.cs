using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class DumpAdlSensors
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
        private static extern int ADL2_New_QueryPMLogData_Get(IntPtr context, int adapterIndex, IntPtr pPMLogDataOutput);

        public static void Run()
        {
            if (ADL2_Main_Control_Create(AllocCallback, 1, out IntPtr ctx) != 0 || ctx == IntPtr.Zero)
            {
                Console.WriteLine("Failed to create ADL context");
                return;
            }

            ADL2_Adapter_NumberOfAdapters_Get(ctx, out int numAdapters);
            Console.WriteLine($"Found {numAdapters} adapters");

            int adapterInfoSize = Marshal.SizeOf(typeof(ADLAdapterInfo));
            IntPtr ptr = Marshal.AllocHGlobal(adapterInfoSize * numAdapters);
            for (int i = 0; i < numAdapters; i++)
            {
                Marshal.WriteInt32(new IntPtr(ptr.ToInt64() + i * adapterInfoSize), adapterInfoSize);
            }
            ADL2_Adapter_AdapterInfo_Get(ctx, ptr, adapterInfoSize * numAdapters);

            int matchedAdapter = -1;
            for (int i = 0; i < numAdapters; i++)
            {
                ADLAdapterInfo info = Marshal.PtrToStructure<ADLAdapterInfo>(new IntPtr(ptr.ToInt64() + i * adapterInfoSize));
                if (info.Exist == 0) continue;
                Console.WriteLine($"Adapter {info.AdapterIndex}: {info.AdapterName}, Vendor: 0x{info.VendorID:X}");
                if (info.VendorID == 0x1002 || (!string.IsNullOrEmpty(info.AdapterName) && info.AdapterName.Contains("Radeon", StringComparison.OrdinalIgnoreCase)))
                {
                    matchedAdapter = info.AdapterIndex;
                }
            }
            Marshal.FreeHGlobal(ptr);

            if (matchedAdapter >= 0)
            {
                IntPtr pQuery = Marshal.AllocHGlobal(4096);
                Marshal.WriteInt32(pQuery, 0, 4096);
                int ret = ADL2_New_QueryPMLogData_Get(ctx, matchedAdapter, pQuery);
                Console.WriteLine($"ADL2_New_QueryPMLogData_Get ret = {ret}");

                if (ret == 0)
                {
                    int size = Marshal.ReadInt32(pQuery, 0);
                    int active = Marshal.ReadInt32(pQuery, 4);
                    Console.WriteLine($"Buffer size={size}, activeSample={active}");

                    for (int s = 0; s < 40; s++)
                    {
                        int val = Marshal.ReadInt32(pQuery, 8 + s * 8);
                        int supp = Marshal.ReadInt32(pQuery, 8 + s * 8 + 4);
                        if (val != 0 || supp != 0)
                        {
                            Console.WriteLine($"Sensor {s,2}: val={val,8} (0x{val:X8}), supp={supp}");
                        }
                    }
                }
                Marshal.FreeHGlobal(pQuery);
            }

            ADL2_Main_Control_Destroy(ctx);
        }
    }
}
