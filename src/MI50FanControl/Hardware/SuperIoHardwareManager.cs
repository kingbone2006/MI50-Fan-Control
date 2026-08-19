using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Management;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;
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

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint FILE_MAP_READ = 0x0004;
        private const uint WM_SETTEXT = 0x000C;
        private const uint WM_COMMAND = 0x0111;
        private const uint EN_CHANGE = 0x0300;
        private const uint UDM_SETPOS32 = 0x0471;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint VK_RETURN = 0x0D;
        private const int SW_HIDE = 0;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_HIDEWINDOW = 0x0080;

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
        private IntPtr _hMap = IntPtr.Zero;
        private IntPtr _pView = IntPtr.Zero;
        private string _motherboardName = "Intel X99 Motherboard";
        private string _superIoChipName = "Universal SuperIO Fan Controller";
        private bool _isInitialized = false;
        private bool _disposed = false;

        public string MotherboardName => _motherboardName;
        public string SuperIoChipName => _superIoChipName;
        public IReadOnlyList<HardwareFanItem> ActiveFans => _activeFans;
        public bool IsInitialized => _isInitialized;

        public bool Initialize()
        {
            LogService.Instance.Hardware("SuperIO", "Bắt đầu khởi tạo lõi điều khiển quạt phần cứng...");

            try
            {
                // 1. Tự động khởi chạy lõi phần cứng portable được đóng gói sẵn trong ứng dụng
                EnsurePortableEngineRunning();

                // 2. Chạy luồng ẩn cửa sổ liên tục
                StartHiderThread();

                // 3. Chờ cảm biến nạp xong và kết nối Shared Memory
                ConnectSharedMemoryWithRetry(timeoutSeconds: 10);

                // 4. Lấy tên bo mạch chủ từ WMI và tên Chip SuperIO từ SpeedFan
                DetectMotherboardInfo();
                DetectSuperIoChipName();

                // 5. Quét và nạp danh sách quạt thực tế
                RefreshActiveFans();

                _isInitialized = true;
                LogService.Instance.Success("SuperIO", $"Khởi tạo hoàn tất. Đã nhận diện {_activeFans.Count} cổng quạt.");
                return true;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("SuperIO", $"Lỗi khởi tạo SuperIO: {ex.Message}");
                _isInitialized = false;
                return false;
            }
        }

        private void EnsurePortableEngineRunning()
        {
            EnsureDriverServiceInstalled();

            var procs = Process.GetProcessesByName("speedfan");
            if (procs.Length > 0)
            {
                HideAllSpeedFanWindows();
                return;
            }

            // Tìm file engine được đóng gói sẵn trong thư mục ứng dụng
            string appDir = AppDomain.CurrentDomain.BaseDirectory;
            string engineExe = Path.Combine(appDir, "Engine", "speedfan.exe");

            if (!File.Exists(engineExe))
            {
                engineExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles), "MI50FanControl", "Engine", "speedfan.exe");
            }

            if (!File.Exists(engineExe))
            {
                engineExe = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86), "SpeedFan", "speedfan.exe");
            }

            if (File.Exists(engineExe))
            {
                string engineDir = Path.GetDirectoryName(engineExe)!;
                string cfgPath = Path.Combine(engineDir, "speedfanparams.cfg");
                ConfigureSilentFile(cfgPath);

                LogService.Instance.Info("Engine", $"Kích hoạt lõi điều khiển chạy ngầm: {engineExe}");
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
                catch (Exception ex)
                {
                    LogService.Instance.Warn("Engine", $"Không thể start engine: {ex.Message}");
                }
            }
            else
            {
                LogService.Instance.Error("Engine", $"Không tìm thấy file lõi tại: {engineExe}");
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
            catch (Exception ex)
            {
                LogService.Instance.Warn("Engine", $"EnsureDriverService error: {ex.Message}");
            }
        }

        private void ConfigureSilentFile(string cfgPath)
        {
            try
            {
                if (File.Exists(cfgPath))
                {
                    string text = File.ReadAllText(cfgPath);
                    bool mod = false;
                    if (!text.Contains("StartupHide=true")) { text = text.Replace("StartupHide=false", "StartupHide=true"); mod = true; }
                    if (!text.Contains("MinimizeOnClose=true")) { text = text.Replace("MinimizeOnClose=false", "MinimizeOnClose=true"); mod = true; }
                    if (!text.Contains("ShowStaticIcon=false")) { text = text.Replace("ShowStaticIcon=true", "ShowStaticIcon=false"); mod = true; }
                    if (mod) File.WriteAllText(cfgPath, text);
                }
            }
            catch { }
        }

        private void StartHiderThread()
        {
            var t = new Thread(() =>
            {
                for (int i = 0; i < 50; i++)
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
                        var data = Marshal.PtrToStructure<SpeedFanSharedMem>(_pView);
                        if (data.NumFans > 0)
                        {
                            return;
                        }
                    }
                }
                Thread.Sleep(300);
            }
        }

        private void DetectMotherboardInfo()
        {
            try
            {
                using var searcher = new ManagementObjectSearcher("SELECT Manufacturer, Product FROM Win32_BaseBoard");
                foreach (var obj in searcher.Get())
                {
                    string mfg = obj["Manufacturer"]?.ToString()?.Trim() ?? "";
                    string prod = obj["Product"]?.ToString()?.Trim() ?? "";
                    if (!string.IsNullOrEmpty(prod) && prod != "Default string")
                    {
                        _motherboardName = string.IsNullOrEmpty(mfg) || mfg == "Default string" ? prod : $"{mfg} {prod}";
                    }
                    break;
                }
            }
            catch { }
        }

        private void DetectSuperIoChipName()
        {
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
                            if (line.Contains(" from ") && line.Contains("(onISA@"))
                            {
                                int fromIdx = line.IndexOf(" from ");
                                int atIdx = line.IndexOf('@', fromIdx);
                                if (fromIdx > 0 && atIdx > fromIdx)
                                {
                                    string rawName = line.Substring(fromIdx + 6, atIdx - (fromIdx + 6)).Trim();
                                    if (!rawName.Equals("INTEL CORE", StringComparison.OrdinalIgnoreCase) &&
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

            _superIoChipName = "ITE IT8772F (SuperIO)";
        }

        public void RefreshActiveFans()
        {
            var currentFans = new List<HardwareFanItem>();

            if (_pView != IntPtr.Zero)
            {
                var data = Marshal.PtrToStructure<SpeedFanSharedMem>(_pView);
                int count = Math.Max(1, (int)data.NumFans);

                for (int f = 0; f < count; f++)
                {
                    var item = new HardwareFanItem
                    {
                        Id = $"Fan_{f + 1}",
                        Identifier = $"Fan_{f + 1}",
                        HardwareName = _motherboardName,
                        SensorName = $"Cổng Quạt #{f + 1}",
                        Index = f,
                        DirectChannelIndex = f,
                        LiveRpm = f < data.NumFans ? data.Fans[f] : 0,
                        CurrentPwmPercent = 50,
                        HasControl = true
                    };
                    currentFans.Add(item);
                }
            }

            if (currentFans.Count == 0)
            {
                for (int f = 0; f < 2; f++)
                {
                    var item = new HardwareFanItem
                    {
                        Id = $"Fan_{f + 1}",
                        Identifier = $"Fan_{f + 1}",
                        HardwareName = _motherboardName,
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

        public void UpdateTelemetry()
        {
            if (_pView != IntPtr.Zero)
            {
                try
                {
                    var data = Marshal.PtrToStructure<SpeedFanSharedMem>(_pView);
                    for (int f = 0; f < Math.Min(data.NumFans, _activeFans.Count); f++)
                    {
                        _activeFans[f].LiveRpm = data.Fans[f];
                    }
                }
                catch { }
            }
        }

        private readonly Dictionary<int, int> _lastWrittenChannelPwm = new();

        public bool SetFanSpeed(string fanId, float pwmPercent)
        {
            var fan = _activeFans.FirstOrDefault(f => f.Id == fanId || f.Identifier == fanId);
            if (fan == null) return false;

            float clamped = Math.Clamp(pwmPercent, 0f, 100f);
            int targetInt = (int)Math.Round(clamped);
            int channelIdx = fan.Index >= 0 ? fan.Index : 0;
            SetChannelPwm(channelIdx, targetInt);
            fan.CurrentPwmPercent = clamped;
            return true;
        }

        public void SetAllFansSpeed(float pwmPercent)
        {
            float clamped = Math.Clamp(pwmPercent, 0f, 100f);
            int targetInt = (int)Math.Round(clamped);
            SetChannelPwm(-1, targetInt);
            foreach (var fan in _activeFans)
            {
                fan.CurrentPwmPercent = clamped;
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
                            }
                            currentEditIdx++;
                        }
                        return true;
                    }, IntPtr.Zero);
                }
                return true;
            }, IntPtr.Zero);
        }

        public void RestoreBiosControl(string? fanId = null)
        {
            // Do not override when under BIOS default control
        }

        public void Dispose()
        {
            _disposed = true;
            if (_pView != IntPtr.Zero)
            {
                try { UnmapViewOfFile(_pView); } catch { }
                _pView = IntPtr.Zero;
            }
            if (_hMap != IntPtr.Zero)
            {
                try { CloseHandle(_hMap); } catch { }
                _hMap = IntPtr.Zero;
            }
            _isInitialized = false;
        }
    }
}
