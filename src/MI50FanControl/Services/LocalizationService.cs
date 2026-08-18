using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Text.Json;

namespace MI50FanControl.Services
{
    public class LanguageOption
    {
        public string Code { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
    }

    public class LocalizationService : INotifyPropertyChanged
    {
        private static LocalizationService? _instance;
        public static LocalizationService Instance => _instance ??= new LocalizationService();

        private Dictionary<string, string> _currentStrings = new();
        private string _currentLanguage = "vi";

        public event PropertyChangedEventHandler? PropertyChanged;
        public event EventHandler? LanguageChanged;

        public string CurrentLanguage => _currentLanguage;

        public string this[string key]
        {
            get
            {
                if (_currentStrings.TryGetValue(key, out var val))
                {
                    return val;
                }
                return key;
            }
        }

        public string Get(string key, string fallback = "")
        {
            if (_currentStrings.TryGetValue(key, out var val))
            {
                return val;
            }
            return string.IsNullOrEmpty(fallback) ? key : fallback;
        }

        public List<LanguageOption> GetAvailableLanguages()
        {
            var list = new List<LanguageOption>
            {
                new LanguageOption { Code = "vi", DisplayName = "Tiếng Việt (Vietnamese)" },
                new LanguageOption { Code = "en", DisplayName = "English (Tiếng Anh)" }
            };

            string langDir = GetLangDirectory();
            if (Directory.Exists(langDir))
            {
                foreach (var file in Directory.GetFiles(langDir, "*.json"))
                {
                    string filename = Path.GetFileNameWithoutExtension(file).ToLowerInvariant();
                    if (filename == "template" || filename == "vi" || filename == "en") continue;

                    list.Add(new LanguageOption
                    {
                        Code = filename,
                        DisplayName = filename.ToUpperInvariant()
                    });
                }
            }

            return list;
        }

        public void SetLanguage(string langCode)
        {
            _currentLanguage = string.IsNullOrWhiteSpace(langCode) ? "vi" : langCode.ToLowerInvariant();
            LoadLanguage(_currentLanguage);

            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs("Item[]"));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(""));
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(null));
            LanguageChanged?.Invoke(this, EventArgs.Empty);
        }

        private void LoadLanguage(string code)
        {
            _currentStrings.Clear();

            string langDir = GetLangDirectory();
            string targetFile = Path.Combine(langDir, $"{code}.json");
            string fallbackFile = Path.Combine(langDir, "vi.json");

            if (!File.Exists(targetFile))
            {
                targetFile = fallbackFile;
            }

            if (File.Exists(targetFile))
            {
                try
                {
                    string json = File.ReadAllText(targetFile);
                    var dict = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (dict != null)
                    {
                        foreach (var kvp in dict)
                        {
                            _currentStrings[kvp.Key] = kvp.Value;
                        }
                    }
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"[LocalizationService] Load error: {ex.Message}");
                }
            }
        }

        public string GetLangDirectory()
        {
            string baseDir = AppDomain.CurrentDomain.BaseDirectory;
            string localLang = Path.Combine(baseDir, "lang");
            if (Directory.Exists(localLang)) return localLang;

            // Fallback for development debugging
            string devLang = Path.GetFullPath(Path.Combine(baseDir, @"..\..\..\lang"));
            if (Directory.Exists(devLang)) return devLang;

            return localLang;
        }

        public void OpenLanguageFolder()
        {
            string dir = GetLangDirectory();
            if (!Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }
            try
            {
                Process.Start(new ProcessStartInfo
                {
                    FileName = dir,
                    UseShellExecute = true,
                    Verb = "open"
                });
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"[LocalizationService] Open folder error: {ex.Message}");
            }
        }
    }
}
