using System;
using System.Diagnostics;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Microsoft.Win32;

namespace MI50FanControl.Installer
{
    public partial class MainWindow : Window
    {
        private string _targetInstallDir = string.Empty;
        private DispatcherTimer? _starCountdownTimer;
        private int _starSecondsLeft = 10;
        private bool _isPrerequisiteMet = true;
        private bool _isVietnamese = true;
        private bool _isInitialized = false;

        public MainWindow()
        {
            InitializeComponent();
            _isInitialized = true;

            // Check command line arguments for language
            var args = Environment.GetCommandLineArgs();
            if (args.Any(a => a.Equals("--lang=en", StringComparison.OrdinalIgnoreCase) || a.Equals("--lang en", StringComparison.OrdinalIgnoreCase) || a.Equals("-en", StringComparison.OrdinalIgnoreCase)))
            {
                _isVietnamese = false;
                LangEnRadio.IsChecked = true;
                LangViRadio.IsChecked = false;
            }
            else
            {
                _isVietnamese = true;
                LangViRadio.IsChecked = true;
                LangEnRadio.IsChecked = false;
            }

            // Safely load images from embedded resources
            LoadEmbeddedImages();

            string defaultPath = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                "MI50FanControl");

            InstallPathBox.Text = defaultPath;

            bool isSilent = args.Any(a => a.Equals("--silent", StringComparison.OrdinalIgnoreCase) || a.Equals("/silent", StringComparison.OrdinalIgnoreCase) || a.Equals("/s", StringComparison.OrdinalIgnoreCase) || a.Equals("-s", StringComparison.OrdinalIgnoreCase));
            if (isSilent)
            {
                Loaded += (s, e) =>
                {
                    try
                    {
                        Hide();
                        PerformInstallation(defaultPath, true, true);
                        string mainExe = Path.Combine(defaultPath, "MI50FanControl.exe");
                        if (File.Exists(mainExe))
                        {
                            Process.Start(new ProcessStartInfo
                            {
                                FileName = mainExe,
                                WorkingDirectory = defaultPath,
                                UseShellExecute = true,
                                Verb = "runas"
                            });
                        }
                    }
                    catch { }
                    Environment.Exit(0);
                };
            }

            // Initialize Language and Prerequisites check
            ApplyLanguage();
            CheckPrerequisites();
        }

        private void LoadEmbeddedImages()
        {
            try
            {
                var appIco = LoadBitmapFromResource("app.ico");
                if (appIco != null)
                {
                    Icon = appIco;
                    AppLogoImage.Source = appIco;
                }

                var qrBmp = LoadBitmapFromResource("donate_qr.jpg");
                if (qrBmp != null)
                {
                    DonateQrImage.Source = qrBmp;
                }

                var starBmp = LoadBitmapFromResource("star.png");
                if (starBmp != null)
                {
                    StarGuideImage.Source = starBmp;
                }
            }
            catch { }
        }

        private static BitmapImage? LoadBitmapFromResource(string resourceSuffix)
        {
            try
            {
                var asm = Assembly.GetExecutingAssembly();
                var name = asm.GetManifestResourceNames().FirstOrDefault(n => n.EndsWith(resourceSuffix, StringComparison.OrdinalIgnoreCase));
                if (name != null)
                {
                    using var stream = asm.GetManifestResourceStream(name);
                    if (stream != null)
                    {
                        var bmp = new BitmapImage();
                        bmp.BeginInit();
                        bmp.CacheOption = BitmapCacheOption.OnLoad;
                        bmp.StreamSource = stream;
                        bmp.EndInit();
                        bmp.Freeze();
                        return bmp;
                    }
                }
            }
            catch { }

            return null;
        }

        private void LangRadio_Checked(object sender, RoutedEventArgs e)
        {
            if (!_isInitialized || LangViRadio == null) return;
            _isVietnamese = (LangViRadio.IsChecked == true);
            ApplyLanguage();
            CheckPrerequisites();
        }

