using System;
using System.Collections.ObjectModel;
using System.Linq;
using System.Windows.Input;
using MI50FanControl.Models;
using MI50FanControl.Services;

namespace MI50FanControl.ViewModels
{
    public class CurveEditorViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly LocalizationService _loc;

        private FanCurveProfile? _selectedProfile;
        private CurvePoint? _selectedPoint;
        private float _testTemperature = 65f;
        private float _hysteresis = 2.0f;
        private float _smoothing = 8.0f;

        public ObservableCollection<FanCurveProfile> Profiles { get; } = new();
        public ObservableCollection<CurvePoint> Points { get; } = new();

        public FanCurveProfile? SelectedProfile
        {
            get => _selectedProfile;
            set
            {
                if (SetProperty(ref _selectedProfile, value))
                {
                    LoadPointsFromSelectedProfile();
                    OnPropertyChanged(nameof(CalculatedOutputSpeedText));
                }
            }
        }

        public CurvePoint? SelectedPoint
        {
            get => _selectedPoint;
            set => SetProperty(ref _selectedPoint, value);
        }

        public float TestTemperature
        {
            get => _testTemperature;
            set
            {
                if (SetProperty(ref _testTemperature, value))
                {
                    OnPropertyChanged(nameof(TestTemperatureText));
                    OnPropertyChanged(nameof(CalculatedOutputSpeedText));
                }
            }
        }

        public string TestTemperatureText => $"{_testTemperature:F0}°C";

        public string CalculatedOutputSpeedText
        {
            get
            {
                if (_selectedProfile == null) return "50%";
                float speed = _selectedProfile.CalculateFanSpeed(_testTemperature);
                return $"{speed:F0}%";
            }
        }

        public float Hysteresis
        {
            get => _hysteresis;
            set
            {
                if (SetProperty(ref _hysteresis, value))
                {
                    _settingsService.Current.HysteresisDegrees = value;
                    _settingsService.Save();
                }
            }
        }

        public float Smoothing
        {
            get => _smoothing;
            set
            {
                if (SetProperty(ref _smoothing, value))
                {
                    _settingsService.Current.SmoothingRatePercentPerSec = value;
                    _settingsService.Save();
                }
            }
        }

        public ICommand NewProfileCommand { get; }
        public ICommand DuplicateProfileCommand { get; }
        public ICommand RenameProfileCommand { get; }
        public ICommand DeleteProfileCommand { get; }
        public ICommand AddPointCommand { get; }
        public ICommand RemovePointCommand { get; }
        public ICommand ResetDefaultCurvesCommand { get; }
        public ICommand SaveCommand { get; }

        public event Action? ProfilesChanged;

        public CurveEditorViewModel(SettingsService settingsService, LocalizationService loc)
        {
            _settingsService = settingsService;
            _loc = loc;

            _hysteresis = _settingsService.Current.HysteresisDegrees;
            _smoothing = _settingsService.Current.SmoothingRatePercentPerSec;

            NewProfileCommand = new RelayCommand(CreateNewProfile);
            DuplicateProfileCommand = new RelayCommand(DuplicateSelectedProfile, () => SelectedProfile != null);
            RenameProfileCommand = new RelayCommand(RenameSelectedProfile, () => SelectedProfile != null);
            DeleteProfileCommand = new RelayCommand(DeleteSelectedProfile, () => SelectedProfile != null && Profiles.Count > 1);
            AddPointCommand = new RelayCommand(AddPointToProfile, () => SelectedProfile != null);
            RemovePointCommand = new RelayCommand(RemoveSelectedPoint, () => SelectedPoint != null && Points.Count > 2);
            ResetDefaultCurvesCommand = new RelayCommand(ResetDefaultProfiles);
            SaveCommand = new RelayCommand(SaveProfileChanges);

            RefreshProfiles();
        }

        public void ResetDefaultProfiles()
        {
            _settingsService.Current.CurveProfiles = FanCurveProfile.CreateDefaultProfiles();
            _settingsService.Current.ActiveCurveProfileId = "balanced";
            _settingsService.Current.HysteresisDegrees = 2.0f;
            _settingsService.Current.SmoothingRatePercentPerSec = 8.0f;
            _settingsService.Save();

            Hysteresis = 2.0f;
            Smoothing = 8.0f;
            RefreshProfiles();
            ProfilesChanged?.Invoke();
        }

