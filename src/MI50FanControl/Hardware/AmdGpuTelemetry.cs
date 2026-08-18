using System;
using System.Runtime.InteropServices;
using MI50FanControl.Services;

namespace MI50FanControl.Hardware
{
    public class AmdGpuTelemetryData
    {
        public bool IsAvailable { get; set; }
        public string GpuName { get; set; } = "AMD Radeon Instinct MI50 / Radeon PRO VII";
        public float CoreTemperature { get; set; }
        public float MemoryTemperature { get; set; }
        public float GpuClockMhz { get; set; }
        public float VramClockMhz { get; set; }
        public float GpuActivityPercent { get; set; }
    }

    public class AmdGpuTelemetry : IDisposable
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
        private static extern int ADL2_Device_PMLog_Device_Destroy(IntPtr context, uint device);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_PMLog_Start(IntPtr context, int adapterIndex, ref ADLPMLogStartInput pPMLogStartInput, out ADLPMLogStartOutput pPMLogStartOutput, uint device);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_PMLog_Stop(IntPtr context, int adapterIndex, uint device);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_New_QueryPMLogData_Get(IntPtr context, int adapterIndex, IntPtr pPMLogDataOutput);

        [StructLayout(LayoutKind.Sequential)]
        public struct ADLFanSpeedValue
        {
            public int iSize;
            public int iSpeedType;
            public int iFanSpeed;
            public int iFlags;
        }

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Overdrive5_FanSpeed_Set(IntPtr context, int adapterIndex, int thermalControllerIndex, ref ADLFanSpeedValue fanSpeedValue);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Overdrive5_FanSpeedToDefault_Set(IntPtr context, int adapterIndex, int thermalControllerIndex);

        private IntPtr _context = IntPtr.Zero;
        private int _adapterIndex = -1;
        private uint _device = 0;
        private IntPtr _pLoggingAddress = IntPtr.Zero;
        private IntPtr _pQueryBuffer = IntPtr.Zero;
        private bool _isInitialized = false;
        private string _adapterName = "AMD GPU";

        private float _lastValidCore = 0;
        private float _lastValidMem = 0;

        public bool IsInitialized => _isInitialized;
        public string GpuName => _adapterName;
        public int AdapterIndex => _adapterIndex;

        public bool Initialize()
        {
            LogService.Instance.Hardware("AMD ADL", "Bắt đầu khởi tạo kết nối driver AMD (atiadlxx.dll)...");

            try
            {
                int ret = ADL2_Main_Control_Create(AllocCallback, 1, out _context);
                if (ret != 0 || _context == IntPtr.Zero)
                {
                    LogService.Instance.Error("AMD ADL", $"ADL2_Main_Control_Create thất bại: {ret}");
                    return false;
                }

                int numAdapters = 0;
                ADL2_Adapter_NumberOfAdapters_Get(_context, out numAdapters);
                if (numAdapters <= 0) return false;

                int adapterInfoSize = Marshal.SizeOf(typeof(ADLAdapterInfo));
                IntPtr ptr = Marshal.AllocHGlobal(adapterInfoSize * numAdapters);
                for (int i = 0; i < numAdapters; i++)
                {
                    Marshal.WriteInt32(new IntPtr(ptr.ToInt64() + i * adapterInfoSize), adapterInfoSize);
                }
                ADL2_Adapter_AdapterInfo_Get(_context, ptr, adapterInfoSize * numAdapters);

                for (int i = 0; i < numAdapters; i++)
                {
                    ADLAdapterInfo info = Marshal.PtrToStructure<ADLAdapterInfo>(new IntPtr(ptr.ToInt64() + i * adapterInfoSize));
                    if (info.Exist == 0) continue;

                    if (info.VendorID == 0x1002 || (!string.IsNullOrEmpty(info.AdapterName) && info.AdapterName.Contains("Radeon", StringComparison.OrdinalIgnoreCase)))
                    {
                        _adapterIndex = info.AdapterIndex;
                        _adapterName = string.IsNullOrWhiteSpace(info.AdapterName) ? "AMD Radeon Instinct MI50 / PRO VII" : info.AdapterName;
                        LogService.Instance.Success("AMD ADL", $"Đã khớp GPU: '{_adapterName}' tại Index {_adapterIndex}");
                        break;
                    }
                }
                Marshal.FreeHGlobal(ptr);

                if (_adapterIndex < 0) return false;

                // Allocate buffer for ADL2_New_QueryPMLogData_Get
                _pQueryBuffer = Marshal.AllocHGlobal(4096);
                Marshal.WriteInt32(_pQueryBuffer, 0, 4096);

                // Initialize PMLog Device
                int rDev = ADL2_Device_PMLog_Device_Create(_context, _adapterIndex, out _device);
                if (rDev == 0)
                {
                    ADLPMLogStartInput startInput = new ADLPMLogStartInput
                    {
                        usSensors = new ushort[256],
                        ulSampleRate = 500,
                        iReserved = new int[256]
                    };
                    for (int s = 0; s < 256; s++) startInput.usSensors[s] = (ushort)s;

                    ADL2_Adapter_PMLog_Start(_context, _adapterIndex, ref startInput, out ADLPMLogStartOutput startOutput, _device);
                    _pLoggingAddress = startOutput.pLoggingAddress;
                }

                _isInitialized = true;
                LogService.Instance.Success("AMD ADL", "AMD ADL Telemetry Engine đã sẵn sàng.");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("AMD ADL", $"Lỗi ADL: {ex.Message}");
                return false;
            }
        }

