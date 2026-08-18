using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Linq;
using System.Windows.Input;
using MI50FanControl.Services;
using WpfApplication = System.Windows.Application;
using WpfClipboard = System.Windows.Clipboard;

namespace MI50FanControl.ViewModels
{
    public class DevLogViewModel : ViewModelBase
    {
        private readonly LogService _logService;
        private readonly FanControlEngine _engine;
        private string _selectedFilter = "All";

        public ObservableCollection<LogEntry> DisplayedEntries { get; } = new();

        public string SelectedFilter
        {
            get => _selectedFilter;
            set
            {
                if (SetProperty(ref _selectedFilter, value))
                {
                    RefreshFilter();
                }
            }
        }

        public ICommand CopyLogsCommand { get; }
        public ICommand SaveLogFileCommand { get; }
        public ICommand ClearLogsCommand { get; }
        public ICommand RescanHardwareCommand { get; }
        public ICommand Force100Command { get; }
        public ICommand Force50Command { get; }
        public ICommand Force0Command { get; }
        public ICommand RestoreBiosCommand { get; }

        public DevLogViewModel(FanControlEngine engine)
        {
            _engine = engine;
            _logService = LogService.Instance;

            CopyLogsCommand = new RelayCommand(CopyLogsToClipboard);
            SaveLogFileCommand = new RelayCommand(SaveLogToFile);
            ClearLogsCommand = new RelayCommand(ClearLogs);
            RescanHardwareCommand = new RelayCommand(RescanHardware);
            Force100Command = new RelayCommand(() => DirectTestPwm(100f));
            Force50Command = new RelayCommand(() => DirectTestPwm(50f));
            Force0Command = new RelayCommand(() => DirectTestPwm(0f));
            RestoreBiosCommand = new RelayCommand(DirectRestoreBios);

            _logService.EntryAdded += OnEntryAdded;

            RefreshFilter();
        }

        private void OnEntryAdded(object? sender, LogEntry entry)
        {
            WpfApplication.Current?.Dispatcher?.InvokeAsync(() =>
            {
                if (MatchesFilter(entry))
                {
                    DisplayedEntries.Add(entry);
                }
            });
        }

        private bool MatchesFilter(LogEntry entry)
        {
            if (_selectedFilter == "All" || string.IsNullOrEmpty(_selectedFilter)) return true;
            if (_selectedFilter == "Errors") return entry.Level == LogLevel.Error || entry.Level == LogLevel.Warning;
            if (_selectedFilter == "Hardware") return entry.Level == LogLevel.Hardware || entry.Category.Contains("SuperIO", StringComparison.OrdinalIgnoreCase) || entry.Category.Contains("Motherboard", StringComparison.OrdinalIgnoreCase);
            if (_selectedFilter == "AMD ADL") return entry.Category.Contains("AMD", StringComparison.OrdinalIgnoreCase) || entry.Category.Contains("ADL", StringComparison.OrdinalIgnoreCase);
            if (_selectedFilter == "PWM") return entry.Category.Contains("PWM", StringComparison.OrdinalIgnoreCase) || entry.Category.Contains("Fan", StringComparison.OrdinalIgnoreCase);
            return true;
        }

        private void RefreshFilter()
        {
            DisplayedEntries.Clear();
            foreach (var entry in _logService.Entries)
            {
                if (MatchesFilter(entry))
                {
                    DisplayedEntries.Add(entry);
                }
            }
        }

        private void CopyLogsToClipboard()
        {
            try
            {
                string text = _logService.GetAllLogsAsText();
                WpfClipboard.SetText(text);
                _logService.Info("DevLogs", "Đã sao chép toàn bộ nhật ký vào Clipboard.");
            }
            catch (Exception ex)
            {
                _logService.Error("DevLogs", $"Lỗi sao chép: {ex.Message}");
            }
        }

        private void SaveLogToFile()
        {
            try
            {
                string logDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                    "MI50FanControl", "Logs");
                Directory.CreateDirectory(logDir);

                string logPath = Path.Combine(logDir, $"mi50_debug_{DateTime.Now:yyyyMMdd_HHmmss}.log");
                _logService.SaveLogToFile(logPath);
                _logService.Success("DevLogs", $"Đã lưu nhật ký vào file: {logPath}");

                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = "explorer.exe",
                    Arguments = $"/select,\"{logPath}\"",
                    UseShellExecute = true
                });
            }
            catch (Exception ex)
            {
                _logService.Error("DevLogs", $"Lỗi lưu file: {ex.Message}");
            }
        }

        private void ClearLogs()
        {
            _logService.Clear();
            DisplayedEntries.Clear();
            _logService.Info("DevLogs", "Nhật ký đã được làm sạch.");
        }

        private void RescanHardware()
        {
            _logService.Hardware("Rescan", "Thực hiện quét lại toàn bộ phần cứng...");
            _engine.SuperIo.RefreshActiveFans();
        }

        private void DirectTestPwm(float percent)
        {
            _logService.Hardware("Direct Test", $"[Dev Console] Gửi lệnh PWM trực tiếp: {percent:F0}%");
            _engine.SuperIo.SetAllFansSpeed(percent);
            _engine.GpuTelemetry.SetGpuFanSpeedPercent(percent);
        }

        private void DirectRestoreBios()
        {
            _logService.Hardware("Direct Test", "[Dev Console] Khôi phục điều khiển BIOS...");
            _engine.SuperIo.RestoreBiosControl();
            _engine.GpuTelemetry.RestoreGpuFanDefault();
        }
    }
}
