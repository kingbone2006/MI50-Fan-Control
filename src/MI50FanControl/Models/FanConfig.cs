namespace MI50FanControl.Models
{
    public enum FanControlMode
    {
        FollowCurve = 0,
        FixedManual = 1,
        BiosDefault = 2
    }

    public class FanConfig
    {
        public string FanIdentifier { get; set; } = string.Empty;
        public string CustomName { get; set; } = string.Empty;
        public FanControlMode Mode { get; set; } = FanControlMode.FollowCurve;
        public float FixedSpeedPercent { get; set; } = 50f;
        public float MinSafePwmPercent { get; set; } = 20f;
        public float MaxSafePwmPercent { get; set; } = 100f;
    }
}
