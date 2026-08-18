using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;

namespace TempTest
{
    public class SensorItem : INotifyPropertyChanged
    {
        private float _currentVal;
        private float _minVal = float.MaxValue;
        private float _maxVal = float.MinValue;
        private int _rawVal;
        private int _supp;

        public string Id { get; set; } = "";
        public string Name { get; set; } = "";
        public string Category { get; set; } = "TEMP"; // TEMP, POWER, CLOCK, OTHER
        public string Unit { get; set; } = "°C";

        public float CurrentVal
        {
            get => _currentVal;
            set
            {
                if (Math.Abs(_currentVal - value) > 0.01f)
                {
                    _currentVal = value;
                    if (_currentVal < _minVal) _minVal = _currentVal;
                    if (_currentVal > _maxVal) _maxVal = _currentVal;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(FormattedValue));
                    OnPropertyChanged(nameof(FormattedMin));
                    OnPropertyChanged(nameof(FormattedMax));
                    OnPropertyChanged(nameof(ValueColorBrush));
                }
            }
        }

        public int RawValue
        {
            get => _rawVal;
            set
            {
                if (_rawVal != value)
                {
                    _rawVal = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(HexValue));
                }
            }
        }

        public int Supported
        {
            get => _supp;
            set
            {
                if (_supp != value)
                {
                    _supp = value;
                    OnPropertyChanged();
                    OnPropertyChanged(nameof(SupportedText));
                }
            }
        }

        public string HexValue => $"0x{_rawVal:X4}";
        public string SupportedText => _supp == 1 ? "Yes (1)" : "No (0)";

        public string FormattedValue => $"{_currentVal:F1} {Unit}";
        public string FormattedMin => _minVal == float.MaxValue ? "-" : $"{_minVal:F1} {Unit}";
        public string FormattedMax => _maxVal == float.MinValue ? "-" : $"{_maxVal:F1} {Unit}";

        public Brush ValueColorBrush
        {
            get
            {
                if (Category == "TEMP")
                {
                    if (_currentVal >= 85) return new SolidColorBrush(Color.FromRgb(239, 68, 68));   // Red
                    if (_currentVal >= 70) return new SolidColorBrush(Color.FromRgb(249, 115, 22));  // Orange
                    if (_currentVal >= 55) return new SolidColorBrush(Color.FromRgb(234, 179, 8));   // Yellow
                    return new SolidColorBrush(Color.FromRgb(56, 189, 248));                         // Sky Blue
                }
                if (Category == "POWER")
                {
                    return new SolidColorBrush(Color.FromRgb(168, 85, 247)); // Purple
                }
                if (Category == "CLOCK")
                {
                    return new SolidColorBrush(Color.FromRgb(34, 197, 94));  // Green
                }
                return new SolidColorBrush(Color.FromRgb(240, 246, 252));
            }
        }

        public void ResetMinMax()
        {
            _minVal = _currentVal;
            _maxVal = _currentVal;
            OnPropertyChanged(nameof(FormattedMin));
            OnPropertyChanged(nameof(FormattedMax));
        }

        public event PropertyChangedEventHandler? PropertyChanged;
        protected void OnPropertyChanged([CallerMemberName] string? prop = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(prop));
        }
    }

    public partial class MainWindow : Window
    {
        private const string AtiAdlDll = "atiadlxx.dll";

        [UnmanagedFunctionPointer(CallingConvention.Cdecl)]
        private delegate IntPtr ADL_Main_Memory_AllocDelegate(int size);
        private static IntPtr ADL_Main_Memory_Alloc(int size) => Marshal.AllocHGlobal(size);
        private static readonly ADL_Main_Memory_AllocDelegate AllocCallback = ADL_Main_Memory_Alloc;

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Main_Control_Create(ADL_Main_Memory_AllocDelegate callback, int enumConnectedAdapters, out IntPtr context);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Main_Control_Destroy(IntPtr context);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_NumberOfAdapters_Get(IntPtr context, out int numAdapters);

        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Ansi)]
        private struct ADLAdapterInfo
        {
            public int Size;
            public int AdapterIndex;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string UDID;
            public int BusNumber;
            public int DeviceNumber;
            public int FunctionNumber;
            public int VendorID;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string AdapterName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DisplayName;
            public int Present;
            public int Exist;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DriverPath;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string DriverPathExt;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 256)]
            public string PNPString;
            public int OSDisplayIndex;
        }

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_Adapter_AdapterInfo_Get(IntPtr context, IntPtr info, int inputSize);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_New_QueryPMLogData_Get(IntPtr context, int adapterIndex, IntPtr pPMLogDataOutput);

        [DllImport(AtiAdlDll, CallingConvention = CallingConvention.Cdecl)]
        private static extern int ADL2_OverdriveN_Temperature_Get(IntPtr context, int adapterIndex, int tempType, out int iTemperature);

        private IntPtr _context = IntPtr.Zero;
        private int _adapterIndex = -1;
        private IntPtr _pQueryBuffer = IntPtr.Zero;
        private readonly DispatcherTimer _timer = new();
        private readonly List<SensorItem> _allSensors = new();
        private readonly ObservableCollection<SensorItem> _viewSensors = new();
        private string _activeFilter = "ALL";
        private string _searchFilter = "";

        public MainWindow()
        {
            InitializeComponent();
            SensorGrid.ItemsSource = _viewSensors;

            InitAdl();
            SetupSensorList();

            _timer.Interval = TimeSpan.FromMilliseconds(300);
            _timer.Tick += Timer_Tick;
            _timer.Start();
        }

        private void InitAdl()
        {
            try
            {
                int ret = ADL2_Main_Control_Create(AllocCallback, 1, out _context);
                if (ret == 0 && _context != IntPtr.Zero)
                {
                    ADL2_Adapter_NumberOfAdapters_Get(_context, out int numAdapters);
                    int adapterInfoSize = Marshal.SizeOf(typeof(ADLAdapterInfo));
                    IntPtr ptr = Marshal.AllocHGlobal(adapterInfoSize * numAdapters);
                    for (int i = 0; i < numAdapters; i++)
                    {
                        Marshal.WriteInt32(new IntPtr(ptr.ToInt64() + i * adapterInfoSize), adapterInfoSize);
                    }
                    ADL2_Adapter_AdapterInfo_Get(_context, ptr, adapterInfoSize * numAdapters);

                    for (int i = 0; i < numAdapters; i++)
                    {
                        var info = Marshal.PtrToStructure<ADLAdapterInfo>(new IntPtr(ptr.ToInt64() + i * adapterInfoSize));
                        if (info.Exist == 0) continue;
                        if (info.VendorID == 0x1002 || (!string.IsNullOrEmpty(info.AdapterName) && info.AdapterName.Contains("Radeon", StringComparison.OrdinalIgnoreCase)))
                        {
                            _adapterIndex = info.AdapterIndex;
                            GpuNameText.Text = $"GPU: {info.AdapterName} (Adapter Index #{_adapterIndex}, Bus #{info.BusNumber})";
                            break;
                        }
                    }
                    Marshal.FreeHGlobal(ptr);

                    if (_adapterIndex >= 0)
                    {
                        _pQueryBuffer = Marshal.AllocHGlobal(4096);
                    }
                }
            }
            catch (Exception ex)
            {
                GpuNameText.Text = $"Lỗi kết nối AMD ADL: {ex.Message}";
            }
        }

        private void SetupSensorList()
        {
            _allSensors.Clear();

            // 1. PMLog known sensors
            var pmlogDefinitions = new (int Id, string Name, string Cat, string Unit)[]
            {
                (0, "PMLOG_CLK_GFXCLK_RAW", "CLOCK", "MHz"),
                (1, "PMLOG_CLK_GFXCLK (Core Clock)", "CLOCK", "MHz"),
                (2, "PMLOG_CLK_MEMCLK (Memory Clock)", "CLOCK", "MHz"),
                (3, "PMLOG_CLK_SOCCLK (SoC Clock)", "CLOCK", "MHz"),
                (4, "PMLOG_CLK_UVDCLK1", "CLOCK", "MHz"),
                (5, "PMLOG_CLK_UVDCLK2", "CLOCK", "MHz"),
                (6, "PMLOG_CLK_VCECLK", "CLOCK", "MHz"),
                (7, "PMLOG_CLK_VCLK", "CLOCK", "MHz"),
                (8, "PMLOG_TEMPERATURE_EDGE (GPU Core Temp)", "TEMP", "°C"),
                (9, "PMLOG_TEMPERATURE_MEM (GPU HBM/VRAM Temp)", "TEMP", "°C"),
                (10, "PMLOG_TEMPERATURE_VRVDDC (VRM VDDC Temp)", "TEMP", "°C"),
                (11, "PMLOG_TEMPERATURE_VRMVDD", "TEMP", "°C"),
                (12, "PMLOG_TEMPERATURE_LIQUID", "TEMP", "°C"),
                (13, "PMLOG_TEMPERATURE_PLX", "TEMP", "°C"),
                (14, "PMLOG_TEMPERATURE_HOTSPOT (ADL Sensor #14)", "TEMP", "°C"),
                (15, "PMLOG_TEMPERATURE_SOC", "TEMP", "°C"),
                (16, "PMLOG_TEMPERATURE_VRMVDD0", "TEMP", "°C"),
                (17, "PMLOG_TEMPERATURE_VRMVDD1", "TEMP", "°C"),
                (18, "PMLOG_TEMPERATURE_VRSOC", "TEMP", "°C"),
                (19, "PMLOG_TEMPERATURE_VRMVDD2", "TEMP", "°C"),
                (20, "PMLOG_TEMPERATURE_VRMVDD3", "TEMP", "°C"),
                (21, "PMLOG_INFO_ACTIVITY_GFX (GPU Usage %)", "OTHER", "%"),
                (22, "PMLOG_INFO_ACTIVITY_MEM (Mem Usage %)", "OTHER", "%"),
                (23, "PMLOG_INFO_ACTIVITY_UVD", "OTHER", "%"),
                (24, "PMLOG_TEMPERATURE_VRVDDIO (Sensor #24)", "TEMP", "°C"),
                (25, "PMLOG_TEMPERATURE_HOTSPOT (HWiNFO Sensor #25)", "TEMP", "°C"),
                (26, "PMLOG_INFO_TOTAL_BOARD_POWER (Total Power)", "POWER", "W"),
                (27, "PMLOG_INFO_ASIC_POWER (GPU ASIC Power)", "POWER", "W"),
                (28, "PMLOG_INFO_VDDCR_GFX_VOLTAGE", "OTHER", "mV"),
                (29, "PMLOG_INFO_VDDCR_SOC_VOLTAGE", "OTHER", "mV"),
                (30, "PMLOG_INFO_VDDCR_MEM_VOLTAGE", "OTHER", "mV"),
                (31, "PMLOG_INFO_GFX_CURRENT", "OTHER", "A"),
                (32, "PMLOG_INFO_SOC_CURRENT", "OTHER", "A"),
                (33, "PMLOG_INFO_MEM_CURRENT", "OTHER", "A")
            };

            foreach (var def in pmlogDefinitions)
            {
                _allSensors.Add(new SensorItem
                {
                    Id = $"PMLog #{def.Id}",
                    Name = def.Name,
                    Category = def.Cat,
                    Unit = def.Unit
                });
            }

            // Extended PMLog sensors from 34 to 63
            for (int s = 34; s < 64; s++)
            {
                _allSensors.Add(new SensorItem
                {
                    Id = $"PMLog #{s}",
                    Name = $"PMLOG_SENSOR_REGISTER_0x{s:X2}",
                    Category = "TEMP",
                    Unit = "°C"
                });
            }

            // OverdriveN Temperature Types
            var odnNames = new[] { "ODN_EDGE_TEMP", "ODN_HOTSPOT_TEMP", "ODN_MEM_TEMP", "ODN_VRVDDC_TEMP", "ODN_VRMVDD_TEMP", "ODN_LIQUID_TEMP", "ODN_PLX_TEMP" };
            for (int t = 0; t < odnNames.Length; t++)
            {
                _allSensors.Add(new SensorItem
                {
                    Id = $"OverdriveN #{t}",
                    Name = odnNames[t],
                    Category = "TEMP",
                    Unit = "°C"
                });
            }

            ApplyFilters();
        }

        private void Timer_Tick(object? sender, EventArgs e)
        {
            if (_context == IntPtr.Zero || _adapterIndex < 0) return;

            // 1. Read PMLog buffer
            if (_pQueryBuffer != IntPtr.Zero)
            {
                Marshal.WriteInt32(_pQueryBuffer, 0, 4096);
                int ret = ADL2_New_QueryPMLogData_Get(_context, _adapterIndex, _pQueryBuffer);
                if (ret == 0)
                {
                    for (int s = 0; s < 64; s++)
                    {
                        var item = _allSensors.FirstOrDefault(x => x.Id == $"PMLog #{s}");
                        if (item != null)
                        {
                            int val = Marshal.ReadInt32(_pQueryBuffer, 8 + s * 8);
                            int supp = Marshal.ReadInt32(_pQueryBuffer, 8 + s * 8 + 4);
                            item.RawValue = val;
                            item.Supported = supp;

                            if (item.Category == "TEMP")
                            {
                                item.CurrentVal = (val >= 1000 && val < 150000) ? (val / 1000.0f) : val;
                            }
                            else
                            {
                                item.CurrentVal = val;
                            }
                        }
                    }
                }
            }

            // 2. Read OverdriveN temperatures
            for (int t = 0; t <= 6; t++)
            {
                var item = _allSensors.FirstOrDefault(x => x.Id == $"OverdriveN #{t}");
                if (item != null)
                {
                    int ret = ADL2_OverdriveN_Temperature_Get(_context, _adapterIndex, t, out int temp);
                    item.RawValue = temp;
                    item.Supported = ret == 0 ? 1 : 0;
                    item.CurrentVal = ret == 0 ? (temp / 1000.0f) : 0;
                }
            }
        }

        private void ApplyFilters()
        {
            _viewSensors.Clear();
            foreach (var s in _allSensors)
            {
                bool matchCat = _activeFilter == "ALL" || s.Category == _activeFilter;
                bool matchSearch = string.IsNullOrWhiteSpace(_searchFilter) ||
                                   s.Id.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase) ||
                                   s.Name.Contains(_searchFilter, StringComparison.OrdinalIgnoreCase);

                if (matchCat && matchSearch)
                {
                    _viewSensors.Add(s);
                }
            }
        }

        private void Filter_Checked(object sender, RoutedEventArgs e)
        {
            if (sender is RadioButton rb && rb.Tag is string tag)
            {
                _activeFilter = tag;
                ApplyFilters();
            }
        }

        private void SearchBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            _searchFilter = SearchBox.Text.Trim();
            ApplyFilters();
        }

        private void ResetMinMax_Click(object sender, RoutedEventArgs e)
        {
            foreach (var s in _allSensors)
            {
                s.ResetMinMax();
            }
        }

        protected override void OnClosed(EventArgs e)
        {
            _timer.Stop();
            if (_pQueryBuffer != IntPtr.Zero)
            {
                Marshal.FreeHGlobal(_pQueryBuffer);
                _pQueryBuffer = IntPtr.Zero;
            }
            if (_context != IntPtr.Zero)
            {
                ADL2_Main_Control_Destroy(_context);
                _context = IntPtr.Zero;
            }
            base.OnClosed(e);
        }
    }
}
