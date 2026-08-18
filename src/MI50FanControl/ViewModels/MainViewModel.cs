using System;
using System.Windows.Input;
using MI50FanControl.Services;

namespace MI50FanControl.ViewModels
{
    public class MainViewModel : ViewModelBase
    {
        private readonly SettingsService _settingsService;
        private readonly FanControlEngine _engine;
        private readonly LocalizationService _loc;

        private object _currentView;
        private string _activeTab = "Dashboard";
        private bool _isLoading = true;
        private int _telemetrySuccessCount = 0;

        public DashboardViewModel DashboardVM { get; }
        public CurveEditorViewModel CurveEditorVM { get; }
        public FanManagerViewModel FanManagerVM { get; }
        public SettingsViewModel SettingsVM { get; }
        public DevLogViewModel DevLogVM { get; }

        public SettingsService SettingsService => _settingsService;
        public FanControlEngine Engine => _engine;
        public LocalizationService Loc => _loc;

        public bool IsLoading
        {
            get => _isLoading;
            set => SetProperty(ref _isLoading, value);
        }

        public object CurrentView
        {
            get => _currentView;
            set => SetProperty(ref _currentView, value);
        }

        public string ActiveTab
        {
            get => _activeTab;
            set => SetProperty(ref _activeTab, value);
        }

        public ICommand NavigateDashboardCommand { get; }
        public ICommand NavigateCurvesCommand { get; }
        public ICommand NavigateFanManagerCommand { get; }
        public ICommand NavigateSettingsCommand { get; }
        public ICommand NavigateDevLogsCommand { get; }

        public MainViewModel()
        {
            _loc = LocalizationService.Instance;
            _settingsService = new SettingsService();
            _settingsService.Load();

            _loc.SetLanguage(_settingsService.Current.Language);

            _engine = new FanControlEngine(_settingsService);

            DashboardVM = new DashboardViewModel(_settingsService, _engine, _loc);
            CurveEditorVM = new CurveEditorViewModel(_settingsService, _loc);
            FanManagerVM = new FanManagerViewModel(_settingsService, _engine.SuperIo, _loc);
            SettingsVM = new SettingsViewModel(_settingsService, _engine.SuperIo, _engine.GpuTelemetry, _loc);
            DevLogVM = new DevLogViewModel(_engine);

            _currentView = DashboardVM;

            NavigateDashboardCommand = new RelayCommand(() =>
            {
                CurrentView = DashboardVM;
                ActiveTab = "Dashboard";
            });

            NavigateCurvesCommand = new RelayCommand(() =>
            {
                CurveEditorVM.RefreshProfiles();
                CurrentView = CurveEditorVM;
                ActiveTab = "Curves";
            });

            NavigateFanManagerCommand = new RelayCommand(() =>
            {
                FanManagerVM.RefreshFans();
                CurrentView = FanManagerVM;
                ActiveTab = "FanManager";
            });

            NavigateSettingsCommand = new RelayCommand(() =>
            {
                SettingsVM.RefreshHardwareInfo();
                SettingsVM.RefreshLanguages();
                CurrentView = SettingsVM;
                ActiveTab = "Settings";
            });

            NavigateDevLogsCommand = new RelayCommand(() =>
            {
                CurrentView = DevLogVM;
                ActiveTab = "DevLogs";
            });

            CurveEditorVM.ProfilesChanged += () =>
            {
                DashboardVM.RefreshProfilesList();
            };

            _loc.LanguageChanged += (s, e) =>
            {
                DashboardVM.RefreshProfilesList();
                CurveEditorVM.RefreshProfiles();
            };

            // Hook live engine telemetry updates
            _engine.TelemetryUpdated += OnEngineTelemetryUpdated;

            // Start hardware engine & background monitoring loop
            _engine.Start();

            // Safety fallback: Dismiss loading overlay after max 3.5s
            System.Threading.Tasks.Task.Delay(3500).ContinueWith(_ =>
            {
                System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    IsLoading = false;
                });
            });
        }

        private void OnEngineTelemetryUpdated(object? sender, FanTelemetryEventArgs e)
        {
            System.Windows.Application.Current?.Dispatcher?.InvokeAsync(() =>
            {
                DashboardVM.UpdateLiveTelemetry(e);
                FanManagerVM.UpdateHardwareState(e.Fans);
                SettingsVM.UpdateTelemetry(e.GpuData);
                SettingsVM.RefreshHardwareInfo();

                _telemetrySuccessCount++;
                if (IsLoading && (_telemetrySuccessCount >= 2 || e.Fans.Count > 0))
                {
                    IsLoading = false;
                }
            });
        }

        public void Shutdown()
        {
            _engine.Dispose();
            _settingsService.Save();
        }
    }
}