        private void ApplyLanguage()
        {
            if (!_isInitialized || HeaderTitleText == null) return;

            if (_isVietnamese)
            {
                Title = "Cài Đặt AMD MI50 / Radeon PRO VII Fan Controller";
                HeaderTitleText.Text = "Cài Đặt MI50 Fan Control";
                HeaderSubtitleText.Text = "Tác giả: Vũ Quốc Hải • AMD Radeon Instinct MI50 / Radeon PRO VII";

                InstallPathLabel.Text = "Thư mục cài đặt phần mềm:";
                BrowseBtn.Content = "Duyệt...";
                OptionsLabel.Text = "Tùy chọn bổ sung:";
                DesktopShortcutCheck.Content = "Tạo biểu tượng lối tắt ngoài màn hình (Desktop Shortcut)";
                StartWithWindowsCheck.Content = "Khởi động cùng Windows khi bật máy (Start with Windows)";
                StartWithWindowsHint.Text = "💡 Đã tự động tích chọn khởi động cùng Windows để tự động tối ưu quạt khi mở máy.";

                DownloadDotNetBtn.Content = "📥 Tải .NET 8 Ngay";
                RecheckBtn.Content = "🔄 Kiểm Tra Lại";
                CancelBtn.Content = "Hủy Bỏ";
                InstallBtn.Content = "Cài Đặt Ngay (Install)";

                ProgressStatusText.Text = "Đang giải nén và cài đặt các tập tin hệ thống...";
                ProgressSubText.Text = "Đang nạp trình điều khiển SuperIO & thiết lập tự khởi động...";

                VietQrContainer.Visibility = Visibility.Visible;
                KofiContainer.Visibility = Visibility.Collapsed;

                StarTitleText.Text = "⭐ Tặng 1 Sao (Star) Trên GitHub Ủng Hộ Dự Án";
                StarDescText.Text = "Không sao cả bạn nhé! Bạn chỉ cần bấm tặng 1 Sao (Star) cho dự án trên GitHub là đã giúp đỡ tác giả rất nhiều rồi! 🌟";

                ThankYouTitleText.Text = "🎉 Cảm Ơn Bạn Rất Nhiều!";
                ThankYouDescText.Text = "Sự đồng hành và ủng hộ của bạn là nguồn động lực to lớn giúp tác giả tiếp tục phát triển & tối ưu MI50 Fan Control ngày một hoàn thiện hơn! ❤️";
                LaunchAppCheck.Content = "Khởi chạy AMD MI50 Fan Control ngay bây giờ";
                FinishBtn.Content = "🚀 Hoàn Tất & Khởi Chạy (Finish)";
            }
            else
            {
                Title = "AMD MI50 / Radeon PRO VII Fan Controller Setup";
                HeaderTitleText.Text = "MI50 Fan Control Setup";
                HeaderSubtitleText.Text = "Author: Vu Quoc Hai • AMD Radeon Instinct MI50 / Radeon PRO VII";

                InstallPathLabel.Text = "Installation Directory:";
                BrowseBtn.Content = "Browse...";
                OptionsLabel.Text = "Additional Options:";
                DesktopShortcutCheck.Content = "Create a Desktop shortcut";
                StartWithWindowsCheck.Content = "Start automatically with Windows";
                StartWithWindowsHint.Text = "💡 Checked by default to automatically regulate fans on Windows startup.";

                DownloadDotNetBtn.Content = "📥 Download .NET 8";
                RecheckBtn.Content = "🔄 Re-check";
                CancelBtn.Content = "Cancel";
                InstallBtn.Content = "Install Now";

                ProgressStatusText.Text = "Extracting files and configuring system services...";
                ProgressSubText.Text = "Registering SuperIO kernel driver and autostart tasks...";

                VietQrContainer.Visibility = Visibility.Collapsed;
                KofiContainer.Visibility = Visibility.Visible;

                StarTitleText.Text = "⭐ Give a Star on GitHub to Support";
                StarDescText.Text = "No problem! Giving a Star to this project repository on GitHub is a great way to support and motivate the author! 🌟";

                ThankYouTitleText.Text = "🎉 Thank You Very Much!";
                ThankYouDescText.Text = "Your support is a huge motivation to keep improving and maintaining the MI50 Fan Control project! ❤️";
                LaunchAppCheck.Content = "Launch AMD MI50 Fan Control now";
                FinishBtn.Content = "🚀 Finish & Launch";
            }
        }

