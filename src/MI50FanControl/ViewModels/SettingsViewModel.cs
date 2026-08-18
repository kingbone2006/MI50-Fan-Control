using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
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

        public float EmergencyThreshold
        {
            get => _settingsService.Current.EmergencyTempThreshold;
            set
            {
                if (_settingsService.Current.EmergencyTempThreshold != value)
                {
                    _settingsService.Current.EmergencyTempThreshold = value;
                    _settingsService.Save();
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(EmergencyThresholdText));
                }
            }
        }

        public string EmergencyThresholdText => $"{EmergencyThreshold:F0}°C";

        public ICommand OpenLangFolderCommand { get; }

        public SettingsViewModel(SettingsService settingsService, SuperIoHardwareManager superIo, AmdGpuTelemetry gpu, LocalizationService loc)
        {
            _settingsService = settingsService;
            _superIo = superIo;
            _gpu = gpu;
            _loc = loc;

            OpenLangFolderCommand = new RelayCommand(() => _loc.OpenLanguageFolder());

            if (AutoStartService.IsAutoStartEnabled())
            {
                _settingsService.Current.StartWithWindows = true;
            }

            RefreshLanguages();
            RefreshHardwareInfo();
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