        [StructLayout(LayoutKind.Sequential)]
        public struct ADLTemperature
        {
            public int iSize;
            public int iTemperature;
        }

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Overdrive5_Temperature_Get(IntPtr context, int adapterIndex, int thermalControllerIndex, out ADLTemperature temperature);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Overdrive6_Temperature_Get(IntPtr context, int adapterIndex, out int temperature);

        public AmdGpuTelemetryData ReadTelemetry()
        {
            var data = new AmdGpuTelemetryData
            {
                GpuName = _adapterName,
                IsAvailable = _isInitialized && _context != IntPtr.Zero
            };

            if (!data.IsAvailable) return data;

            try
            {
                int rawCore = 0;
                int rawMem = 0;
                int rawGfxClock = 0;
                int rawMemClock = 0;
                int rawActivity = 0;

                // Query live sensor buffer via PMLog
                if (_pQueryBuffer != IntPtr.Zero)
                {
                    Marshal.WriteInt32(_pQueryBuffer, 0, 4096);
                    int qRet = ADL2_New_QueryPMLogData_Get(_context, _adapterIndex, _pQueryBuffer);
                    if (qRet == 0)
                    {
                        rawGfxClock = Marshal.ReadInt32(_pQueryBuffer, 8 + 1 * 8); // PMLOG_CLK_GFXCLK
                        rawMemClock = Marshal.ReadInt32(_pQueryBuffer, 8 + 2 * 8); // PMLOG_CLK_MEMCLK
                        rawCore = Marshal.ReadInt32(_pQueryBuffer, 8 + 8 * 8);     // PMLOG_TEMPERATURE_EDGE (GPU Temp)
                        rawMem = Marshal.ReadInt32(_pQueryBuffer, 8 + 9 * 8);      // PMLOG_TEMPERATURE_MEM (HBM Temp)
                        rawActivity = Marshal.ReadInt32(_pQueryBuffer, 8 + 21 * 8); // PMLOG_INFO_ACTIVITY_GFX
                    }
                }

                // Fallback to PMLog Shared Address if Query returned 0
                if (rawCore == 0 && _pLoggingAddress != IntPtr.Zero)
                {
                    rawGfxClock = Marshal.ReadInt32(_pLoggingAddress, 8 + 1 * 8);
                    rawMemClock = Marshal.ReadInt32(_pLoggingAddress, 8 + 2 * 8);
                    rawCore = Marshal.ReadInt32(_pLoggingAddress, 8 + 8 * 8);
                    rawMem = Marshal.ReadInt32(_pLoggingAddress, 8 + 9 * 8);
                    rawActivity = Marshal.ReadInt32(_pLoggingAddress, 8 + 21 * 8);
                }

                // Fallback 1: ADL2_Overdrive6_Temperature_Get
                if (rawCore <= 0 && _context != IntPtr.Zero && _adapterIndex >= 0)
                {
                    try
                    {
                        int od6Ret = ADL2_Overdrive6_Temperature_Get(_context, _adapterIndex, out int od6Temp);
                        if (od6Ret == 0 && od6Temp > 0)
                        {
                            rawCore = od6Temp > 1000 ? (od6Temp / 1000) : od6Temp;
                        }
                    }
                    catch { }
                }

                // Fallback 2: ADL2_Overdrive5_Temperature_Get
                if (rawCore <= 0 && _context != IntPtr.Zero && _adapterIndex >= 0)
                {
                    try
                    {
                        var od5Temp = new ADLTemperature { iSize = Marshal.SizeOf(typeof(ADLTemperature)) };
                        int od5Ret = ADL2_Overdrive5_Temperature_Get(_context, _adapterIndex, 0, out od5Temp);
                        if (od5Ret == 0 && od5Temp.iTemperature > 0)
                        {
                            rawCore = od5Temp.iTemperature > 1000 ? (od5Temp.iTemperature / 1000) : od5Temp.iTemperature;
                        }
                    }
                    catch { }
                }

                // 1. Chuẩn hóa nhiệt độ Core GPU
                float coreTemp = (rawCore >= 15 && rawCore <= 120) ? rawCore : _lastValidCore;
                if (coreTemp > 0) _lastValidCore = coreTemp;

                float memTemp = (rawMem >= 15 && rawMem <= 120) ? rawMem : _lastValidMem;
                if (memTemp > 0) _lastValidMem = memTemp;

                data.CoreTemperature = coreTemp;
                data.MemoryTemperature = memTemp;
                data.GpuClockMhz = Math.Clamp(rawGfxClock, 0, 3000);
                data.VramClockMhz = Math.Clamp(rawMemClock, 0, 3000);
                data.GpuActivityPercent = Math.Clamp(rawActivity, 0, 100);
            }
            catch (Exception ex)
            {
                LogService.Instance.Debug("AMD ADL", $"ReadTelemetry error: {ex.Message}");
                data.IsAvailable = false;
            }

            return data;
        }

