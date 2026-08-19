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

        private float _filteredTemp = 0f;
        private float _referenceTemp = 0f;
        private float _currentPwm = 30f;
        private DateTime _lastCalculationTime = DateTime.UtcNow;
        private DateTime _cooldownHoldStartTime = DateTime.MinValue;

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

                    // 3. Determine Control Temperature
                    float currentTemp;
                    if (_settingsService.Current.SelectedSensor == GpuSensorSource.GpuHotSpot && gpuData.MemoryTemperature > 0)
                    {
                        currentTemp = Math.Max(gpuData.CoreTemperature, gpuData.MemoryTemperature);
                    }
                    else
                    {
                        currentTemp = gpuData.CoreTemperature > 0 ? gpuData.CoreTemperature : gpuData.MemoryTemperature;
                    }

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

                    // 5. Calculate Target Fan Speed (Continuous slew-rate & hysteresis)
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

                int delayMs = Math.Clamp(_settingsService.Current.PollingIntervalMs, 200, 2000);
                await Task.Delay(delayMs, token);
            }
        }

        private float ApplyHysteresisAndSmoothing(FanCurveProfile? activeProfile, float rawTemp)
        {
            if (activeProfile == null) return 45f;
            if (_isTest100Running) return 100f;
            if (_settingsService.Current.GlobalManualOverride) return _settingsService.Current.GlobalManualSpeedPercent;

            DateTime now = DateTime.UtcNow;
            float dt = (float)(now - _lastCalculationTime).TotalSeconds;
            _lastCalculationTime = now;
            if (dt <= 0f || dt > 3f) dt = 0.5f;

            if (rawTemp <= 0f) rawTemp = 40f;

            // 1. Initial State Setup
            if (_filteredTemp <= 0f)
            {
                _filteredTemp = rawTemp;
                _referenceTemp = rawTemp;
                _currentPwm = activeProfile.CalculateFanSpeed(rawTemp);
                return (float)Math.Round(_currentPwm);
            }

            // 2. Exponential Moving Average (EMA) Sensor Filter
            // Time constant Tau = 3.0s smoothly dampens 1-2 degree sensor jitter
            float tau = 3.0f;
            float alpha = dt / (tau + dt);
            _filteredTemp = _filteredTemp + alpha * (rawTemp - _filteredTemp);

            // 3. Intelligent Asymmetric Temperature Hysteresis
            float hysteresis = Math.Max(0.5f, _settingsService.Current.HysteresisDegrees);

            if (_filteredTemp > _referenceTemp)
            {
                // Heating up: follow temperature upwards smoothly to react before overheating
                _referenceTemp = _filteredTemp;
                _cooldownHoldStartTime = DateTime.MinValue;
            }
            else if (_filteredTemp < _referenceTemp - hysteresis)
            {
                // Cooling down: Require sustained lower temperature before lowering reference temp
                if (_cooldownHoldStartTime == DateTime.MinValue)
                {
                    _cooldownHoldStartTime = now;
                }

                // Hold fan speed for 3.5 seconds to eliminate hunting / wave oscillation
                if ((now - _cooldownHoldStartTime).TotalSeconds >= 3.5)
                {
                    // Smoothly glide reference temperature downwards (max 1.5°C/s)
                    float maxDrop = 1.5f * dt;
                    float targetRef = _filteredTemp + (hysteresis * 0.4f);
                    if (_referenceTemp > targetRef)
                    {
                        _referenceTemp = Math.Max(targetRef, _referenceTemp - maxDrop);
                    }
                }
            }
            else
            {
                // Inside deadband: reset cooldown timer, hold reference temperature constant!
                _cooldownHoldStartTime = DateTime.MinValue;
            }

            // 4. Calculate target PWM from the stabilized reference temperature
            float targetPwm = activeProfile.CalculateFanSpeed(_referenceTemp);
            targetPwm = Math.Clamp(targetPwm, 0f, 100f);

            // 5. Dynamic Slew-Rate Limiting (Ramping)
            float smoothingRate = Math.Max(1.0f, _settingsService.Current.SmoothingRatePercentPerSec); // % per second

            if (targetPwm > _currentPwm)
            {
                // Ramping UP: Responsive and smooth (e.g. 10-15%/s)
                float upRate = Math.Max(smoothingRate, 10.0f);
                float maxUpStep = upRate * dt;
                _currentPwm = Math.Min(targetPwm, _currentPwm + maxUpStep);
            }
            else if (targetPwm < _currentPwm)
            {
                // Ramping DOWN: Quiet and gentle (e.g. 3-6%/s)
                float downRate = Math.Max(2.0f, smoothingRate * 0.6f);
                float maxDownStep = downRate * dt;
                _currentPwm = Math.Max(targetPwm, _currentPwm - maxDownStep);
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
                    if (cfg.Mode == FanControlMode.BiosDefault)
                    {
                        // Giữ nguyên BIOS Default, không can thiệp đè PWM
                        continue;
                    }
                    else if (cfg.Mode == FanControlMode.FixedManual)
                    {
                        targetPwm = cfg.FixedSpeedPercent;
                    }
                    else
                    {
                        targetPwm = globalPwm;
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
