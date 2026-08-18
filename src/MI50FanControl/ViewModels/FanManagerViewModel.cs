using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MI50FanControl.Hardware;
using MI50FanControl.Models;
using MI50FanControl.Services;

namespace MI50FanControl.ViewModels
{
    public class FanManagerViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly SuperIoHardwareManager _superIo;
        private readonly LocalizationService _loc;

        private FanCardViewModel? _selectedFan;

        public ObservableCollection<FanCardViewModel> Fans { get; } = new();

        public FanCardViewModel? SelectedFan
        {
            get => _selectedFan;
            set => SetProperty(ref _selectedFan, value);
        }

        public ICommand SaveSettingsCommand { get; }

        public FanManagerViewModel(SettingsService settingsService, SuperIoHardwareManager superIo, LocalizationService loc)
        {
            _settingsService = settingsService;
            _superIo = superIo;
            _loc = loc;

            SaveSettingsCommand = new RelayCommand(SaveFanConfigurations);
            RefreshFans();
        }

        public void RefreshFans()
        {
            Fans.Clear();
            foreach (var hwFan in _superIo.ActiveFans)
            {
                var cfg = _settingsService.Current.FanConfigs.FirstOrDefault(c => c.FanIdentifier == hwFan.Identifier);
                var card = new FanCardViewModel
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
                card.UpdateFromHardware(hwFan);
                Fans.Add(card);
            }

            if (SelectedFan == null)
            {
                SelectedFan = Fans.FirstOrDefault();
            }
        }

        public void UpdateHardwareState(IReadOnlyList<HardwareFanItem> activeFans)
        {
            foreach (var hwFan in activeFans)
            {
                var existing = Fans.FirstOrDefault(f => f.Identifier == hwFan.Identifier);
                if (existing != null)
                {
                    existing.UpdateFromHardware(hwFan);
                }
            }
        }

        public void SaveFanConfigurations()
        {
            foreach (var fan in Fans)
            {
                var cfg = _settingsService.Current.FanConfigs.FirstOrDefault(c => c.FanIdentifier == fan.Identifier);
                if (cfg == null)
                {
                    cfg = new FanConfig { FanIdentifier = fan.Identifier };
                    _settingsService.Current.FanConfigs.Add(cfg);
                }

                cfg.CustomName = fan.CustomName;
                cfg.Mode = fan.Mode;
                cfg.MinSafePwmPercent = fan.MinSafePwm;
                cfg.MaxSafePwmPercent = fan.MaxSafePwm;
                cfg.FixedSpeedPercent = fan.FixedSpeedPercent;
            }

            _settingsService.Save();
        }
    }
}
