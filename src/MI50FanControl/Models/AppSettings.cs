using System.Collections.Generic;

namespace MI50FanControl.Models
{
    public enum GpuSensorSource
    {
        GpuHotSpot = 0,
        GpuCore = 1
    }

    public class AppSettings
    {
        public string Language { get; set; } = "vi";
        public GpuSensorSource SelectedSensor { get; set; } = GpuSensorSource.GpuHotSpot;
        public string ActiveCurveProfileId { get; set; } = "balanced";
        public List<FanCurveProfile> CurveProfiles { get; set; } = FanCurveProfile.CreateDefaultProfiles();
        public List<FanConfig> FanConfigs { get; set; } = new();

        public bool StartWithWindows { get; set; } = false;
        public bool MinimizeToTrayOnClose { get; set; } = true;
        public bool MinimizeToTrayOnMinimize { get; set; } = true;

        public bool EmergencyProtectionEnabled { get; set; } = true;
        public float EmergencyTempThreshold { get; set; } = 90f;

        public float HysteresisDegrees { get; set; } = 2.0f;
        public float SmoothingRatePercentPerSec { get; set; } = 8.0f;
        public int PollingIntervalMs { get; set; } = 800;

        public bool GlobalManualOverride { get; set; } = false;
        public float GlobalManualSpeedPercent { get; set; } = 60f;

        public bool DeveloperMode { get; set; } = true;
    }
}
