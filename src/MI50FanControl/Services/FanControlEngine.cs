using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MI50FanControl.Hardware;
using MI50FanControl.Models;

namespace MI50FanControl.Services
{
    public class FanTelemetryEventArgs : EventArgs
    {
        public AmdGpuTelemetryData GpuData { get; }
        public IReadOnlyList<HardwareFanItem> Fans { get; }
        public float TargetPwm { get; }
        public bool IsEmergencyOverheat { get; }
        public bool IsTest100Running { get; }
        public int Test100SecondsLeft { get; }

        public FanTelemetryEventArgs(AmdGpuTelemetryData gpuData, IReadOnlyList<HardwareFanItem> fans, float targetPwm, bool isEmergency, bool isTest100, int testSecLeft)
        {
            GpuData = gpuData;
            Fans = fans;
            TargetPwm = targetPwm;
            IsEmergencyOverheat = isEmergency;
            IsTest100Running = isTest100;
            Test100SecondsLeft = testSecLeft;
        }
    }

    public class FanControlEngine : IDisposable
    {
        private readonly SettingsService _settingsService;
        private readonly AmdGpuTelemetry _gpuTelemetry;
        private readonly SuperIoHardwareManager _superIoManager;

        private CancellationTokenSource? _cts;
        private Task? _monitoringTask;

        private float _smoothedTemp = 0f;
        private float _lockedTemp = 0f;
        private float _currentPwm = 30f;
        private DateTime _lastUpChangeTime = DateTime.MinValue;
        private DateTime _lastDownChangeTime = DateTime.MinValue;
        private DateTime _tempDropStartTime = DateTime.MinValue;

        private bool _isTest100Running = false;
        private int _test100SecondsRemaining = 0;
        private DateTime _test100EndTime = DateTime.MinValue;

        public event EventHandler<FanTelemetryEventArgs>? TelemetryUpdated;

        public SuperIoHardwareManager SuperIo => _superIoManager;
        public AmdGpuTelemetry GpuTelemetry => _gpuTelemetry;

        public FanControlEngine(SettingsService settingsService)
        {
            _settingsService = settingsService;
            _gpuTelemetry = new AmdGpuTelemetry();
            _superIoManager = new SuperIoHardwareManager();
        }

        public void Start()
        {
            LogService.Instance.Info("Engine", "Bắt đầu khởi động FanControlEngine...");

            // 1. Initialize AMD GPU ADL Telemetry
            bool adlOk = _gpuTelemetry.Initialize();
            if (adlOk)
            {
                LogService.Instance.Success("Engine", $"Kết nối GPU ADL thành công: {_gpuTelemetry.GpuName}");
            }
            else
            {
                LogService.Instance.Warn("Engine", "Không thể kết nối ADL GPU (Có thể đang chạy GPU khác hoặc thiếu driver).");
            }

            // 2. Initialize Motherboard SuperIO Fan Controller
            bool superIoOk = _superIoManager.Initialize();
            if (superIoOk)
            {
                LogService.Instance.Success("Engine", $"Kết nối SuperIO thành công trên bo mạch: {_superIoManager.MotherboardName}");
            }
            else
            {
                LogService.Instance.Warn("Engine", "SuperIO chưa phản hồi. Sẽ tự động thử lại trong vòng lặp.");
            }

            // 3. Start Control Loop
            _cts = new CancellationTokenSource();
            _monitoringTask = Task.Run(() => MonitoringLoopAsync(_cts.Token));
            LogService.Instance.Success("Engine", "Lõi điều khiển quạt phần cứng đã kích hoạt thành công!");
        }

        public void Stop()
        {
            _cts?.Cancel();
            try
            {
                _monitoringTask?.Wait(2000);
            }
            catch { }

            _gpuTelemetry.Dispose();
            _superIoManager.Dispose();
            LogService.Instance.Info("Engine", "FanControlEngine đã dừng an toàn.");
        }

