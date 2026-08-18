using System;
using MI50FanControl.Hardware;
using MI50FanControl.Models;
using MI50FanControl.Services;

namespace MI50FanControl.ViewModels
{
    public class FanCardViewModel : ViewModelBase
    {
        private string _id = string.Empty;
        private string _identifier = string.Empty;
        private string _hardwareName = string.Empty;
        private string _sensorName = string.Empty;
        private string _customName = string.Empty;
        private float _liveRpm = 0;
        private float _currentPwm = 0;
        private FanControlMode _mode = FanControlMode.FollowCurve;
        private float _fixedSpeedPercent = 50f;
        private float _minSafePwm = 20f;
        private float _maxSafePwm = 100f;
        private bool _hasControl = true;

        public string Id
        {
            get => _id;
            set => SetProperty(ref _id, value);
        }

        public string Identifier
        {
            get => _identifier;
            set => SetProperty(ref _identifier, value);
        }

        public string HardwareName
        {
            get => _hardwareName;
            set => SetProperty(ref _hardwareName, value);
        }

        public string SensorName
        {
            get => _sensorName;
            set => SetProperty(ref _sensorName, value);
        }

        private int _index = 0;

        public int Index
        {
            get => _index;
            set
            {
                if (SetProperty(ref _index, value))
                {
                    OnPropertyChanged(nameof(LocalizedSensorName));
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(SubtitleText));
                }
            }
        }

        public string LocalizedSensorName
        {
            get
            {
                string prefix = LocalizationService.Instance.Get("FanHeaderPrefix", "Cổng Quạt");
                return $"{prefix} #{_index + 1}";
            }
        }

        public string DisplayName => !string.IsNullOrWhiteSpace(CustomName) ? CustomName.Trim() : LocalizedSensorName;
        public string SubtitleText => !string.IsNullOrWhiteSpace(CustomName) ? LocalizedSensorName : HardwareName;

        public FanCardViewModel()
        {
            LocalizationService.Instance.LanguageChanged += (s, e) =>
            {
                OnPropertyChanged(nameof(LocalizedSensorName));
                OnPropertyChanged(nameof(DisplayName));
                OnPropertyChanged(nameof(SubtitleText));
            };
        }

        public string CustomName
        {
            get => _customName;
            set
            {
                if (SetProperty(ref _customName, value))
                {
                    OnPropertyChanged(nameof(DisplayName));
                    OnPropertyChanged(nameof(SubtitleText));
                }
            }
        }

        public float LiveRpm
        {
            get => _liveRpm;
            set
            {
                if (SetProperty(ref _liveRpm, value))
                {
                    OnPropertyChanged(nameof(LiveRpmText));
                }
            }
        }

        public string LiveRpmText => $"{_liveRpm:F0} RPM";

        public float CurrentPwm
        {
            get => _currentPwm;
            set
            {
                if (SetProperty(ref _currentPwm, value))
                {
                    OnPropertyChanged(nameof(CurrentPwmText));
                }
            }
        }

        public string CurrentPwmText => $"{_currentPwm:F0}%";

        public FanControlMode Mode
        {
            get => _mode;
            set => SetProperty(ref _mode, value);
        }

        public float FixedSpeedPercent
        {
            get => _fixedSpeedPercent;
            set => SetProperty(ref _fixedSpeedPercent, value);
        }

        public float MinSafePwm
        {
            get => _minSafePwm;
            set => SetProperty(ref _minSafePwm, value);
        }

        public float MaxSafePwm
        {
            get => _maxSafePwm;
            set => SetProperty(ref _maxSafePwm, value);
        }

        public bool HasControl
        {
            get => _hasControl;
            set => SetProperty(ref _hasControl, value);
        }

        public void UpdateFromHardware(HardwareFanItem item)
        {
            Index = item.Index;
            HardwareName = item.HardwareName;
            LiveRpm = item.LiveRpm;
            CurrentPwm = item.CurrentPwmPercent;
            HasControl = item.HasControl;
        }
    }
}
