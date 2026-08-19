using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using System.Windows.Threading;
using MI50FanControl.Hardware;
using MI50FanControl.Models;
using MI50FanControl.Services;

namespace MI50FanControl.ViewModels
{
    public class SettingsViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly SuperIoHardwareManager _superIo;
        private readonly AmdGpuTelemetry _gpu;
        private readonly LocalizationService _loc;

        private float _liveCoreTemp = 0;
        private string _motherboard = "Detecting...";
        private string _superIoChip = "Detecting...";
        private string _gpuModel = "AMD Radeon Instinct MI50 / Radeon PRO VII";
        private LanguageOption? _selectedLanguage;
        private float _emergencyThreshold = 90f;
        private string _saveStatusMessage = string.Empty;
        private DispatcherTimer? _statusTimer;

        public event Action? SettingsReset;

        public ObservableCollection<LanguageOption> AvailableLanguages { get; } = new();

        public float LiveCoreTemp
        {
            get => _liveCoreTemp;
            set
            {
                if (SetProperty(ref _liveCoreTemp, value))
                {
                    OnPropertyChanged(nameof(LiveCoreTempText));
                }
            }
        }

        public string LiveCoreTempText => $"{_liveCoreTemp:F0}°C";

        public string Motherboard
        {
            get => _motherboard;
            set => SetProperty(ref _motherboard, value);
        }

        public string SuperIoChip
        {
            get => _superIoChip;
            set => SetProperty(ref _superIoChip, value);
        }

        public string GpuModel
        {
            get => _gpuModel;
            set => SetProperty(ref _gpuModel, value);
        }

        public LanguageOption? SelectedLanguage
        {
            get => _selectedLanguage;
            set
            {
                if (SetProperty(ref _selectedLanguage, value) && value != null)
                {
                    _settingsService.Current.Language = value.Code;
                    _settingsService.Save();
                    _loc.SetLanguage(value.Code);
                }
            }
        }

        public bool StartWithWindows
        {
            get => _settingsService.Current.StartWithWindows;
            set
            {
                if (_settingsService.Current.StartWithWindows != value)
                {
                    _settingsService.Current.StartWithWindows = value;
                    _settingsService.Save();
                    AutoStartService.SetAutoStart(value);
                    OnPropertyChanged();
                }
            }
        }

        public bool MinimizeToTrayClose
        {
            get => _settingsService.Current.MinimizeToTrayOnClose;
            set
            {
                if (_settingsService.Current.MinimizeToTrayOnClose != value)
                {
                    _settingsService.Current.MinimizeToTrayOnClose = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                }
            }
        }

        public bool MinimizeToTrayMin
        {
            get => _settingsService.Current.MinimizeToTrayOnMinimize;
            set
            {
                if (_settingsService.Current.MinimizeToTrayOnMinimize != value)
                {
                    _settingsService.Current.MinimizeToTrayOnMinimize = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                }
            }
        }

        public bool DeveloperMode
        {
            get => _settingsService.Current.DeveloperMode;
            set
            {
                if (_settingsService.Current.DeveloperMode != value)
                {
                    _settingsService.Current.DeveloperMode = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                }
            }
        }

        private readonly UpdateService _updateService;
        private string _checkUpdateStatus = string.Empty;
        private bool _isCheckingUpdate = false;

        public string AppVersionDisplay => UpdateService.CurrentVersionDisplay;

        public bool AutoCheckUpdates
        {
            get => _settingsService.Current.AutoCheckUpdates;
            set
            {
                if (_settingsService.Current.AutoCheckUpdates != value)
                {
                    _settingsService.Current.AutoCheckUpdates = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                }
            }
        }

        public string CheckUpdateStatus
        {
            get => _checkUpdateStatus;
            set => SetProperty(ref _checkUpdateStatus, value);
        }

        public bool IsCheckingUpdate
        {
            get => _isCheckingUpdate;
            set => SetProperty(ref _isCheckingUpdate, value);
        }

        public float EmergencyThreshold
        {
            get => _emergencyThreshold;
            set
            {
                if (SetProperty(ref _emergencyThreshold, value))
                {
                    OnPropertyChanged(nameof(EmergencyThresholdText));
                }
            }
        }

        public string EmergencyThresholdText => $"{_emergencyThreshold:F0}°C";

        public string SaveStatusMessage
        {
            get => _saveStatusMessage;
            set => SetProperty(ref _saveStatusMessage, value);
        }

        public ICommand OpenLangFolderCommand { get; }
        public ICommand SaveEmergencyCommand { get; }
        public ICommand ResetEmergencyDefaultCommand { get; }
        public ICommand ResetAllDefaultsCommand { get; }
        public ICommand CheckUpdatesCommand { get; }

