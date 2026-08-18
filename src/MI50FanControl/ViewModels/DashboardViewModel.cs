using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MI50FanControl.Models;
using MI50FanControl.Services;

namespace MI50FanControl.ViewModels
{
    public class DashboardViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly FanControlEngine _engine;
        private readonly LocalizationService _loc;

        private float _gpuTemp = 0;
        private float _gpuClock = 0;
        private float _vramClock = 0;
        private string _gpuName = "AMD Radeon Instinct MI50 / Radeon PRO VII";
        private bool _isEmergency = false;
        private bool _isTesting100 = false;
        private int _testSecondsRemaining = 0;
        private bool _manualOverride = false;
        private float _manualSpeed = 60f;
        private FanCurveProfile? _selectedProfile;

        public ObservableCollection<FanCardViewModel> ActiveFans { get; } = new();
        public ObservableCollection<FanCurveProfile> Profiles { get; } = new();

        public float GpuTemperature
        {
            get => _gpuTemp;
            set
            {
                if (SetProperty(ref _gpuTemp, value))
                {
                    OnPropertyChanged(nameof(GpuTemperatureText));
                    OnPropertyChanged(nameof(TempGaugeFraction));
                }
            }
        }

        public string GpuTemperatureText => $"{_gpuTemp:F0}°C";
        public double TempGaugeFraction => Math.Clamp(_gpuTemp / 100.0, 0.0, 1.0);

        public float GpuClock
        {
            get => _gpuClock;
            set
            {
                if (SetProperty(ref _gpuClock, value))
                {
                    OnPropertyChanged(nameof(GpuClockText));
                    OnPropertyChanged(nameof(GpuClockGaugeFraction));
                }
            }
        }

        public string GpuClockText => $"{_gpuClock:F0} MHz";
        public double GpuClockGaugeFraction => Math.Clamp(_gpuClock / 2000.0, 0.0, 1.0);

        public float VramClock
        {
            get => _vramClock;
            set
            {
                if (SetProperty(ref _vramClock, value))
                {
                    OnPropertyChanged(nameof(VramClockText));
                    OnPropertyChanged(nameof(VramClockGaugeFraction));
                }
            }
        }

        public string VramClockText => $"{_vramClock:F0} MHz";
        public double VramClockGaugeFraction => Math.Clamp(_vramClock / 1200.0, 0.0, 1.0);

        public string GpuName
        {
            get => _gpuName;
            set => SetProperty(ref _gpuName, value);
        }

        public bool HasActiveFans => ActiveFans.Count > 0;

        public bool IsEmergency
        {
            get => _isEmergency;
            set => SetProperty(ref _isEmergency, value);
        }

        public bool IsTesting100
        {
            get => _isTesting100;
            set
            {
                if (SetProperty(ref _isTesting100, value))
                {
                    OnPropertyChanged(nameof(Test100ButtonText));
                }
            }
        }

        public int TestSecondsRemaining
        {
            get => _testSecondsRemaining;
            set
            {
                if (SetProperty(ref _testSecondsRemaining, value))
                {
                    OnPropertyChanged(nameof(Test100ButtonText));
                }
            }
        }

        public string Test100ButtonText => _isTesting100
            ? $"{_loc["TestingFan"]} ({_testSecondsRemaining}s)"
            : _loc["Test100Btn"];

        public bool ManualOverride
        {
            get => _manualOverride;
            set
            {
                if (SetProperty(ref _manualOverride, value))
                {
                    _settingsService.Current.GlobalManualOverride = value;
                    _settingsService.Save();
                }
            }
        }

        public float ManualSpeed
        {
            get => _manualSpeed;
            set
            {
                if (SetProperty(ref _manualSpeed, value))
                {
                    _settingsService.Current.GlobalManualSpeedPercent = value;
                    _settingsService.Save();
                }
            }
        }

        public FanCurveProfile? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (SetProperty(ref _selectedProfile, value) && value != null)
                {
                    _settingsService.Current.ActiveCurveProfileId = value.Id;
                    _settingsService.Save();
                }
            }
        }

        public ICommand Test100Command { get; }
        public ICommand ToggleManualOverrideCommand { get; }

        public DashboardViewModel(SettingsService settingsService, FanControlEngine engine, LocalizationService loc)
        {
            _settingsService = settingsService;
            _engine = engine;
            _loc = loc;

            _manualOverride = _settingsService.Current.GlobalManualOverride;
            _manualSpeed = _settingsService.Current.GlobalManualSpeedPercent;

            Test100Command = new RelayCommand(() =>
            {
                if (_isTesting100)
                {
                    _engine.CancelTest100Percent();
                }
                else
                {
                    _engine.TriggerTest100Percent(5);
                }
            });

            ToggleManualOverrideCommand = new RelayCommand(() =>
            {
                ManualOverride = !ManualOverride;
            });

            _loc.LanguageChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(Test100ButtonText));
            };

            RefreshProfilesList();
        }

        public void RefreshProfilesList()
        {
            Profiles.Clear();
            foreach (var p in _settingsService.Current.CurveProfiles)
            {
                Profiles.Add(p);
            }
            _selectedProfile = Profiles.FirstOrDefault(p => p.Id == _settingsService.Current.ActiveCurveProfileId)
                              ?? Profiles.FirstOrDefault();
            OnPropertyChanged(nameof(SelectedProfile));
        }

        public void UpdateLiveTelemetry(FanTelemetryEventArgs state)
        {
            GpuTemperature = state.GpuData.CoreTemperature;
            GpuClock = state.GpuData.GpuClockMhz;
            VramClock = state.GpuData.VramClockMhz;

            if (!string.IsNullOrEmpty(state.GpuData.GpuName) && _gpuName != state.GpuData.GpuName)
            {
                GpuName = state.GpuData.GpuName;
            }

            IsEmergency = state.IsEmergencyOverheat;
            IsTesting100 = state.IsTest100Running;
            TestSecondsRemaining = state.Test100SecondsLeft;

            // Sync fan cards
            bool activeCountChanged = false;
            foreach (var hwFan in state.Fans)
            {
                var existing = ActiveFans.FirstOrDefault(f => f.Identifier == hwFan.Identifier);
                if (existing == null)
                {
                    var cfg = _settingsService.Current.FanConfigs.FirstOrDefault(c => c.FanIdentifier == hwFan.Identifier);
                    var newCard = new FanCardViewModel
                    {
                        Id = hwFan.Id,
                        Identifier = hwFan.Identifier,
                        HardwareName = hwFan.HardwareName,
                        SensorName = hwFan.SensorName,
                        CustomName = cfg?.CustomName ?? string.Empty,
                        Mode = cfg?.Mode ?? FanControlMode.FollowCurve,
                        MinSafePwm = cfg?.MinSafePwmPercent ?? 20f,
                        MaxSafePwm = cfg?.MaxSafePwmPercent ?? 100f,
                        FixedSpeedPercent = cfg?.FixedSpeedPercent ?? 50f
                    };
                    newCard.UpdateFromHardware(hwFan);
                    ActiveFans.Add(newCard);
                    activeCountChanged = true;
                }
                else
                {
                    existing.UpdateFromHardware(hwFan);
                    var cfg = _settingsService.Current.FanConfigs.FirstOrDefault(c => c.FanIdentifier == hwFan.Identifier);
                    string expectedName = cfg?.CustomName ?? string.Empty;
                    if (existing.CustomName != expectedName)
                    {
                        existing.CustomName = expectedName;
                    }
                }
            }

            if (activeCountChanged)
            {
                OnPropertyChanged(nameof(HasActiveFans));
            }
        }
    }
}