        public void TriggerTest100Percent(int durationSeconds = 5)
        {
            _isTest100Running = true;
            _test100SecondsRemaining = durationSeconds;
            _test100EndTime = DateTime.UtcNow.AddSeconds(durationSeconds);
            LogService.Instance.Hardware("Test 100%", $"Bắt đầu test quạt 100% trong {durationSeconds} giây.");
        }

        public void CancelTest100Percent()
        {
            _isTest100Running = false;
            _test100SecondsRemaining = 0;
            LogService.Instance.Info("Test 100%", "Đã hủy chế độ test quạt 100%.");
        }

        private async Task MonitoringLoopAsync(CancellationToken token)
        {
            int loopCount = 0;

            while (!token.IsCancellationRequested)
            {
                try
                {
                    loopCount++;
                    // Periodic dynamic rescan every ~10 loops (5 seconds)
                    if (loopCount % 10 == 0)
                    {
                        _superIoManager.RefreshActiveFans();
                    }

                    // 1. Read AMD GPU Telemetry
                    var gpuData = _gpuTelemetry.ReadTelemetry();

                    // 2. Read Motherboard Fans Telemetry
                    _superIoManager.UpdateTelemetry();

                    // 3. Determine Control Temperature (Trực tiếp theo GPU Core Temp)
                    float currentTemp = gpuData.CoreTemperature > 0 ? gpuData.CoreTemperature : gpuData.MemoryTemperature;

                    // 4. Check Test 100% Status
                    if (_isTest100Running)
                    {
                        var remaining = (_test100EndTime - DateTime.UtcNow).TotalSeconds;
                        if (remaining > 0)
                        {
                            _test100SecondsRemaining = (int)Math.Ceiling(remaining);
                        }
                        else
                        {
                            _isTest100Running = false;
                            _test100SecondsRemaining = 0;
                            LogService.Instance.Info("Test 100%", "Chế độ Test 100% kết thúc. Khôi phục điều khiển theo đường cong.");
                        }
                    }

                    // 5. Calculate Target Fan Speed (Step-by-step 1% smooth engine)
                    float appliedPwm;
                    bool isEmergency = false;

                    if (_isTest100Running)
                    {
                        appliedPwm = 100f;
                    }
                    else if (_settingsService.Current.GlobalManualOverride)
                    {
                        appliedPwm = _settingsService.Current.GlobalManualSpeedPercent;
                    }
                    else if (_settingsService.Current.EmergencyProtectionEnabled && currentTemp >= _settingsService.Current.EmergencyTempThreshold)
                    {
                        appliedPwm = 100f;
                        isEmergency = true;
                        LogService.Instance.Warn("Safety", $"CẢNH BÁO QUÁ NHIỆT: Nhiệt độ GPU đạt {currentTemp:F0}°C >= {_settingsService.Current.EmergencyTempThreshold}°C! Tự động bật quạt 100%!");
                    }
                    else
                    {
                        var activeProfile = _settingsService.Current.CurveProfiles
                            .FirstOrDefault(p => p.Id == _settingsService.Current.ActiveCurveProfileId)
                            ?? _settingsService.Current.CurveProfiles.FirstOrDefault();

                        appliedPwm = ApplyHysteresisAndSmoothing(activeProfile, currentTemp);
                    }

                    // 6. Dispatch PWM to Hardware
                    DispatchSpeedToHardware(appliedPwm, isEmergency);

                    // 7. Fire Telemetry Event
                    TelemetryUpdated?.Invoke(this, new FanTelemetryEventArgs(
                        gpuData,
                        _superIoManager.ActiveFans,
                        appliedPwm,
                        isEmergency,
                        _isTest100Running,
                        _test100SecondsRemaining));
                }
                catch (Exception ex)
                {
                    LogService.Instance.Debug("Engine Loop", $"Lỗi vòng lặp giám sát: {ex.Message}");
                }

                await Task.Delay(500, token);
            }
        }