        public SettingsViewModel(SettingsService settingsService, SuperIoHardwareManager superIo, AmdGpuTelemetry gpu, LocalizationService loc, UpdateService updateService)
        {
            _settingsService = settingsService;
            _superIo = superIo;
            _gpu = gpu;
            _loc = loc;
            _updateService = updateService;

            _emergencyThreshold = _settingsService.Current.EmergencyTempThreshold;

            OpenLangFolderCommand = new RelayCommand(() => _loc.OpenLanguageFolder());

            SaveEmergencyCommand = new RelayCommand(SaveEmergencySettings);
            ResetEmergencyDefaultCommand = new RelayCommand(ResetEmergencyDefault);
            ResetAllDefaultsCommand = new RelayCommand(ResetAllDefaults);
            CheckUpdatesCommand = new RelayCommand(async () => await PerformCheckUpdateAsync(true));

            if (AutoStartService.IsAutoStartEnabled())
            {
                _settingsService.Current.StartWithWindows = true;
            }

            RefreshLanguages();
            RefreshHardwareInfo();
        }

        public async System.Threading.Tasks.Task PerformCheckUpdateAsync(bool isManual)
        {
            if (IsCheckingUpdate) return;
            IsCheckingUpdate = true;
            CheckUpdateStatus = _loc.Get("CheckingUpdates", "Đang kiểm tra bản cập nhật...");

            try
            {
                var info = await _updateService.CheckForUpdatesAsync();
                if (info.HasUpdate)
                {
                    CheckUpdateStatus = $"🚀 {_loc.Get("UpdateFound", "Đã tìm thấy bản")} {info.LatestVersion}!";
                    System.Windows.Application.Current.Dispatcher.Invoke(() =>
                    {
                        var dialog = new Views.UpdateDialogView(info, _settingsService, _updateService);
                        dialog.Owner = System.Windows.Application.Current.MainWindow;
                        dialog.ShowDialog();
                        OnPropertyChanged(nameof(AutoCheckUpdates));
                    });
                }
                else
                {
                    CheckUpdateStatus = _loc.Get("UpToDate", "✅ Bạn đang sử dụng phiên bản mới nhất (v3.0)!");
                }
            }
            catch (Exception ex)
            {
                CheckUpdateStatus = _loc.Get("UpdateCheckFailed", "⚠️ Không thể kết nối tới GitHub để kiểm tra.");
                LogService.Instance.Error("UpdateCheck", ex.Message);
            }
            finally
            {
                IsCheckingUpdate = false;
            }
        }

        public void SaveEmergencySettings()
        {
            _settingsService.Current.EmergencyTempThreshold = _emergencyThreshold;
            _settingsService.Save();
            SetStatusMessage(_loc.Get("SaveSuccess", "✅ Đã lưu & áp dụng thành công!"));
        }

        public void ResetEmergencyDefault()
        {
            EmergencyThreshold = 90f;
            _settingsService.Current.EmergencyTempThreshold = 90f;
            _settingsService.Save();
            SetStatusMessage(_loc.Get("SaveSuccess", "✅ Đã lưu & áp dụng thành công!"));
        }

        public void ResetAllDefaults()
        {
            _settingsService.Current.EmergencyProtectionEnabled = true;
            _settingsService.Current.EmergencyTempThreshold = 90f;
            _settingsService.Current.HysteresisDegrees = 2.0f;
            _settingsService.Current.SmoothingRatePercentPerSec = 8.0f;
            _settingsService.Current.PollingIntervalMs = 800;
            _settingsService.Current.CurveProfiles = FanCurveProfile.CreateDefaultProfiles();
            _settingsService.Current.ActiveCurveProfileId = "balanced";
            _settingsService.Current.GlobalManualOverride = false;
            _settingsService.Current.GlobalManualSpeedPercent = 60f;
            _settingsService.Current.MinimizeToTrayOnClose = true;
            _settingsService.Current.MinimizeToTrayOnMinimize = true;
            _settingsService.Current.SelectedSensor = GpuSensorSource.GpuHotSpot;
            _settingsService.Save();

            EmergencyThreshold = 90f;
            MinimizeToTrayClose = true;
            MinimizeToTrayMin = true;

            SettingsReset?.Invoke();
            SetStatusMessage(_loc.Get("ResetDefaultsSuccess", "✅ Đã khôi phục toàn bộ cài đặt về mặc định chuẩn thành công!"));
        }

        private void SetStatusMessage(string message)
        {
            SaveStatusMessage = message;
            _statusTimer?.Stop();
            _statusTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(4)
            };
            _statusTimer.Tick += (s, e) =>
            {
                SaveStatusMessage = string.Empty;
                _statusTimer?.Stop();
            };
            _statusTimer.Start();
        }

        public void RefreshLanguages()
        {
            AvailableLanguages.Clear();
            foreach (var lang in _loc.GetAvailableLanguages())
            {
                AvailableLanguages.Add(lang);
            }
            _selectedLanguage = AvailableLanguages.FirstOrDefault(l => l.Code == _settingsService.Current.Language)
                                ?? AvailableLanguages.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedLanguage));
        }

        public void RefreshHardwareInfo()
        {
            Motherboard = _superIo.MotherboardName;
            SuperIoChip = _superIo.SuperIoChipName;
            GpuModel = _gpu.GpuName;
            EmergencyThreshold = _settingsService.Current.EmergencyTempThreshold;
        }

        public void UpdateTelemetry(AmdGpuTelemetryData data)
        {
            LiveCoreTemp = data.CoreTemperature;
            if (!string.IsNullOrEmpty(data.GpuName) && GpuModel != data.GpuName)
            {
                GpuModel = data.GpuName;
            }
        }
    }
}
