using System;
using System.IO;
using System.Text.Json;
using MI50FanControl.Models;

namespace MI50FanControl.Services
{
    public class SettingsService
    {
        private static readonly string AppDataFolder = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
            "MI50FanControl");

        private static readonly string SettingsFilePath = Path.Combine(AppDataFolder, "appsettings.json");

        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            WriteIndented = true
        };

        public AppSettings Current { get; private set; } = new();

        public void Load()
        {
            try
            {
                if (File.Exists(SettingsFilePath))
                {
                    string json = File.ReadAllText(SettingsFilePath);
                    var loaded = JsonSerializer.Deserialize<AppSettings>(json, JsonOptions);
                    if (loaded != null)
                    {
                        Current = loaded;
                        EnsureDefaults();
                        return;
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Load error: {ex.Message}");
            }

            Current = new AppSettings();
            EnsureDefaults();
            Save();
        }

        private void EnsureDefaults()
        {
            if (Current.CurveProfiles == null || Current.CurveProfiles.Count == 0)
            {
                Current.CurveProfiles = FanCurveProfile.CreateDefaultProfiles();
            }
            if (string.IsNullOrEmpty(Current.ActiveCurveProfileId))
            {
                Current.ActiveCurveProfileId = Current.CurveProfiles[0].Id;
            }
            if (Current.FanConfigs == null)
            {
                Current.FanConfigs = new();
            }

            // Ensure any fans default to FollowCurve so all connected fans respond to the temperature curve
            foreach (var cfg in Current.FanConfigs)
            {
                if (cfg.Mode == FanControlMode.BiosDefault)
                {
                    cfg.Mode = FanControlMode.FollowCurve;
                }
            }

            if (AutoStartService.IsAutoStartEnabled())
            {
                Current.StartWithWindows = true;
            }
        }

        public void Save()
        {
            try
            {
                if (!Directory.Exists(AppDataFolder))
                {
                    Directory.CreateDirectory(AppDataFolder);
                }

                string json = JsonSerializer.Serialize(Current, JsonOptions);
                File.WriteAllText(SettingsFilePath, json);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[SettingsService] Save error: {ex.Message}");
            }
        }
    }
}
