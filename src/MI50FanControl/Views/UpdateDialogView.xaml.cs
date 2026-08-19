using System;
using System.Diagnostics;
using System.IO;
using System.Windows;
using MI50FanControl.Services;

namespace MI50FanControl.Views
{
    public partial class UpdateDialogView : Window
    {
        private readonly UpdateInfo _updateInfo;
        private readonly SettingsService _settingsService;
        private readonly UpdateService _updateService;

        public UpdateDialogView(UpdateInfo updateInfo, SettingsService settingsService, UpdateService updateService)
        {
            InitializeComponent();
            _updateInfo = updateInfo;
            _settingsService = settingsService;
            _updateService = updateService;

            NewVersionText.Text = string.IsNullOrEmpty(_updateInfo.LatestVersion) ? _updateInfo.Title : _updateInfo.LatestVersion;
            ChangelogBox.Text = string.IsNullOrWhiteSpace(_updateInfo.Changelog) ? "Không có mô tả chi tiết cho bản phát hành này." : _updateInfo.Changelog;
            DisableAutoCheck.IsChecked = !_settingsService.Current.AutoCheckUpdates;
        }

        private void IgnoreBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();
            Close();
        }

        private async void UpdateNowBtn_Click(object sender, RoutedEventArgs e)
        {
            SaveSettings();

            if (!string.IsNullOrEmpty(_updateInfo.DownloadUrl) && _updateInfo.DownloadUrl.EndsWith(".exe", StringComparison.OrdinalIgnoreCase))
            {
                try
                {
                    DownloadProgressPanel.Visibility = Visibility.Visible;
                    UpdateNowBtn.IsEnabled = false;
                    IgnoreBtn.IsEnabled = false;

                    var progress = new Progress<int>(percent =>
                    {
                        DownloadProgressBar.Value = percent;
                        DownloadPercentText.Text = $"{percent}%";
                    });

                    string? installerPath = await _updateService.DownloadInstallerAsync(_updateInfo.DownloadUrl, progress);

                    if (!string.IsNullOrEmpty(installerPath) && File.Exists(installerPath))
                    {
                        Process.Start(new ProcessStartInfo
                        {
                            FileName = installerPath,
                            UseShellExecute = true,
                            Verb = "runas"
                        });

                        System.Windows.Application.Current.Shutdown();
                        return;
                    }
                }
                catch (Exception ex)
                {
                    LogService.Instance.Error("UpdateDialog", $"Lỗi tự động tải cập nhật: {ex.Message}");
                }
            }

            // Fallback: Open browser to release page
            try
            {
                string targetUrl = string.IsNullOrEmpty(_updateInfo.ReleaseUrl) ? UpdateService.GitHubRepoUrl : _updateInfo.ReleaseUrl;
                Process.Start(new ProcessStartInfo
                {
                    FileName = targetUrl,
                    UseShellExecute = true
                });
            }
            catch { }

            Close();
        }

        private void SaveSettings()
        {
            if (DisableAutoCheck.IsChecked == true)
            {
                _settingsService.Current.AutoCheckUpdates = false;
                _settingsService.Save();
            }
            else if (_settingsService.Current.AutoCheckUpdates == false && DisableAutoCheck.IsChecked == false)
            {
                _settingsService.Current.AutoCheckUpdates = true;
                _settingsService.Save();
            }
        }
    }
}