        private void CheckPrerequisites()
        {
            if (!_isInitialized || PrereqCard == null) return;

            bool hasDotNet8 = DetectDotNet8DesktopRuntime();

            if (hasDotNet8)
            {
                _isPrerequisiteMet = true;
                PrereqCard.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(48, 54, 61));
                PrereqIcon.Text = "✅";
                PrereqTitle.Text = _isVietnamese
                    ? "Môi Trường .NET 8.0 Desktop Runtime: Đã Sẵn Sàng"
                    : ".NET 8.0 Desktop Runtime Environment: Ready";
                PrereqTitle.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
                PrereqSubtext.Text = _isVietnamese
                    ? "Hệ thống đã có đầy đủ thư viện cần thiết để vận hành ứng dụng."
                    : "All necessary system runtimes are installed and ready.";
                DownloadDotNetBtn.Visibility = Visibility.Collapsed;
                RecheckBtn.Visibility = Visibility.Collapsed;
                InstallBtn.IsEnabled = true;
                InstallBtn.Opacity = 1.0;
                InstallBtn.ToolTip = null;
            }
            else
            {
                _isPrerequisiteMet = false;
                PrereqCard.BorderBrush = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                PrereqIcon.Text = "⚠️";
                PrereqTitle.Text = _isVietnamese
                    ? "Chưa Cài Đặt .NET 8.0 Desktop Runtime (x64)!"
                    : ".NET 8.0 Desktop Runtime (x64) Not Found!";
                PrereqTitle.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(239, 68, 68));
                PrereqSubtext.Text = _isVietnamese
                    ? "Ứng dụng yêu cầu .NET 8 Desktop Runtime. Vui lòng bấm Tải Ngay và cài đặt để tiếp tục."
                    : "This application requires .NET 8 Desktop Runtime. Please download and install it to continue.";
                DownloadDotNetBtn.Visibility = Visibility.Visible;
                RecheckBtn.Visibility = Visibility.Visible;
                InstallBtn.IsEnabled = false;
                InstallBtn.Opacity = 0.5;
                InstallBtn.ToolTip = _isVietnamese
                    ? "Vui lòng cài đặt .NET 8 Desktop Runtime trước khi tiếp tục."
                    : "Please install .NET 8 Desktop Runtime before continuing.";
            }
        }

        private bool DetectDotNet8DesktopRuntime()
        {
            try
            {
                // 1. Check directory C:\Program Files\dotnet\shared\Microsoft.WindowsDesktop.App
                string desktopPath = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "dotnet", "shared", "Microsoft.WindowsDesktop.App");

                if (Directory.Exists(desktopPath))
                {
                    var versions = Directory.GetDirectories(desktopPath);
                    if (versions.Any(v => Path.GetFileName(v).StartsWith("8.")))
                    {
                        return true;
                    }
                }

                // 2. Check via dotnet CLI
                var psi = new ProcessStartInfo("dotnet", "--list-runtimes")
                {
                    RedirectStandardOutput = true,
                    UseShellExecute = false,
                    CreateNoWindow = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    string output = proc.StandardOutput.ReadToEnd();
                    proc.WaitForExit(1500);
                    if (output.Contains("Microsoft.WindowsDesktop.App 8."))
                    {
                        return true;
                    }
                }
            }
            catch { }

            return false;
        }

        private void DownloadDotNetBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe")
                {
                    UseShellExecute = true
                });
            }
            catch
            {
                try
                {
                    Process.Start(new ProcessStartInfo("https://dotnet.microsoft.com/download/dotnet/8.0")
                    {
                        UseShellExecute = true
                    });
                }
                catch { }
            }
        }

        private void RecheckBtn_Click(object sender, RoutedEventArgs e)
        {
            CheckPrerequisites();
            if (_isPrerequisiteMet)
            {
                string msg = _isVietnamese
                    ? "Thư viện .NET 8.0 Desktop Runtime đã được cài đặt thành công! Bạn có thể tiếp tục cài đặt."
                    : ".NET 8.0 Desktop Runtime has been successfully detected! You can now proceed with the installation.";
                string title = _isVietnamese ? "Kiểm Tra Thành Công" : "Check Successful";
                System.Windows.MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Information);
            }
            else
            {
                string msg = _isVietnamese
                    ? "Vẫn chưa phát hiện .NET 8.0 Desktop Runtime trên máy. Bạn vui lòng hoàn tất quá trình cài đặt .NET 8 rồi bấm Kiểm Tra Lại nhé!"
                    : ".NET 8.0 Desktop Runtime was not detected. Please complete the .NET 8 installation and click Re-check!";
                string title = _isVietnamese ? "Chưa Tìm Thấy Thư Viện" : "Runtime Not Found";
                System.Windows.MessageBox.Show(msg, title, MessageBoxButton.OK, MessageBoxImage.Warning);
            }
        }

        private void BrowseBtn_Click(object sender, RoutedEventArgs e)
        {
            using var dialog = new System.Windows.Forms.FolderBrowserDialog();
            dialog.Description = _isVietnamese ? "Chọn thư mục cài đặt MI50 Fan Control" : "Select MI50 Fan Control Installation Folder";
            dialog.SelectedPath = InstallPathBox.Text;
            if (dialog.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                InstallPathBox.Text = dialog.SelectedPath;
            }
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = System.Security.Principal.WindowsIdentity.GetCurrent();
                var principal = new System.Security.Principal.WindowsPrincipal(identity);
                return principal.IsInRole(System.Security.Principal.WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }

        private async void InstallBtn_Click(object sender, RoutedEventArgs e)
        {
            if (!_isPrerequisiteMet)
            {
                string msg = _isVietnamese
                    ? "Vui lòng cài đặt .NET 8.0 Desktop Runtime (x64) trước khi tiếp tục."
                    : "Please install .NET 8.0 Desktop Runtime (x64) before continuing.";
                System.Windows.MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            if (!IsRunningAsAdministrator())
            {
                try
                {
                    string langArg = _isVietnamese ? "--lang=vi" : "--lang=en";
                    var psi = new ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName ?? "MI50FanControl_Setup.exe",
                        Arguments = langArg,
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                    Close();
                    return;
                }
                catch
                {
                    string msg = _isVietnamese
                        ? "Bộ cài đặt cần quyền Quản trị viên (Administrator) để tiếp tục.\nVui lòng nhấp chuột phải vào file cài đặt và chọn 'Run as administrator'!"
                        : "Administrator privileges are required to install.\nPlease right-click the setup file and select 'Run as administrator'!";
                    System.Windows.MessageBox.Show(msg, "Admin Required", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
            }

            _targetInstallDir = InstallPathBox.Text.Trim();
            if (string.IsNullOrEmpty(_targetInstallDir))
            {
                string msg = _isVietnamese ? "Vui lòng chọn thư mục cài đặt hợp lệ." : "Please select a valid installation path.";
                System.Windows.MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return;
            }

            Step1Panel.Visibility = Visibility.Collapsed;
            Step2Panel.Visibility = Visibility.Visible;
            CancelBtn.IsEnabled = false;
            InstallBtn.Visibility = Visibility.Collapsed;

            bool desktopShortcut = DesktopShortcutCheck.IsChecked == true;
            bool startWithWin = StartWithWindowsCheck.IsChecked == true;

            try
            {
                await Task.Run(() => PerformInstallation(_targetInstallDir, desktopShortcut, startWithWin));

                // Step 2 finished -> Show Donate Panel (Step 3)
                Step2Panel.Visibility = Visibility.Collapsed;
                DonatePanel.Visibility = Visibility.Visible;
            }
            catch (Exception ex)
            {
                Step2Panel.Visibility = Visibility.Collapsed;
                Step1Panel.Visibility = Visibility.Visible;
                CancelBtn.IsEnabled = true;
                InstallBtn.Visibility = Visibility.Visible;

                string msg = _isVietnamese ? $"Cài đặt thất bại:\n{ex.Message}" : $"Installation failed:\n{ex.Message}";
                System.Windows.MessageBox.Show(msg, "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void OpenKofiBtn_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://ko-fi.com/kingbone2006")
                {
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void DonatedBtn_Click(object sender, RoutedEventArgs e)
        {
            ShowThankYouScreen();
        }

        private void NoMoneyBtn_Click(object sender, RoutedEventArgs e)
        {
            // Open GitHub repo automatically
            OpenGitHubRepo();

            // Transition to Star Panel (Step 4)
            DonatePanel.Visibility = Visibility.Collapsed;
            StarPanel.Visibility = Visibility.Visible;

            // Start 10-second countdown
            StartStarCountdown();
        }

        private void OpenGitHubRepo()
        {
            try
            {
                Process.Start(new ProcessStartInfo("https://github.com/kingbone2006/MI50-Fan-Control")
                {
                    UseShellExecute = true
                });
            }
            catch { }
        }

        private void StartStarCountdown()
        {
            _starSecondsLeft = 10;
            StarContinueBtn.IsEnabled = false;
            StarContinueBtn.Content = _isVietnamese
                ? $"⏳ Vui lòng chờ ({_starSecondsLeft}s)..."
                : $"⏳ Please wait ({_starSecondsLeft}s)...";

            _starCountdownTimer = new DispatcherTimer
            {
                Interval = TimeSpan.FromSeconds(1)
            };
            _starCountdownTimer.Tick += (s, e) =>
            {
                _starSecondsLeft--;
                if (_starSecondsLeft > 0)
                {
                    StarContinueBtn.Content = _isVietnamese
                        ? $"⏳ Vui lòng chờ ({_starSecondsLeft}s)..."
                        : $"⏳ Please wait ({_starSecondsLeft}s)...";
                }
                else
                {
                    _starCountdownTimer.Stop();
                    StarContinueBtn.IsEnabled = true;
                    StarContinueBtn.Content = _isVietnamese
                        ? "⭐ Tôi Đã Thả Sao / Tiếp Tục"
                        : "⭐ I Have Starred / Continue";
                    StarContinueBtn.Background = new SolidColorBrush(System.Windows.Media.Color.FromRgb(16, 185, 129));
                    StarContinueBtn.Foreground = new SolidColorBrush(System.Windows.Media.Color.FromRgb(255, 255, 255));
                }
            };
            _starCountdownTimer.Start();
        }

        private void StarContinueBtn_Click(object sender, RoutedEventArgs e)
        {
            _starCountdownTimer?.Stop();
            ShowThankYouScreen();
        }

        private void ShowThankYouScreen()
        {
            DonatePanel.Visibility = Visibility.Collapsed;
            StarPanel.Visibility = Visibility.Collapsed;
            ThankYouPanel.Visibility = Visibility.Visible;
            CancelBtn.Visibility = Visibility.Collapsed;
            FinishBtn.Visibility = Visibility.Visible;
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                Process.Start(new ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch { }
            e.Handled = true;
        }

        private void PerformInstallation(string installDir, bool createDesktopShortcut, bool startWithWindows)
        {
            // 1. Kill any old running instances before extracting
            KillProcess("MI50FanControl");
            KillProcess("speedfan");
            KillProcess("Uninstall");
            System.Threading.Thread.Sleep(300);

            if (!Directory.Exists(installDir))
            {
                Directory.CreateDirectory(installDir);
            }

            // 2. Extract payload
            var assembly = Assembly.GetExecutingAssembly();
            Stream? resourceStream = null;
            foreach (var resName in assembly.GetManifestResourceNames())
            {
                if (resName.EndsWith("app_payload.zip", StringComparison.OrdinalIgnoreCase))
                {
                    resourceStream = assembly.GetManifestResourceStream(resName);
                    break;
                }
            }
            if (resourceStream == null)
            {
                resourceStream = assembly.GetManifestResourceStream("MI50FanControl.Installer.app_payload.zip");
            }

            if (resourceStream != null)
            {
                using (resourceStream)
                using (var archive = new ZipArchive(resourceStream, ZipArchiveMode.Read))
                {
                    archive.ExtractToDirectory(installDir, true);
                }
            }
            else
            {
                string devPublishDir = Path.GetFullPath(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, @"..\..\..\..\publish_new"));
                if (Directory.Exists(devPublishDir))
                {
                    CopyDirectory(devPublishDir, installDir);
                }
                else
                {
                    string localPublish = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "publish_new");
                    if (Directory.Exists(localPublish))
                    {
                        CopyDirectory(localPublish, installDir);
                    }
                }
            }

            // 3. Ensure Kernel Driver is silently registered and active
            try
            {
                string sysWOW64 = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows), "SysWOW64", "speedfan.sys");
                string bundledSys = Path.Combine(installDir, "Engine", "speedfan.sys");

                if (File.Exists(bundledSys))
                {
                    try { File.Copy(bundledSys, sysWOW64, true); } catch { }
                }

                string targetSys = File.Exists(sysWOW64) ? sysWOW64 : bundledSys;

                var psiCreate = new ProcessStartInfo("sc.exe", $"create speedfan type= kernel start= auto binPath= \"\\??\\{targetSys}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psiCreate)?.WaitForExit(1500);

                var psiStart = new ProcessStartInfo("sc.exe", "start speedfan")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false
                };
                Process.Start(psiStart)?.WaitForExit(1500);
            }
            catch { }

            string mainExePath = Path.Combine(installDir, "MI50FanControl.exe");
            string uninstallExePath = Path.Combine(installDir, "Uninstall.exe");
            string iconPath = Path.Combine(installDir, "Assets", "app.ico");




            // 4. Create Desktop Shortcut
            if (createDesktopShortcut && File.Exists(mainExePath))
            {
                string desktopShortcut = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),
                    "MI50 Fan Control.lnk");
                CreateShortcut(desktopShortcut, mainExePath, installDir, "AMD MI50 / Radeon PRO VII Fan Controller", iconPath);
            }

            // 5. Create Start Menu Shortcuts
            string startMenuDir = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.Programs),
                "MI50FanControl");
            if (!Directory.Exists(startMenuDir))
            {
                Directory.CreateDirectory(startMenuDir);
            }

            if (File.Exists(mainExePath))
            {
                string startMenuApp = Path.Combine(startMenuDir, "MI50 Fan Control.lnk");
                CreateShortcut(startMenuApp, mainExePath, installDir, "AMD MI50 / Radeon PRO VII Fan Controller", iconPath);
            }

            if (File.Exists(uninstallExePath))
            {
                string startMenuUninst = Path.Combine(startMenuDir, "Uninstall MI50 Fan Control.lnk");
                CreateShortcut(startMenuUninst, uninstallExePath, installDir, "Gỡ cài đặt MI50 Fan Control", uninstallExePath);
            }

            // 6. Register in Windows Add/Remove Programs
            try
            {
                using var key = Registry.CurrentUser.CreateSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall\MI50FanControl");
                if (key != null)
                {
                    key.SetValue("DisplayName", "AMD MI50 / Radeon PRO VII Fan Controller");
                    key.SetValue("DisplayVersion", "3.0.0");
                    key.SetValue("Publisher", "Vũ Quốc Hải");
                    key.SetValue("InstallLocation", installDir);
                    key.SetValue("UninstallString", $"\"{uninstallExePath}\"");
                    key.SetValue("DisplayIcon", iconPath);
                    key.SetValue("NoModify", 1, RegistryValueKind.DWord);
                    key.SetValue("NoRepair", 1, RegistryValueKind.DWord);
                }
            }
            catch { }


            // 7. Start With Windows (Elevated Scheduled Task + Registry)
            if (startWithWindows && File.Exists(mainExePath))
            {
                try
                {
                    // Create elevated Scheduled Task
                    var psi = new ProcessStartInfo("schtasks", $"/Create /TN \"MI50FanControl\" /TR \"\\\"{mainExePath}\\\" --minimized\" /SC ONLOGON /RL HIGHEST /F")
                    {
                        CreateNoWindow = true,
                        UseShellExecute = false
                    };
                    Process.Start(psi)?.WaitForExit(3000);

                    // Also HKCU Run key
                    using var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true);
                    runKey?.SetValue("MI50FanControl", $"\"{mainExePath}\" --minimized");
                }
                catch { }
            }

            // 8. Save configured language & startup configuration in user settings
            try
            {
                string settingsFolder = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MI50FanControl");
                if (!Directory.Exists(settingsFolder))
                {
                    Directory.CreateDirectory(settingsFolder);
                }
                string appSettingsFile = Path.Combine(settingsFolder, "appsettings.json");
                string langCode = _isVietnamese ? "vi" : "en";
                string json = $"{{\r\n  \"Language\": \"{langCode}\",\r\n  \"StartWithWindows\": {startWithWindows.ToString().ToLower()},\r\n  \"MinimizeToTrayOnClose\": true,\r\n  \"MinimizeToTrayOnMinimize\": true\r\n}}";
                File.WriteAllText(appSettingsFile, json);
            }
            catch { }
        }

        private static void CreateShortcut(string shortcutPath, string targetPath, string workingDir, string description, string iconPath)
        {
            try
            {
                Type? shellType = Type.GetTypeFromProgID("WScript.Shell");
                if (shellType != null)
                {
                    dynamic shell = Activator.CreateInstance(shellType)!;
                    dynamic shortcut = shell.CreateShortcut(shortcutPath);
                    shortcut.TargetPath = targetPath;
                    shortcut.WorkingDirectory = workingDir;
                    shortcut.Description = description;
                    if (File.Exists(iconPath))
                    {
                        shortcut.IconLocation = iconPath;
                    }
                    shortcut.Save();
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[Installer] CreateShortcut error: {ex.Message}");
            }
        }

        private static void CopyDirectory(string sourceDir, string targetDir)
        {
            foreach (string dirPath in Directory.GetDirectories(sourceDir, "*", SearchOption.AllDirectories))
            {
                Directory.CreateDirectory(dirPath.Replace(sourceDir, targetDir));
            }
            foreach (string newPath in Directory.GetFiles(sourceDir, "*.*", SearchOption.AllDirectories))
            {
                File.Copy(newPath, newPath.Replace(sourceDir, targetDir), true);
            }
        }

        private static void KillProcess(string procName)
        {
            try
            {
                foreach (var proc in Process.GetProcessesByName(procName))
                {
                    try
                    {
                        proc.Kill();
                        proc.WaitForExit(1500);
                    }
                    catch { }
                }
            }
            catch { }
        }

        private void FinishBtn_Click(object sender, RoutedEventArgs e)
        {
            if (LaunchAppCheck.IsChecked == true)
            {
                string mainExe = Path.Combine(_targetInstallDir, "MI50FanControl.exe");
                if (File.Exists(mainExe))
                {
                    try
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = mainExe,
                            WorkingDirectory = _targetInstallDir,
                            UseShellExecute = true,
                            Verb = "runas"
                        });
                    }
                    catch { }
                }
            }
            Close();
        }
    }
}