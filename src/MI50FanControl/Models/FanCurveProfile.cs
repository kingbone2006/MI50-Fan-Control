using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace MI50FanControl.Models
{
    public class CurvePoint : IComparable<CurvePoint>, INotifyPropertyChanged
    {
        private float _temp;
        private float _speed;

        public float Temperature
        {
            get => _temp;
            set
            {
                if (Math.Abs(_temp - value) > 0.01f)
                {
                    _temp = value;
                    OnPropertyChanged();
                }
            }
        }

        public float FanSpeedPercent
        {
            get => _speed;
            set
            {
                float clamped = Math.Clamp(value, 0f, 100f);
                if (Math.Abs(_speed - clamped) > 0.01f)
                {
                    _speed = clamped;
                    OnPropertyChanged();
                }
            }
        }

        public CurvePoint() { }

        public CurvePoint(float temp, float speed)
        {
            _temp = temp;
            _speed = Math.Clamp(speed, 0f, 100f);
        }

        public int CompareTo(CurvePoint? other)
        {
            if (other == null) return 1;
            return Temperature.CompareTo(other.Temperature);
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }

    public class FanCurveProfile : INotifyPropertyChanged
    {
        public string Id { get; set; } = Guid.NewGuid().ToString();
        public string Name { get; set; } = "Default Profile";
        public List<CurvePoint> Points { get; set; } = new();

        public string LocalizedName
        {
            get
            {
                return Id switch
                {
                    "silent" => MI50FanControl.Services.LocalizationService.Instance.Get("ProfileName_Silent", Name),
                    "balanced" => MI50FanControl.Services.LocalizationService.Instance.Get("ProfileName_Balanced", Name),
                    "performance" => MI50FanControl.Services.LocalizationService.Instance.Get("ProfileName_Performance", Name),
                    "aggressive" => MI50FanControl.Services.LocalizationService.Instance.Get("ProfileName_Aggressive", Name),
                    _ => Name
                };
            }
        }

        public event PropertyChangedEventHandler? PropertyChanged;

        public FanCurveProfile()
        {
            MI50FanControl.Services.LocalizationService.Instance.LanguageChanged += (s, e) =>
            {
                PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(LocalizedName)));
            };
        }

        public float CalculateFanSpeed(float temperature)
        {
            if (Points == null || Points.Count == 0) return 50f;
            if (Points.Count == 1) return Points[0].FanSpeedPercent;

            var sorted = Points.OrderBy(p => p.Temperature).ToList();

            if (temperature <= sorted[0].Temperature)
            {
                return sorted[0].FanSpeedPercent;
            }

            if (temperature >= sorted[^1].Temperature)
            {
                return sorted[^1].FanSpeedPercent;
            }

            for (int i = 0; i < sorted.Count - 1; i++)
            {
                var p1 = sorted[i];
                var p2 = sorted[i + 1];

                if (temperature >= p1.Temperature && temperature <= p2.Temperature)
                {
                    float range = p2.Temperature - p1.Temperature;
                    if (range <= 0.001f) return p1.FanSpeedPercent;

                    float factor = (temperature - p1.Temperature) / range;
                    float speed = p1.FanSpeedPercent + factor * (p2.FanSpeedPercent - p1.FanSpeedPercent);
                    return Math.Clamp(speed, 0f, 100f);
                }
            }

            return sorted[^1].FanSpeedPercent;
        }

        public static List<FanCurveProfile> CreateDefaultProfiles()
        {
            return new List<FanCurveProfile>
            {
                new FanCurveProfile
                {
                    Id = "silent",
                    Name = "Silent (Yên Tĩnh)",
                    Points = new List<CurvePoint>
                    {
                        new CurvePoint(30, 20),
                        new CurvePoint(50, 30),
                        new CurvePoint(65, 45),
                        new CurvePoint(75, 65),
                        new CurvePoint(85, 100)
                    }
                },
                new FanCurveProfile
                {
                    Id = "balanced",
                    Name = "Balanced (Cân Bằng - Mặc định)",
                    Points = new List<CurvePoint>
                    {
                        new CurvePoint(30, 25),
                        new CurvePoint(45, 35),
                        new CurvePoint(60, 55),
                        new CurvePoint(72, 80),
                        new CurvePoint(82, 100)
                    }
                },
                new FanCurveProfile
                {
                    Id = "performance",
                    Name = "Performance (Hiệu Năng Cao)",
                    Points = new List<CurvePoint>
                    {
                        new CurvePoint(30, 35),
                        new CurvePoint(45, 50),
                        new CurvePoint(60, 75),
                        new CurvePoint(70, 95),
                        new CurvePoint(78, 100)
                    }
                },
                new FanCurveProfile
                {
                    Id = "aggressive",
                    Name = "Aggressive (Làm Mát Tối Đa)",
                    Points = new List<CurvePoint>
                    {
                        new CurvePoint(30, 45),
                        new CurvePoint(45, 65),
                        new CurvePoint(55, 85),
                        new CurvePoint(65, 100)
                    }
                }
            };
        }
    }
}
