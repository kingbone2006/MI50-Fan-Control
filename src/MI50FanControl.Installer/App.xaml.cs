using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using WpfApplication = System.Windows.Application;
using WpfMessageBox = System.Windows.MessageBox;

namespace MI50FanControl.Installer
{
    public partial class App : WpfApplication
    {
        protected override void OnStartup(StartupEventArgs e)
        {
            AppDomain.CurrentDomain.UnhandledException += (s, ev) =>
            {
                try
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "installer_crash.log");
                    File.WriteAllText(path, ev.ExceptionObject.ToString());
                }
                catch { }
            };

            DispatcherUnhandledException += (s, ev) =>
            {
                try
                {
                    string path = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory), "installer_crash.log");
                    File.WriteAllText(path, ev.Exception.ToString());
                }
                catch { }
                ev.Handled = true;
            };

            base.OnStartup(e);

            try
            {
                var mainWindow = new MainWindow();
                mainWindow.Show();
            }
            catch (Exception ex)
            {
                WpfMessageBox.Show($"Lỗi khởi động bộ cài:\n{ex.Message}\n\n{ex.StackTrace}", "Lỗi Khởi Động", MessageBoxButton.OK, MessageBoxImage.Error);
                Environment.Exit(1);
            }
        }
    }
}