        public bool SetGpuFanSpeedPercent(float percent)
        {
            if (_context == IntPtr.Zero || _adapterIndex < 0) return false;

            try
            {
                var fanSpeed = new ADLFanSpeedValue
                {
                    iSize = Marshal.SizeOf(typeof(ADLFanSpeedValue)),
                    iSpeedType = 1,
                    iFanSpeed = (int)Math.Clamp(percent, 0f, 100f),
                    iFlags = 1
                };

                int ret = ADL2_Overdrive5_FanSpeed_Set(_context, _adapterIndex, 0, ref fanSpeed);
                return ret == 0;
            }
            catch
            {
                return false;
            }
        }

        public void RestoreGpuFanDefault()
        {
            if (_context == IntPtr.Zero || _adapterIndex < 0) return;
            try
            {
                ADL2_Overdrive5_FanSpeedToDefault_Set(_context, _adapterIndex, 0);
            }
            catch { }
        }

        public void Dispose()
        {
            RestoreGpuFanDefault();

            if (_pQueryBuffer != IntPtr.Zero)
            {
                try { Marshal.FreeHGlobal(_pQueryBuffer); } catch { }
                _pQueryBuffer = IntPtr.Zero;
            }

            if (_isInitialized && _context != IntPtr.Zero)
            {
                try
                {
                    if (_adapterIndex >= 0 && _device != 0)
                    {
                        ADL2_Adapter_PMLog_Stop(_context, _adapterIndex, _device);
                        ADL2_Device_PMLog_Device_Destroy(_context, _device);
                    }
                }
                catch { }

                try
                {
                    ADL2_Main_Control_Destroy(_context);
                }
                catch { }

                _context = IntPtr.Zero;
                _isInitialized = false;
            }
        }
    }
}
