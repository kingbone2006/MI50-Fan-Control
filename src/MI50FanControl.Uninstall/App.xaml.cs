using System;
using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace MI50FanControl.Uninstall
{
    public partial class App : WpfApplication
    {
        public static string TargetInstallDir { get; private set; } = string.Empty;

        protected override void OnStartup(StartupEventArgs e)
        {
            if (!IsRunningAsAdministrator())
            {
                try
                {
                    var psi = new ProcessStartInfo
                    {
                        FileName = Environment.ProcessPath ?? Process.GetCurrentProcess().MainModule?.FileName,
                        UseShellExecute = true,
                        Verb = "runas"
                    };
                    Process.Start(psi);
                }
                catch
                {
                    WpfMessageBox.Show(
                        "Gỡ cài đặt yêu cầu quyền Quản trị viên (Administrator) để xóa file hệ thống trong Program Files.\n\nVui lòng nhấp chuột phải vào Uninstall.exe và chọn 'Run as administrator'!",
                        "Yêu Cầu Quyền Administrator",
                        MessageBoxButton.OK,
                        MessageBoxImage.Warning);
                }
                Environment.Exit(0);
                return;
            }

            string currentExe = Process.GetCurrentProcess().MainModule?.FileName ?? AppDomain.CurrentDomain.BaseDirectory;
            string currentDir = Path.GetDirectoryName(currentExe) ?? AppDomain.CurrentDomain.BaseDirectory;

            int tempArgIndex = Array.IndexOf(e.Args, "--from-temp");
            if (tempArgIndex >= 0 && tempArgIndex + 1 < e.Args.Length)
            {
                TargetInstallDir = e.Args[tempArgIndex + 1];
                base.OnStartup(e);
                return;
            }

            // Copy to TEMP and run from TEMP to release file locks on Program Files\MI50FanControl
            string tempUninstallExe = Path.Combine(Path.GetTempPath(), "MI50FanControl_Uninstall.exe");
            try
            {
                File.Copy(currentExe, tempUninstallExe, true);
                var psi = new ProcessStartInfo
                {
                    FileName = tempUninstallExe,
                    Arguments = $"--from-temp \"{currentDir}\"",
                    UseShellExecute = true,
                    Verb = "runas"
                };
                Process.Start(psi);
                Environment.Exit(0);
                return;
            }
            catch
            {
                TargetInstallDir = currentDir;
                base.OnStartup(e);
            }
        }

        private static bool IsRunningAsAdministrator()
        {
            try
            {
                using var identity = WindowsIdentity.GetCurrent();
                var principal = new WindowsPrincipal(identity);
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch
            {
                return false;
            }
        }
    }
}