        private float ApplyHysteresisAndSmoothing(FanCurveProfile? activeProfile, float rawTemp)
        {
            if (activeProfile == null) return 45f;
            if (_isTest100Running) return 100f;
            if (_settingsService.Current.GlobalManualOverride) return _settingsService.Current.GlobalManualSpeedPercent;

            if (_smoothedTemp <= 0)
            {
                _smoothedTemp = rawTemp;
                _lockedTemp = rawTemp;
                _currentPwm = (float)Math.Round(activeProfile.CalculateFanSpeed(rawTemp));
                return _currentPwm;
            }

            // 1. Exponential Moving Average for Temperature (eliminates 1-2 degree sensor fluctuations)
            _smoothedTemp = (_smoothedTemp * 0.85f) + (rawTemp * 0.15f);

            DateTime now = DateTime.UtcNow;

            // 2. Temperature Plateau & Hysteresis Lock
            // When heating up: Only elevate target if smoothed temp rises by >= +1.5°C
            if (_smoothedTemp >= _lockedTemp + 1.5f)
            {
                _lockedTemp = _smoothedTemp;
                _tempDropStartTime = DateTime.MinValue;
            }
            // When cooling down: Only lower target if smoothed temp drops by >= -2.0°C and stays cool for 4 seconds
            else if (_smoothedTemp <= _lockedTemp - 2.0f)
            {
                if (_tempDropStartTime == DateTime.MinValue)
                {
                    _tempDropStartTime = now;
                }

                if ((now - _tempDropStartTime).TotalSeconds >= 4.0)
                {
                    _lockedTemp = _smoothedTemp;
                    _tempDropStartTime = DateTime.MinValue;
                }
            }
            else
            {
                // Temperature is hovering within deadband, reset cooldown timer and hold steady!
                _tempDropStartTime = DateTime.MinValue;
            }

            // 3. Calculate target PWM strictly from the stabilized locked temperature
            float targetCurvePwm = activeProfile.CalculateFanSpeed(_lockedTemp);
            int roundedTargetPwm = (int)Math.Round(targetCurvePwm);
            int roundedCurrentPwm = (int)Math.Round(_currentPwm);

            // 4. Smooth Step-by-Step 1% adjustment logic
            if (roundedTargetPwm > roundedCurrentPwm)
            {
                // Ramping UP: Tăng từng 1% đều đặn mỗi 1000ms (1.0 giây)
                if ((now - _lastUpChangeTime).TotalMilliseconds >= 1000)
                {
                    _currentPwm += 1.0f;
                    _lastUpChangeTime = now;
                }
            }
            else if (roundedTargetPwm < roundedCurrentPwm)
            {
                // Ramping DOWN: Giảm từng 1% êm ái mỗi 1500ms (1.5 giây)
                if ((now - _lastDownChangeTime).TotalMilliseconds >= 1500)
                {
                    _currentPwm -= 1.0f;
                    _lastDownChangeTime = now;
                }
            }

            _currentPwm = Math.Clamp(_currentPwm, 0f, 100f);
            return (float)Math.Round(_currentPwm);
        }

        private void DispatchSpeedToHardware(float globalPwm, bool isEmergency)
        {
            if (_isTest100Running || isEmergency)
            {
                _superIoManager.SetAllFansSpeed(100f);
                _gpuTelemetry.SetGpuFanSpeedPercent(100f);
                return;
            }

            var fanConfigs = _settingsService.Current.FanConfigs;

            foreach (var fan in _superIoManager.ActiveFans)
            {
                var cfg = fanConfigs.FirstOrDefault(c => c.FanIdentifier == fan.Identifier || c.FanIdentifier == fan.Id);

                float targetPwm = globalPwm;
                if (cfg != null)
                {
                    switch (cfg.Mode)
                    {
                        case FanControlMode.BiosDefault:
                            _superIoManager.RestoreBiosControl(fan.Id);
                            continue;

                        case FanControlMode.FixedManual:
                            targetPwm = cfg.FixedSpeedPercent;
                            break;

                        case FanControlMode.FollowCurve:
                        default:
                            targetPwm = globalPwm;
                            break;
                    }

                    targetPwm = Math.Clamp(targetPwm, cfg.MinSafePwmPercent, cfg.MaxSafePwmPercent);
                }

                _superIoManager.SetFanSpeed(fan.Id, targetPwm);
            }
        }

        public void Dispose()
        {
            Stop();
        }
    }
}
