using System;
using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using WpfApplication = System.Windows.Application;

namespace MI50FanControl.Services
{
    public enum LogLevel
    {
        Info,
        Success,
        Warning,
        Error,
        Hardware,
        Debug
    }

    public class LogEntry
    {
        public DateTime Timestamp { get; set; } = DateTime.Now;
        public LogLevel Level { get; set; } = LogLevel.Info;
        public string Category { get; set; } = "General";
        public string Message { get; set; } = string.Empty;

        public string TimeFormatted => Timestamp.ToString("HH:mm:ss.fff");

        public string LevelBadge => Level switch
        {
            LogLevel.Success => " [OK] ",
            LogLevel.Warning => "[WARN]",
            LogLevel.Error => "[ERR] ",
            LogLevel.Hardware => "[HW]  ",
            LogLevel.Debug => "[DBG] ",
            _ => "[INFO]"
        };

        public string DisplayText => $"[{TimeFormatted}] {LevelBadge} [{Category}] {Message}";
    }

    public class LogService
    {
        private static LogService? _instance;
        public static LogService Instance => _instance ??= new LogService();

        private readonly object _lock = new();
        private const int MaxEntries = 1000;

        public ObservableCollection<LogEntry> Entries { get; } = new();

        public event EventHandler<LogEntry>? EntryAdded;

        public void Log(LogLevel level, string category, string message)
        {
            var entry = new LogEntry
            {
                Level = level,
                Category = category,
                Message = message
            };

            lock (_lock)
            {
                WpfApplication.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    if (Entries.Count >= MaxEntries)
                    {
                        Entries.RemoveAt(0);
                    }
                    Entries.Add(entry);
                    EntryAdded?.Invoke(this, entry);
                });
            }

            System.Diagnostics.Debug.WriteLine(entry.DisplayText);
        }

        public void Info(string category, string message) => Log(LogLevel.Info, category, message);
        public void Success(string category, string message) => Log(LogLevel.Success, category, message);
        public void Warn(string category, string message) => Log(LogLevel.Warning, category, message);
        public void Error(string category, string message) => Log(LogLevel.Error, category, message);
        public void Hardware(string category, string message) => Log(LogLevel.Hardware, category, message);
        public void Debug(string category, string message) => Log(LogLevel.Debug, category, message);

        public string GetAllLogsAsText()
        {
            var sb = new StringBuilder();
            sb.AppendLine("=== AMD MI50 FAN CONTROL - DIAGNOSTIC LOG DUMP ===");
            sb.AppendLine($"Export Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            sb.AppendLine($"OS: {Environment.OSVersion} ({(Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit")})");
            sb.AppendLine($"Machine: {Environment.MachineName}, User: {Environment.UserName}");
            sb.AppendLine("===================================================\n");

            lock (_lock)
            {
                foreach (var entry in Entries)
                {
                    sb.AppendLine(entry.DisplayText);
                }
            }

            return sb.ToString();
        }

        public void SaveLogToFile(string filePath)
        {
            File.WriteAllText(filePath, GetAllLogsAsText());
        }

        public void Clear()
        {
            lock (_lock)
            {
                WpfApplication.Current?.Dispatcher?.InvokeAsync(() =>
                {
                    Entries.Clear();
                });
            }
        }
    }
}
