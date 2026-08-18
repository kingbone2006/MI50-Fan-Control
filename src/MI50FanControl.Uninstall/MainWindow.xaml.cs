using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows;
using Microsoft.Win32;

namespace MI50FanControl.Uninstall
{
    public partial class MainWindow : Window
    {
        public MainWindow()
        {
            InitializeComponent();
        }

        private void CancelBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }

        private async void UninstallBtn_Click(object sender, RoutedEventArgs e)
        {
            CancelBtn.IsEnabled = false;
            UninstallBtn.Visibility = Visibility.Collapsed;
            UninstallProgressBar.Visibility = Visibility.Visible;

            StatusTitleText.Text = "Đang gỡ cài đặt MI50 Fan Control...";
            StatusDescText.Text = "Đang dừng tiến trình, dọn dẹp registry, xóa sạch toàn bộ file và thư mục...";

            await Task.Run(() => PerformUninstall());

            UninstallProgressBar.Visibility = Visibility.Collapsed;
            StatusTitleText.Text = "Gỡ cài đặt hoàn tất 100%!";
            StatusDescText.Text = "Toàn bộ phần mềm, thư mục cài đặt, dữ liệu cấu hình, phím tắt và driver đã được xóa sạch sẽ khỏi máy tính.";

            CloseBtn.Visibility = Visibility.Visible;
        }

        private void PerformUninstall()
        {
            string installDir = App.TargetInstallDir;
            if (string.IsNullOrEmpty(installDir) || !Directory.Exists(installDir))
            {
                installDir = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                    "MI50FanControl");
            }

            // 1. Dừng triệt để tất cả tiến trình liên quan
            KillProcess("MI50FanControl");
            KillProcess("speedfan");
            KillProcess("instspeedfan");
            Thread.Sleep(500);

            // 2. Dừng và gỡ bỏ driver service speedfan nếu có
            try
            {
                var psiStop = new ProcessStartInfo("sc.exe", "stop speedfan") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psiStop)?.WaitForExit(1500);

                var psiDel = new ProcessStartInfo("sc.exe", "delete speedfan") { CreateNoWindow = true, UseShellExecute = false };
                Process.Start(psiDel)?.WaitForExit(1500);
            }
            catch { }

            // 3. Xóa sạch phím tắt Desktop và Start Menu
            try
            {
                string userDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "MI50 Fan Control.lnk");
                if (File.Exists(userDesktop)) File.Delete(userDesktop);

                string publicDesktop = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonDesktopDirectory), "MI50 Fan Control.lnk");
                if (File.Exists(publicDesktop)) File.Delete(publicDesktop);

                string userStartMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Programs), "MI50FanControl");
                if (Directory.Exists(userStartMenu)) Directory.Delete(userStartMenu, true);

                string commonStartMenu = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms), "MI50FanControl");
                if (Directory.Exists(commonStartMenu)) Directory.Delete(commonStartMenu, true);
            }
            catch { }

            // 4. Xóa sạch các mục Registry
            try
            {
                using (var runKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    runKey?.DeleteValue("MI50FanControl", false);
                }
                using (var runKeyLM = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Run", true))
                {
                    runKeyLM?.DeleteValue("MI50FanControl", false);
                }

                using (var unKey = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    unKey?.DeleteSubKeyTree("MI50FanControl", false);
                }
                using (var unKeyL = Registry.LocalMachine.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Uninstall", true))
                {
                    unKeyL?.DeleteSubKeyTree("MI50FanControl", false);
                }

                Registry.CurrentUser.DeleteSubKeyTree(@"Software\MI50FanControl", false);
            }
            catch { }

            // 5. Xóa sạch thư mục cấu hình AppData của người dùng
            try
            {
                string localAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "MI50FanControl");
                if (Directory.Exists(localAppData)) Directory.Delete(localAppData, true);

                string roamingAppData = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "MI50FanControl");
                if (Directory.Exists(roamingAppData)) Directory.Delete(roamingAppData, true);
            }
            catch { }

            // 6. Xóa sạch 100% toàn bộ thư mục cài đặt phần mềm
            try
            {
                if (Directory.Exists(installDir))
                {
                    // Lớp 1: Xóa đệ quy .NET
                    DeleteDirectoryRecursively(installDir);

                    // Lớp 2: Xóa bằng lệnh cmd rd /s /q
                    if (Directory.Exists(installDir))
                    {
                        var cmdPsi = new ProcessStartInfo("cmd.exe", $"/c rd /s /q \"{installDir}\"")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(cmdPsi)?.WaitForExit(2000);
                    }

                    // Lớp 3: Xóa bằng PowerShell
                    if (Directory.Exists(installDir))
                    {
                        var psPsi = new ProcessStartInfo("powershell.exe", $"-NoProfile -Command \"Remove-Item -Path '{installDir}' -Recurse -Force -ErrorAction SilentlyContinue\"")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(psPsi)?.WaitForExit(2000);
                    }
                }
            }
            catch { }
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

        private static void DeleteDirectoryRecursively(string targetDir)
        {
            if (!Directory.Exists(targetDir)) return;

            string[] files = Directory.GetFiles(targetDir);
            string[] dirs = Directory.GetDirectories(targetDir);

            foreach (string file in files)
            {
                try
                {
                    File.SetAttributes(file, FileAttributes.Normal);
                    File.Delete(file);
                }
                catch { }
            }

            foreach (string dir in dirs)
            {
                DeleteDirectoryRecursively(dir);
            }

            try
            {
                Directory.Delete(targetDir, true);
            }
            catch { }
        }

        private void CloseBtn_Click(object sender, RoutedEventArgs e)
        {
            Close();
        }
    }
}