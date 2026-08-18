using System;
using System.Diagnostics;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace FanTest
{
    internal class Program
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowsProc lpEnumFunc, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool EnumChildWindows(IntPtr hWndParent, EnumWindowsProc lpEnumFunc, IntPtr lParam);

        private delegate bool EnumWindowsProc(IntPtr hWnd, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr hWnd, out uint lpdwProcessId);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetWindowText(IntPtr hWnd, StringBuilder lpString, int nMaxCount);

        [DllImport("user32.dll", CharSet = CharSet.Auto)]
        private static extern int GetClassName(IntPtr hWnd, StringBuilder lpClassName, int nMaxCount);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, string lParam);

        [DllImport("user32.dll")]
        private static extern IntPtr SendMessage(IntPtr hWnd, uint Msg, IntPtr wParam, IntPtr lParam);

        [DllImport("user32.dll")]
        private static extern bool ShowWindow(IntPtr hWnd, int nCmdShow);

        [DllImport("user32.dll")]
        private static extern bool SetWindowPos(IntPtr hWnd, IntPtr hWndInsertAfter, int X, int Y, int cx, int cy, uint uFlags);

        private const uint FILE_MAP_READ = 0x0004;
        private const uint WM_SETTEXT = 0x000C;
        private const uint WM_COMMAND = 0x0111;
        private const uint EN_CHANGE = 0x0300;
        private const uint UDM_SETPOS32 = 0x0471;
        private const uint WM_KEYDOWN = 0x0100;
        private const uint WM_KEYUP = 0x0101;
        private const uint VK_RETURN = 0x0D;
        private const int SW_HIDE = 0;
        private const uint SWP_NOSIZE = 0x0001;
        private const uint SWP_NOMOVE = 0x0002;
        private const uint SWP_NOZORDER = 0x0004;
        private const uint SWP_HIDEWINDOW = 0x0080;

        [StructLayout(LayoutKind.Sequential, Pack = 1)]
        public struct SpeedFanSharedMem
        {
            public ushort Version;
            public ushort Flags;
            public int MemSize;
            public int Handle;
            public ushort NumTemps;
            public ushort NumFans;
            public ushort NumVolts;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] Temps;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] Fans;

            [MarshalAs(UnmanagedType.ByValArray, SizeConst = 32)]
            public int[] Volts;
        }

        static void Main(string[] args)
        {
            Console.OutputEncoding = Encoding.UTF8;
            Console.Title = "FanTest - ĐIỀU KHIỂN QUẠT 70% (LÕI ẨN 100% & TỰ ĐỢI CẢM BIẾN)";
            Console.ForegroundColor = ConsoleColor.Cyan;
            Console.WriteLine("================================================================================");
            Console.WriteLine(" FANTEST - ĐIỀU KHIỂN QUẠT NATIVE VỚI LÕI ẨN 100% (KHÔNG HIỆN CỬA SỔ)");
            Console.WriteLine(" Tự động chờ cảm biến quét xong trước khi phát lệnh ép 70% tốc độ");
            Console.WriteLine("================================================================================");
            Console.ResetColor();

            // 1. Cấu hình file config để SpeedFan khởi động ẩn tuyệt đối
            ConfigureSpeedFanSilentConfig();

            // 2. Khởi chạy SpeedFan ngầm và chạy luồng ẩn cửa sổ ngay lập tức
            StartSpeedFanCompletelyHidden();
            StartContinuousHider();

            // 3. Cơ chế chờ thông minh đến khi nạp xong cảm biến (Smart Ready Polling)
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.Write("\n[1/3] Đang chờ chip SuperIO quét và nạp xong cảm biến");
            Console.ResetColor();

            var (hMap, pView, readyData, elapsedMs) = WaitForSensorsReady(maxWaitSeconds: 20);

            if (hMap == IntPtr.Zero || pView == IntPtr.Zero || readyData.NumFans == 0)
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("\n[LỖI] Không thể đọc dữ liệu cảm biến sau thời gian chờ. Vui lòng thử lại!");
                Console.ResetColor();
                Console.WriteLine("\nNhấn Enter để đóng...");
                Console.ReadLine();
                return;
            }

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"\n\n[2/3] [THÀNH CÔNG] Đã nhận diện {readyData.NumFans} quạt sau {elapsedMs / 1000.0:F1}s!");
            for (int f = 0; f < readyData.NumFans; f++)
            {
                Console.WriteLine($"  -> Quạt #{f + 1}: {readyData.Fans[f],5} RPM (Tốc độ khởi điểm)");
            }
            Console.ResetColor();

            // 4. Gửi lệnh ép 70% PWM
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n================================================================================");
            Console.WriteLine(" >>> [3/3] BẮT ĐẦU GỬI LỆNH ÉP 70% TỐC ĐỘ TOÀN BỘ QUẠT TRONG 30 GIÂY <<<");
            Console.WriteLine("================================================================================");
            Console.ResetColor();

            SetPwmPercent(70);

            // 5. Bảng theo dõi số vòng quay RPM thời gian thực
            for (int sec = 1; sec <= 30; sec++)
            {
                SetPwmPercent(70);
                var live = Marshal.PtrToStructure<SpeedFanSharedMem>(pView);

                Console.ForegroundColor = ConsoleColor.White;
                Console.Write($"[{sec,2}s/30s]  ");

                for (int f = 0; f < live.NumFans; f++)
                {
                    int rpm = live.Fans[f];
                    Console.ForegroundColor = rpm > 1000 ? ConsoleColor.Green : ConsoleColor.Yellow;
                    Console.Write($"Quạt #{f + 1}: {rpm,5} RPM   ");
                }

                if (live.NumTemps >= 2)
                {
                    Console.ForegroundColor = ConsoleColor.DarkGray;
                    Console.Write($"| Temp: {live.Temps[1] / 100.0,4:F0}°C");
                }

                Console.WriteLine();
                Console.ResetColor();
                Thread.Sleep(1000);
            }

            // 6. Khôi phục về mức bình thường (20%)
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("\n--------------------------------------------------------------------------------");
            Console.WriteLine("Đang khôi phục quạt về chế độ mặc định (20%)...");
            SetPwmPercent(20);

            UnmapViewOfFile(pView);
            CloseHandle(hMap);

            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[HOÀN TẤT] Quá trình điều tốc hoàn tất xuất sắc!");
            Console.ResetColor();
            Console.WriteLine("\nNhấn Enter để đóng cửa sổ...");
            Console.ReadLine();
        }

        private static void ConfigureSpeedFanSilentConfig()
        {
            try
            {
                string cfgPath = @"C:\Program Files (x86)\SpeedFan\speedfanparams.cfg";
                if (File.Exists(cfgPath))
                {
                    string text = File.ReadAllText(cfgPath);
                    bool modified = false;

                    if (!text.Contains("StartupHide=true"))
                    {
                        text = text.Replace("StartupHide=false", "StartupHide=true");
                        modified = true;
                    }
                    if (!text.Contains("MinimizeOnClose=true"))
                    {
                        text = text.Replace("MinimizeOnClose=false", "MinimizeOnClose=true");
                        modified = true;
                    }
                    if (!text.Contains("ShowStaticIcon=false"))
                    {
                        text = text.Replace("ShowStaticIcon=true", "ShowStaticIcon=false");
                        modified = true;
                    }
                    if (modified)
                    {
                        File.WriteAllText(cfgPath, text);
                    }
                }
            }
            catch { }
        }

        private static void StartSpeedFanCompletelyHidden()
        {
            var procs = Process.GetProcessesByName("speedfan");
            if (procs.Length == 0)
            {
                string sfPath = @"C:\Program Files (x86)\SpeedFan\speedfan.exe";
                if (File.Exists(sfPath))
                {
                    try
                    {
                        var psi = new ProcessStartInfo
                        {
                            FileName = sfPath,
                            Arguments = "/NOSMARTSCAN /NOSMBSCAN /NOPCISCAN /MINIMIZED",
                            UseShellExecute = true,
                            WindowStyle = ProcessWindowStyle.Hidden,
                            CreateNoWindow = true
                        };
                        Process.Start(psi);
                    }
                    catch { }
                }
            }
            else
            {
                HideAllSpeedFanWindows();
            }
        }

        private static void StartContinuousHider()
        {
            var t = new Thread(() =>
            {
                for (int i = 0; i < 60; i++)
                {
                    HideAllSpeedFanWindows();
                    Thread.Sleep(100);
                }
            })
            {
                IsBackground = true
            };
            t.Start();
        }

        private static void HideAllSpeedFanWindows()
        {
            try
            {
                var procs = Process.GetProcessesByName("speedfan");
                if (procs.Length == 0) return;
                uint sfPid = (uint)procs[0].Id;

                EnumWindows((hWnd, l) =>
                {
                    GetWindowThreadProcessId(hWnd, out uint pid);
                    if (pid == sfPid)
                    {
                        // Đẩy cửa sổ ra ngoài màn hình và ẩn hoàn toàn
                        SetWindowPos(hWnd, IntPtr.Zero, -32000, -32000, 0, 0, SWP_NOZORDER | SWP_HIDEWINDOW);
                        ShowWindow(hWnd, SW_HIDE);
                    }
                    return true;
                }, IntPtr.Zero);
            }
            catch { }
        }

        private static (IntPtr hMap, IntPtr pView, SpeedFanSharedMem data, long elapsedMs) WaitForSensorsReady(int maxWaitSeconds)
        {
            var sw = Stopwatch.StartNew();
            IntPtr hMap = IntPtr.Zero;
            IntPtr pView = IntPtr.Zero;
            SpeedFanSharedMem data = default;

            while (sw.Elapsed.TotalSeconds < maxWaitSeconds)
            {
                if (hMap == IntPtr.Zero)
                {
                    hMap = OpenFileMapping(FILE_MAP_READ, false, "SFSharedMemory_ALM");
                    if (hMap == IntPtr.Zero) hMap = OpenFileMapping(FILE_MAP_READ, false, @"Global\SFSharedMemory_ALM");
                }

                if (hMap != IntPtr.Zero)
                {
                    if (pView == IntPtr.Zero)
                    {
                        pView = MapViewOfFile(hMap, FILE_MAP_READ, 0, 0, UIntPtr.Zero);
                    }

                    if (pView != IntPtr.Zero)
                    {
                        data = Marshal.PtrToStructure<SpeedFanSharedMem>(pView);

                        // Tiêu chí cảm biến nạp xong hoàn toàn:
                        // 1. NumFans > 0
                        // 2. Có ít nhất một quạt có số RPM thực tế (> 0)
                        bool hasLiveRpm = false;
                        for (int i = 0; i < data.NumFans; i++)
                        {
                            if (data.Fans[i] > 0)
                            {
                                hasLiveRpm = true;
                                break;
                            }
                        }

                        if (data.NumFans > 0 && hasLiveRpm)
                        {
                            return (hMap, pView, data, sw.ElapsedMilliseconds);
                        }
                    }
                }

                Console.Write(".");
                Thread.Sleep(350);
            }

            if (pView != IntPtr.Zero)
            {
                data = Marshal.PtrToStructure<SpeedFanSharedMem>(pView);
            }

            return (hMap, pView, data, sw.ElapsedMilliseconds);
        }

        private static void SetPwmPercent(int percent)
        {
            var procs = Process.GetProcessesByName("speedfan");
            if (procs.Length == 0) return;
            uint sfPid = (uint)procs[0].Id;

            EnumWindows((hWnd, l) =>
            {
                GetWindowThreadProcessId(hWnd, out uint pid);
                if (pid == sfPid)
                {
                    EnumChildWindows(hWnd, (child, param) =>
                    {
                        StringBuilder cCls = new StringBuilder(256);
                        GetClassName(child, cCls, 256);
                        string clsName = cCls.ToString();

                        if (clsName == "TEdit" || clsName.Contains("Edit"))
                        {
                            SendMessage(child, WM_SETTEXT, IntPtr.Zero, percent.ToString());
                            IntPtr wParam = (IntPtr)((EN_CHANGE << 16) | (child.ToInt32() & 0xFFFF));
                            SendMessage(hWnd, WM_COMMAND, wParam, child);
                            SendMessage(child, WM_KEYDOWN, (IntPtr)VK_RETURN, IntPtr.Zero);
                            SendMessage(child, WM_KEYUP, (IntPtr)VK_RETURN, IntPtr.Zero);
                        }
                        if (clsName == "TUpDown" || clsName.Contains("UpDown") || clsName.Contains("updown"))
                        {
                            SendMessage(child, UDM_SETPOS32, IntPtr.Zero, (IntPtr)percent);
                        }
                        return true;
                    }, IntPtr.Zero);
                }
                return true;
            }, IntPtr.Zero);
        }
    }
}
