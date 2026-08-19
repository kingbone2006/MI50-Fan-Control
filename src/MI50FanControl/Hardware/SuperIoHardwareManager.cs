using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
using LibreHardwareMonitor.Hardware;
using MI50FanControl.Services;

namespace MI50FanControl.Hardware
{
    public class HardwareFanItem
    {
        public string Id { get; set; } = string.Empty;
        public string HardwareName { get; set; } = string.Empty;
        public string SensorName { get; set; } = string.Empty;
        public string Identifier { get; set; } = string.Empty;
        public int Index { get; set; }
        public float LiveRpm { get; set; }
        public float CurrentPwmPercent { get; set; } = 50;
        public bool HasControl { get; set; } = true;
        public int DirectChannelIndex { get; set; } = -1;
        public ISensor? LhmFanSensor { get; set; }
        public ISensor? LhmControlSensor { get; set; }
    }

    public class SuperIoHardwareManager : IDisposable
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll", SetLastError = true)]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        private const uint FILE_MAP_READ = 0x0004;
        private const int SW_HIDE = 0;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_HIDEWINDOW = 0x0080;

        private const uint WM_SETTEXT = 0x000C;
        private const uint WM_COMMAND = 0x0111;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const int EN_CHANGE = 0x0300;
        private const int VK_RETURN = 0x0D;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        private struct SpeedFanSharedMem
        {
            public ushort Version;
            public ushort Flags;
            public int MemSize;
            public int Handle;
            public ushort NumTemps;
            public ushort NumFans;
            public ushort NumVolts;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] Temps;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] Fans;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] Volts;
        }

        private readonly List<HardwareFanItem> _activeFans = new();
        private readonly DirectSuperIoDriver _directDriver = new();
        private readonly Dictionary<int, int> _lastWrittenChannelPwm = new();

        private Computer? _lhmComputer;
        private IntPtr _hMap = IntPtr.Zero;
        private IntPtr _pView = IntPtr.Zero;

        private string _motherboardName = "Intel / AMD Motherboard";
        private string _superIoChipName = "Universal SuperIO Controller";
        private bool _isInitialized = false;
        private bool _disposed = false;

        public string MotherboardName => _motherboardName;
        public string SuperIoChipName => _superIoChipName;
        public IReadOnlyList<HardwareFanItem> ActiveFans => _activeFans;
        public bool IsInitialized => _isInitialized;
        public DirectSuperIoDriver DirectDriver => _directDriver;


        public bool Initialize()
        {
            LogService.Instance.Hardware("SuperIO", "Bắt đầu khởi tạo hệ thống quản lý phần cứng SuperIO & Fan Engine (v3.0)...");

            try
            {
                // 1. Nhận diện Bo Mạch Chủ từ WMI
                DetectMotherboardInfo();

                // 2. Thử nghiệm Dynamic LPC Port Direct Probe (ITE, Nuvoton, Fintek, Winbond)
                try
                {
                    _directDriver.Initialize();
                }
                catch (Exception ex)
                {
                    LogService.Instance.Warn("SuperIO", $"Direct driver init: {ex.Message}");
                }

                // 3. Khởi tạo LibreHardwareMonitor Motherboard Engine
                InitializeLibreHardwareMonitor();

                // 4. Khởi chạy SuperIO Engine ngầm điều khiển quạt
                EnsureSuperIoEngineRunning();
                StartHiderThread();
                ConnectSharedMemoryWithRetry(timeoutSeconds: 4);

                // 5. Nhận diện ĐỘNG 100% chip SuperIO trên máy (Không hardcode bất kỳ chip nào)
                DetectSuperIoChipName();


                // 7. Nạp danh sách các quạt vật lý đang hoạt động
                RefreshActiveFans();

                _isInitialized = true;
                LogService.Instance.Success("SuperIO", $"Khởi tạo hoàn tất. Bo mạch: '{_motherboardName}', Chip IO: '{_superIoChipName}', Số cổng quạt nhận diện: {_activeFans.Count}");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("SuperIO", $"Lỗi khởi tạo SuperIO Hardware Manager: {ex.Message}");
                _isInitialized = false;
                return false;
            }
        }

        private void InitializeLibreHardwareMonitor()
        {
            try
            {
                _lhmComputer = new Computer
                {
                    IsMotherboardEnabled = true,
                    IsControllerEnabled = true,
                    IsCpuEnabled = false,
                    IsGpuEnabled = false
                };
                _lhmComputer.Open();
                LogService.Instance.Success("SuperIO", "Đã nạp thành công LibreHardwareMonitor Motherboard Subsystem.");
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn("SuperIO", $"Không thể nạp LHM Motherboard: {ex.Message}");
            }
        }

        private void DetectMotherboardInfo()
        {
            try
            {
                string mfg = string.Empty;
                string prod = string.Empty;

                using (var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product, Version FROM Win32_BaseBoard"))
                {
                    foreach (var obj in searcher.Get())
                    {
                        mfg = obj["Manufacturer"]?.ToString()?.Trim() ?? "";
                        prod = obj["Product"]?.ToString()?.Trim() ?? "";
                        break;
                    }
                }

                string[] genericStrings = { "Default string", "To be filled by O.E.M.", "System manufacturer", "Base Board" };
                bool isProdGeneric = string.IsNullOrWhiteSpace(prod) || genericStrings.Any(g => prod.Equals(g, StringComparison.OrdinalIgnoreCase));
                bool isMfgGeneric = string.IsNullOrWhiteSpace(mfg) || genericStrings.Any(g => mfg.Equals(g, StringComparison.OrdinalIgnoreCase));

                if (isProdGeneric)
                {
                    using var csSearcher = new ManagementObjectSearcher("SELECT Manufacturer, Model FROM Win32_ComputerSystem");
                    foreach (var obj in csSearcher.Get())
                    {
                        string csMfg = obj["Manufacturer"]?.ToString()?.Trim() ?? "";
                        string csModel = obj["Model"]?.ToString()?.Trim() ?? "";
                        if (!string.IsNullOrWhiteSpace(csModel) && !genericStrings.Any(g => csModel.Equals(g, StringComparison.OrdinalIgnoreCase)))
                        {
                            prod = csModel;
                            if (isMfgGeneric && !string.IsNullOrWhiteSpace(csMfg)) mfg = csMfg;
                        }
                        break;
                    }
                }

                if (!string.IsNullOrWhiteSpace(prod) && !genericStrings.Any(g => prod.Equals(g, StringComparison.OrdinalIgnoreCase)))
                {
                    if (!isMfgGeneric && !prod.StartsWith(mfg, StringComparison.OrdinalIgnoreCase))
                    {
                        _motherboardName = $"{mfg} {prod}";
                    }
                    else
                    {
                        _motherboardName = prod;
                    }
                }
                else
                {
                    _motherboardName = !isMfgGeneric ? $"{mfg} Motherboard" : "Standard PC Motherboard";
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn("SuperIO", $"Lỗi đọc thông tin bo mạch chủ: {ex.Message}");
                _motherboardName = "Intel / AMD Motherboard";
            }
        }

        private void DetectSuperIoChipName()
        {
            // ƯU TIÊN 1: Chip nhận diện trực tiếp qua quét cổng LPC (Direct SuperIO Driver)
            if (_directDriver.IsDetected && !string.IsNullOrWhiteSpace(_directDriver.ChipName))
            {
                _superIoChipName = $"{_directDriver.ChipName} (SuperIO)";
                return;
            }

            // ƯU TIÊN 2: Chip nhận diện qua LibreHardwareMonitor Motherboard SubHardware
            if (_lhmComputer != null)
            {
                try
                {
                    foreach (var hw in _lhmComputer.Hardware)
                    {
                        if (hw.HardwareType == HardwareType.Motherboard)
                        {
                            hw.Update();
                            foreach (var sub in hw.SubHardware)
                            {
                                if (sub.HardwareType == HardwareType.SuperIO ||
                                    sub.HardwareType == HardwareType.EmbeddedController)
                                {
                                    string cleanName = sub.Name.Trim();
                                    if (!string.IsNullOrWhiteSpace(cleanName))
                                    {
                                        _superIoChipName = $"{cleanName} (SuperIO)";
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // ƯU TIÊN 3: Nhận diện từ file cấu hình cảm biến engine nếu có
            try
            {
                string[] candidates = new[]
                {
                    Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Engine", "speedfansens.cfg"),
                    Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MI50FanControl", "Engine", "speedfansens.cfg"),
                    @"C:\Program Files\MI50FanControl\Engine\speedfansens.cfg"
                };

                foreach (var path in candidates)
                {
                    if (File.Exists(path))
                    {
                        string[] lines = File.ReadAllLines(path);
                        foreach (var line in lines)
                        {
                            if (line.Contains("UniqueID=") && line.Contains("(onISA@"))
                            {
                                int eq = line.IndexOf("UniqueID=");
                                int at = line.IndexOf('@', eq);
                                if (eq > 0 && at > eq)
                                {
                                    string rawName = line.Substring(eq + 9, at - (eq + 9)).Trim();
                                    if (!rawName.Equals("INTEL CORE", StringComparison.OrdinalIgnoreCase) &&
                                        !rawName.Equals("ISA", StringComparison.OrdinalIgnoreCase) &&
                                        !string.IsNullOrWhiteSpace(rawName))
                                    {
                                        _superIoChipName = rawName.StartsWith("IT", StringComparison.OrdinalIgnoreCase) ? $"ITE {rawName} (SuperIO)" : $"{rawName} (SuperIO)";
                                        return;
                                    }
                                }
                            }
                        }
                    }
                }
            }
            catch { }

            // ƯU TIÊN 4: Đặt tên động theo chuẩn chung của bo mạch chủ đã phát hiện (KHÔNG hardcode ITE8772)
            if (!string.IsNullOrWhiteSpace(_motherboardName) && !_motherboardName.Contains("Standard"))
            {
                _superIoChipName = $"{_motherboardName} SuperIO / EC";
            }
            else
            {
                _superIoChipName = "Universal Motherboard SuperIO Controller";
            }
        }

        public void RefreshActiveFans()
        {
            var currentFans = new List<HardwareFanItem>();

            // 1. Quét quạt từ Shared Memory
            if (_pView != IntPtr.Zero)
            {
                try
                {
                    var data = Marshal.PtrToStructure<SpeedFanSharedMem>(_pView);
                    int count = (int)data.NumFans;

                    for (int f = 0; f < count; f++)
                    {
                        int rpm = data.Fans[f];
                        var item = new HardwareFanItem
                        {
                            Id = $"Fan_{f + 1}",
                            Identifier = $"Fan_{f + 1}",
                            HardwareName = _superIoChipName,
                            SensorName = $"Cổng Quạt #{f + 1}",
                            Index = f,
                            DirectChannelIndex = f,
                            LiveRpm = rpm,
                            CurrentPwmPercent = 50,
                            HasControl = true
                        };
                        currentFans.Add(item);
                    }
                }
                catch { }
            }

            // 2. Quét quạt từ LibreHardwareMonitor (nếu có sensor)
            if (currentFans.Count == 0 && _lhmComputer != null)
            {
                try
                {
                    int fIndex = 0;
                    foreach (var hw in _lhmComputer.Hardware)
                    {
                        if (hw.HardwareType == HardwareType.Motherboard)
                        {
                            hw.Update();
                            foreach (var sub in hw.SubHardware)
                            {
                                sub.Update();
                                var fanSensors = sub.Sensors.Where(s => s.SensorType == SensorType.Fan).ToList();
                                var ctrlSensors = sub.Sensors.Where(s => s.SensorType == SensorType.Control).ToList();

                                for (int i = 0; i < fanSensors.Count; i++)
                                {
                                    var fanSensor = fanSensors[i];
                                    var ctrlSensor = i < ctrlSensors.Count ? ctrlSensors[i] : null;

                                    var item = new HardwareFanItem
                                    {
                                        Id = $"Fan_{fIndex + 1}",
                                        Identifier = fanSensor.Identifier?.ToString() ?? $"Fan_{fIndex + 1}",
                                        HardwareName = sub.Name,
                                        SensorName = !string.IsNullOrWhiteSpace(fanSensor.Name) ? fanSensor.Name : $"Cổng Quạt #{fIndex + 1}",
                                        Index = fIndex,
                                        DirectChannelIndex = fIndex,
                                        LiveRpm = fanSensor.Value ?? 0,
                                        CurrentPwmPercent = 50,
                                        HasControl = ctrlSensor?.Control != null,
                                        LhmFanSensor = fanSensor,
                                        LhmControlSensor = ctrlSensor
                                    };
                                    currentFans.Add(item);
                                    fIndex++;
                                }
                            }
                        }
                    }
                }
                catch { }
            }

            // 3. Quét quạt từ Direct Driver
            if (currentFans.Count == 0 && _directDriver.IsDetected && _directDriver.Fans.Count > 0)
            {
                int fIndex = 0;
                foreach (var df in _directDriver.Fans)
                {
                    var item = new HardwareFanItem
                    {
                        Id = $"Fan_{fIndex + 1}",
                        Identifier = $"Direct_Fan_{fIndex + 1}",
                        HardwareName = _superIoChipName,
                        SensorName = df.Name,
                        Index = fIndex,
                        DirectChannelIndex = df.ChannelIndex,
                        LiveRpm = df.LiveRpm,
                        CurrentPwmPercent = df.CurrentPwmPercent,
                        HasControl = true
                    };
                    currentFans.Add(item);
                    fIndex++;
                }
            }

            // 4. Default Fallback nếu chưa nhận diện được
            if (currentFans.Count == 0)
            {
                for (int f = 0; f < 2; f++)
                {
                    var item = new HardwareFanItem
                    {
                        Id = $"Fan_{f + 1}",
                        Identifier = $"Fan_{f + 1}",
                        HardwareName = _superIoChipName,
                        SensorName = $"Cổng Quạt #{f + 1}",
                        Index = f,
                        DirectChannelIndex = f,
                        LiveRpm = 0,
                        CurrentPwmPercent = 50,
                        HasControl = true
                    };
                    currentFans.Add(item);
                }
            }

            _activeFans.Clear();
            _activeFans.AddRange(currentFans);
        }

        public void UpdateLiveFans()
        {
            // Cập nhật từ Direct LPC
            if (_directDriver.IsDetected)
            {
                _directDriver.UpdateTelemetry();
            }

            // Cập nhật từ LHM
            if (_lhmComputer != null)
            {
                try
                {
                    foreach (var hw in _lhmComputer.Hardware)
                    {
                        if (hw.HardwareType == HardwareType.Motherboard)
                        {
                            hw.Update();
                            foreach (var sub in hw.SubHardware)
                            {
                                sub.Update();
                            }
                        }
                    }
                }
                catch { }
            }

            // Cập nhật giá trị vào danh sách _activeFans
            if (_pView != IntPtr.Zero)
            {
                try
                {
                    var data = Marshal.PtrToStructure<SpeedFanSharedMem>(_pView);
                    for (int f = 0; f < _activeFans.Count; f++)
                    {
                        var fan = _activeFans[f];
                        if (f < data.NumFans && data.Fans[f] > 0)
                        {
                            fan.LiveRpm = data.Fans[f];
                        }
                        else if (fan.LhmFanSensor != null && (fan.LhmFanSensor.Value ?? 0) > 0)
                        {
                            fan.LiveRpm = fan.LhmFanSensor.Value ?? 0;
                        }
                        else if (f < _directDriver.Fans.Count && _directDriver.IsDetected && _directDriver.Fans[f].LiveRpm > 0)
                        {
                            fan.LiveRpm = _directDriver.Fans[f].LiveRpm;
                        }
                        else if (f < data.NumFans)
                        {
                            fan.LiveRpm = data.Fans[f];
                        }
                    }
                }
                catch { }
            }
            else
            {
                for (int f = 0; f < _activeFans.Count; f++)
                {
                    var fan = _activeFans[f];
                    if (fan.LhmFanSensor != null)
                    {
                        fan.LiveRpm = fan.LhmFanSensor.Value ?? 0;
                    }
                    else if (f < _directDriver.Fans.Count && _directDriver.IsDetected)
                    {
                        fan.LiveRpm = _directDriver.Fans[f].LiveRpm;
                    }
                }
            }
        }

        public bool SetFanPwm(int fanIndex, float pwmPercent)
        {
            float clamped = Math.Clamp(pwmPercent, 0f, 100f);
            int targetInt = (int)Math.Round(clamped);

            if (fanIndex >= 0 && fanIndex < _activeFans.Count)
            {
                var fan = _activeFans[fanIndex];
                fan.CurrentPwmPercent = clamped;

                // 1. Ghi qua Background SuperIO Engine
                int channelIdx = fan.DirectChannelIndex >= 0 ? fan.DirectChannelIndex : fanIndex;
                SetChannelPwm(channelIdx, targetInt);

                // 2. Ghi qua LHM Control Sensor nếu có
                if (fan.LhmControlSensor?.Control != null)
                {
                    try
                    {
                        fan.LhmControlSensor.Control.SetSoftware(clamped);
                    }
                    catch { }
                }

                // 3. Ghi qua Direct SuperIO Driver
                if (_directDriver.IsDetected)
                {
                    _directDriver.SetFanPwm(channelIdx, clamped);
                }

                return true;
            }

            return false;
        }

        public void SetAllFansPwm(float pwmPercent)
        {
            float clamped = Math.Clamp(pwmPercent, 0f, 100f);
            int targetInt = (int)Math.Round(clamped);

            SetChannelPwm(-1, targetInt);

            for (int i = 0; i < _activeFans.Count; i++)
            {
                var fan = _activeFans[i];
                fan.CurrentPwmPercent = clamped;

                if (fan.LhmControlSensor?.Control != null)
                {
                    try
                    {
                        fan.LhmControlSensor.Control.SetSoftware(clamped);
                    }
                    catch { }
                }
            }

            if (_directDriver.IsDetected)
            {
                _directDriver.SetAllFansPwm(clamped);
            }
        }

        public void SetChannelPwm(int channelIndex, int percent)
        {
            percent = Math.Clamp(percent, 0, 100);
            if (_lastWrittenChannelPwm.TryGetValue(channelIndex, out int last) && last == percent)
            {
                return;
            }
            _lastWrittenChannelPwm[channelIndex] = percent;

            var procs = Process.GetProcessesByName("speedfan");
            if (procs.Length == 0) return;
            uint sfPid = (uint)procs[0].Id;

            EnumWindows((hWnd, l) =>
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == sfPid)
                {
                    int currentEditIdx = 0;
                    EnumChildWindows(hWnd, (child, param) =>
                    {
                        StringBuilder cCls = new StringBuilder(256);
                        GetClassName(child, cCls, 256);
                        string clsName = cCls.ToString();

                        if (clsName == "TEdit" || clsName.Contains("Edit"))
                        {
                            if (channelIndex < 0 || currentEditIdx == channelIndex)
                            {
                                SendMessage(child, WM_SETTEXT, IntPtr.Zero, percent.ToString());
                                IntPtr wParam = (IntPtr)((EN_CHANGE << 16) | (child.ToInt32() & 0xFFFF));
                                SendMessage(hWnd, WM_COMMAND, wParam, child);
                                SendMessage(child, WM_KEYDOWN, (IntPtr)VK_RETURN, IntPtr.Zero);
                                SendMessage(child, WM_KEYUP, (IntPtr)VK_RETURN, IntPtr.Zero);
                            }
                            currentEditIdx++;
                        }
                        return true;
                    }, IntPtr.Zero);
                }
                return true;
            }, IntPtr.Zero);
        }

        public void UpdateTelemetry() => UpdateLiveFans();

        public void SetAllFansSpeed(float speedPercent) => SetAllFansPwm(speedPercent);
        public bool SetFanSpeed(int fanIndex, float speedPercent) => SetFanPwm(fanIndex, speedPercent);

        public bool SetFanSpeed(string fanId, float speedPercent)
        {
            var fan = _activeFans.FirstOrDefault(f => f.Id == fanId || f.Identifier == fanId);
            if (fan == null) return false;
            return SetFanPwm(fan.Index, speedPercent);
        }

        public void RestoreBiosControl(string? fanId = null) => RestoreBiosDefault();
        public void RestoreBiosControl() => RestoreBiosDefault();

        public void RestoreBiosDefault()
        {
            if (_directDriver.IsDetected)
            {
                _directDriver.RestoreBiosDefault();
            }

            if (_lhmComputer != null)
            {
                try
                {
                    foreach (var fan in _activeFans)
                    {
                        if (fan.LhmControlSensor?.Control != null)
                        {
                            fan.LhmControlSensor.Control.SetDefault();
                        }
                    }
                }
                catch { }
            }
        }

        private void EnsureSuperIoEngineRunning()
        {
            EnsureDriverServiceInstalled();

            var procs = Process.GetProcessesByName("speedfan");
            if (procs.Length > 0)
            {
                HideAllSpeedFanWindows();
                return;
            }

            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string engineExe = Path.Combine(appDir, "Engine", "speedfan.exe");

            if (!File.Exists(engineExe))
            {
                engineExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MI50FanControl", "Engine", "speedfan.exe");
            }

            if (File.Exists(engineExe))
            {
                string engineDir = Path.GetDirectoryName(engineExe)!;
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = engineExe,
                        Arguments = "/NOSMARTSCAN /NOSMBSCAN /NOPCISCAN /MINIMIZED",
                        WorkingDirectory = engineDir,
                        UseShellExecute = true,
                        WindowStyle = ProcessWindowStyle.Minimized
                    };
                    Process.Start(psi);
                }
                catch { }
            }
        }

        private void EnsureDriverServiceInstalled()
        {
            try
            {
                string sysSysWOW64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64", "speedfan.sys");
                string sysDrivers = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "System32", "drivers", "speedfan.sys");

                string appDir = AppDomain.CurrentDomain.BaseDirectory;
                string bundledSys = Path.Combine(appDir, "Engine", "speedfan.sys");

                if (!File.Exists(sysSysWOW64) && File.Exists(bundledSys))
                {
                    try { File.Copy(bundledSys, sysSysWOW64, true); } catch { }
                }

                string targetSys = File.Exists(sysSysWOW64) ? sysSysWOW64 : (File.Exists(sysDrivers) ? sysDrivers : bundledSys);

                var psiCreate = new ProcessStartInfo("sc.exe", $"create speedfan type= kernel start= auto binPath= \"\\??\\{targetSys}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psiCreate)?.WaitForExit(1500);

                var psiStart = new ProcessStartInfo("sc.exe", "start speedfan")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psiStart)?.WaitForExit(1500);
            }
            catch { }
        }

        private void StartHiderThread()
        {
            var t = new Thread(() =>
            {
                for (int i = 0; i < 40; i++)
                {
                    if (_disposed) break;
                    HideAllSpeedFanWindows();
                    Thread.Sleep(150);
                }
            })
            {
                IsBackground = true
            };
            t.Start();
        }

        private void HideAllSpeedFanWindows()
        {
            try
            {
                var procs = Process.GetProcessesByName("speedfan");
                if (procs.Length == 0) return;
                uint sfPid = (uint)procs[0].Id;

                EnumWindows((hWnd, l) =>
                {
                    GetWindowThreadProcessId(hWnd, out uint pid);
                    if (pid == sfPid)
                    {
                        SetWindowPos(hWnd, IntPtr.Zero, -32000, -32000, 0, 0, SWP_NOZORDER | SWP_HIDEWINDOW);
                        ShowWindow(hWnd, SW_HIDE);
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
        }

        private void ConnectSharedMemoryWithRetry(int timeoutSeconds)
        {
            var sw = Stopwatch.StartNew();
            while (sw.Elapsed.TotalSeconds < timeoutSeconds)
            {
                if (_hMap == IntPtr.Zero)
                {
                    _hMap = OpenFileMapping(FILE_MAP_READ, false, "SFSharedMemory_ALM");
                    if (_hMap == IntPtr.Zero) _hMap = OpenFileMapping(FILE_MAP_READ, false, @"Global\SFSharedMemory_ALM");
                }

                if (_hMap != IntPtr.Zero)
                {
                    if (_pView == IntPtr.Zero)
                    {
                        _pView = MapViewOfFile(_hMap, FILE_MAP_READ, 0, 0, UIntPtr.Zero);
                    }

                    if (_pView != IntPtr.Zero)
                    {
                        return;
                    }
                }
                Thread.Sleep(200);
            }
        }

        public void Dispose()
        {
            _disposed = true;
            RestoreBiosDefault();

            try
            {
                _directDriver.Dispose();
                _lhmComputer?.Close();
            }
            catch { }


            if (_pView != IntPtr.Zero)
            {
                UnmapViewOfFile(_pView);
                _pView = IntPtr.Zero;
            }

            if (_hMap != IntPtr.Zero)
            {
                CloseHandle(_hMap);
                _hMap = IntPtr.Zero;
            }
        }
    }
}
