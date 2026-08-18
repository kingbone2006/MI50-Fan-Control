using System;
using System.IO;

namespace FanDiag
{
    public class InspectSpeedfanChipName
    {
        public static void Run()
        {
            // 1. Check speedfansens.cfg in both Engine and AppData
            string[] cfgPaths = new[]
            {
                @"C:\Users\MI50\Desktop\fancontrol\src\MI50FanControl\Engine\speedfansens.cfg",
                @"C:\Program Files\MI50FanControl\Engine\speedfansens.cfg",
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "SpeedFan", "speedfansens.cfg"),
                Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "SpeedFan", "speedfansens.cfg")
            };

            foreach (var p in cfgPaths)
            {
                if (File.Exists(p))
                {
                    Console.WriteLine($"=== Found config: {p} ===");
                    string[] lines = File.ReadAllLines(p);
                    foreach (var line in lines)
                    {
                        if (line.StartsWith("Chip=") || line.Contains("IT8") || line.Contains("NCT") || line.Contains("W83") || line.Contains("Chip") || line.Contains("ISA"))
                        {
                            Console.WriteLine($"  {line}");
                        }
                    }
                }
            }
        }
    }
}