        public void RefreshProfiles()
        {
            Profiles.Clear();
            foreach (var p in _settingsService.Current.CurveProfiles)
            {
                Profiles.Add(p);
            }

            SelectedProfile = Profiles.FirstOrDefault(p => p.Id == _settingsService.Current.ActiveCurveProfileId)
                              ?? Profiles.FirstOrDefault();
        }

        public void SyncPointsFromProfile()
        {
            LoadPointsFromSelectedProfile();
            SaveProfileChanges();
        }

        private void LoadPointsFromSelectedProfile()
        {
            Points.Clear();
            if (_selectedProfile != null)
            {
                foreach (var pt in _selectedProfile.Points.OrderBy(p => p.Temperature))
                {
                    Points.Add(pt);
                }
            }
        }

        private void CreateNewProfile()
        {
            var newProf = new FanCurveProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"Custom Curve {Profiles.Count + 1}",
                Points = new System.Collections.Generic.List<CurvePoint>
                {
                    new CurvePoint(30, 25),
                    new CurvePoint(50, 40),
                    new CurvePoint(70, 75),
                    new CurvePoint(85, 100)
                }
            };

            _settingsService.Current.CurveProfiles.Add(newProf);
            _settingsService.Save();

            Profiles.Add(newProf);
            SelectedProfile = newProf;
            ProfilesChanged?.Invoke();
        }

        private void RenameSelectedProfile()
        {
            if (SelectedProfile == null) return;
            // Let's refresh profile name bindings
            _settingsService.Save();
            ProfilesChanged?.Invoke();
        }

        private void DuplicateSelectedProfile()
        {
            if (SelectedProfile == null) return;

            var dup = new FanCurveProfile
            {
                Id = Guid.NewGuid().ToString(),
                Name = $"{SelectedProfile.Name} (Copy)",
                Points = SelectedProfile.Points.Select(p => new CurvePoint(p.Temperature, p.FanSpeedPercent)).ToList()
            };

            _settingsService.Current.CurveProfiles.Add(dup);
            _settingsService.Save();

            Profiles.Add(dup);
            SelectedProfile = dup;
            ProfilesChanged?.Invoke();
        }

        private void DeleteSelectedProfile()
        {
            if (SelectedProfile == null || Profiles.Count <= 1) return;

            var toRemove = SelectedProfile;
            _settingsService.Current.CurveProfiles.RemoveAll(p => p.Id == toRemove.Id);
            Profiles.Remove(toRemove);

            SelectedProfile = Profiles.FirstOrDefault();
            if (SelectedProfile != null)
            {
                _settingsService.Current.ActiveCurveProfileId = SelectedProfile.Id;
            }
            _settingsService.Save();
            ProfilesChanged?.Invoke();
        }

        private void AddPointToProfile()
        {
            if (SelectedProfile == null) return;

            float newTemp = 60f;
            if (Points.Count > 0)
            {
                newTemp = Math.Clamp(Points[^1].Temperature - 10f, 20f, 95f);
            }

            var newPt = new CurvePoint(newTemp, 50f);
            SelectedProfile.Points.Add(newPt);
            SelectedProfile.Points = SelectedProfile.Points.OrderBy(p => p.Temperature).ToList();

            LoadPointsFromSelectedProfile();
            SelectedPoint = newPt;
            SaveProfileChanges();
        }

        private void RemoveSelectedPoint()
        {
            if (SelectedProfile == null || SelectedPoint == null || Points.Count <= 2) return;

            SelectedProfile.Points.Remove(SelectedPoint);
            LoadPointsFromSelectedProfile();
            SelectedPoint = Points.FirstOrDefault();
            SaveProfileChanges();
        }

        public void SaveProfileChanges()
        {
            if (SelectedProfile != null)
            {
                SelectedProfile.Points = Points.OrderBy(p => p.Temperature).ToList();
                _settingsService.Save();
                OnPropertyChanged(nameof(CalculatedOutputSpeedText));
                ProfilesChanged?.Invoke();
            }
        }
    }
}
