using System;
using System.Diagnostics;
using System.IO;
using Microsoft.Win32;

namespace MI50FanControl.Services
{
    public class AutoStartService
    {
        private const string RunKeyPath = @"Software\Microsoft\Windows\CurrentVersion\Run";
        private const string AppName = "MI50FanControl";

        public static bool IsAutoStartEnabled()
        {
            try
            {
                using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, false);
                if (key?.GetValue(AppName) != null) return true;

                var psi = new ProcessStartInfo("schtasks", $"/Query /TN \"{AppName}\"")
                {
                    CreateNoWindow = true,
                    UseShellExecute = false,
                    RedirectStandardOutput = true
                };
                using var proc = Process.Start(psi);
                if (proc != null)
                {
                    proc.WaitForExit(1000);
                    if (proc.ExitCode == 0) return true;
                }
            }
            catch
            {
            }
            return false;
        }

        public static void SetAutoStart(bool enable)
        {
            try
            {
                string exePath = Process.GetCurrentProcess().MainModule?.FileName ??
                                 Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "MI50FanControl.exe");

                if (enable)
                {
                    // 1. Create elevated Scheduled Task for silent logon without UAC prompts
                    try
                    {
                        var psi = new ProcessStartInfo("schtasks", $"/Create /TN \"{AppName}\" /TR \"\\\"{exePath}\\\" --minimized\" /SC ONLOGON /RL HIGHEST /F")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(psi)?.WaitForExit(3000);
                    }
                    catch { }

                    // 2. Also register in HKCU Run key
                    using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                    key?.SetValue(AppName, $"\"{exePath}\" --minimized");
                }
                else
                {
                    // 1. Delete Scheduled Task
                    try
                    {
                        var psi = new ProcessStartInfo("schtasks", $"/Delete /TN \"{AppName}\" /F")
                        {
                            CreateNoWindow = true,
                            UseShellExecute = false
                        };
                        Process.Start(psi)?.WaitForExit(3000);
                    }
                    catch { }

                    // 2. Remove from HKCU Run key
                    using var key = Registry.CurrentUser.OpenSubKey(RunKeyPath, true);
                    key?.DeleteValue(AppName, false);
                }
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[AutoStartService] Set error: {ex.Message}");
            }
        }
    }
}
