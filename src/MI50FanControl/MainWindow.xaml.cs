using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Windows;
using System.Windows.Forms;
using MI50FanControl.ViewModels;

namespace MI50FanControl
{
    public partial class MainWindow : Window
    {
        private readonly MainViewModel _vm;
        private NotifyIcon? _notifyIcon;
        private bool _isExplicitExit = false;

        public MainWindow()
        {
            InitializeComponent();
            _vm = new MainViewModel();
            DataContext = _vm;

            InitializeSystemTray();
        }

        private void InitializeSystemTray()
        {
            try
            {
                _notifyIcon = new NotifyIcon();

                // Load icon reliably from process module, disk, or resource
                System.Drawing.Icon? appIcon = null;
                try
                {
                    string exePath = System.Diagnostics.Process.GetCurrentProcess().MainModule?.FileName ?? "";
                    if (!string.IsNullOrEmpty(exePath) && File.Exists(exePath))
                    {
                        appIcon = System.Drawing.Icon.ExtractAssociatedIcon(exePath);
                    }
                }
                catch { }

                if (appIcon == null)
                {
                    string iconPath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "Assets", "app.ico");
                    if (File.Exists(iconPath))
                    {
                        try { appIcon = new System.Drawing.Icon(iconPath); } catch { }
                    }
                }

                _notifyIcon.Icon = appIcon ?? SystemIcons.Application;
                _notifyIcon.Text = "AMD MI50 / Radeon PRO VII Fan Controller";
                _notifyIcon.Visible = true;

                // Context Menu
                var contextMenu = new ContextMenuStrip();
                BuildTrayMenu(contextMenu);

                contextMenu.Opening += (s, e) =>
                {
                    BuildTrayMenu(contextMenu);
                };

                _notifyIcon.ContextMenuStrip = contextMenu;
                _notifyIcon.DoubleClick += (s, e) => RestoreWindow();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[MainWindow] Tray init error: {ex.Message}");
            }
        }

        private void BuildTrayMenu(ContextMenuStrip menu)
        {
            menu.Items.Clear();

            var showItem = new ToolStripMenuItem("📊 Mở Giao Diện (Dashboard)", null, (s, e) => RestoreWindow());
            showItem.Font = new Font(showItem.Font, System.Drawing.FontStyle.Bold);
            menu.Items.Add(showItem);

            menu.Items.Add(new ToolStripSeparator());

            // 4 Fan Profiles / Modes
            string activeId = _vm.SettingsService.Current.ActiveCurveProfileId;
            var profiles = _vm.SettingsService.Current.CurveProfiles;

            foreach (var prof in profiles)
            {
                string iconPrefix = prof.Id switch
                {
                    "silent" => "🌿 ",
                    "balanced" => "⚖️ ",
                    "performance" => "🚀 ",
                    "aggressive" => "❄️ ",
                    _ => "📈 "
                };

                var item = new ToolStripMenuItem($"{iconPrefix}{prof.Name}", null, (s, e) =>
                {
                    _vm.SettingsService.Current.ActiveCurveProfileId = prof.Id;
                    _vm.SettingsService.Current.GlobalManualOverride = false;
                    _vm.SettingsService.Save();
                    _vm.DashboardVM.RefreshProfilesList();
                })
                {
                    Checked = (prof.Id == activeId),
                    CheckOnClick = false
                };

                menu.Items.Add(item);
            }

            menu.Items.Add(new ToolStripSeparator());
            var exitItem = new ToolStripMenuItem("❌ Thoát / Exit", null, (s, e) => ExitApplication());
            menu.Items.Add(exitItem);
        }

        private void RestoreWindow()
        {
            Show();
            WindowState = WindowState.Normal;
            Activate();
        }

        private void Hyperlink_RequestNavigate(object sender, System.Windows.Navigation.RequestNavigateEventArgs e)
        {
            try
            {
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(e.Uri.AbsoluteUri) { UseShellExecute = true });
            }
            catch { }
            e.Handled = true;
        }

        private void ExitApplication()
        {
            _isExplicitExit = true;
            _vm.Shutdown();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }
            System.Windows.Application.Current.Shutdown();
        }

        protected override void OnStateChanged(EventArgs e)
        {
            base.OnStateChanged(e);
            if (WindowState == WindowState.Minimized && _vm.SettingsVM.MinimizeToTrayMin)
            {
                Hide();
            }
        }

        protected override void OnClosing(CancelEventArgs e)
        {
            if (!_isExplicitExit && _vm.SettingsVM.MinimizeToTrayClose)
            {
                e.Cancel = true;
                Hide();
                return;
            }

            _vm.Shutdown();
            if (_notifyIcon != null)
            {
                _notifyIcon.Visible = false;
                _notifyIcon.Dispose();
                _notifyIcon = null;
            }

            base.OnClosing(e);
        }
    }
}