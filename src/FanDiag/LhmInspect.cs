using System;
using System.Linq;
using System.Reflection;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class LhmInspect
    {
        public static void Inspect()
        {
            Console.WriteLine("\n--- LIBREHARDWAREMONITOR AMD GPU INTERNALS ---");
            var computer = new Computer
            {
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true
            };
            computer.Open();

            foreach (var hw in computer.Hardware)
            {
                Console.WriteLine($"\nHardware: [{hw.HardwareType}] {hw.Name} ({hw.GetType().FullName})");
                hw.Update();

                // Inspect fields via reflection
                var fields = hw.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                foreach (var f in fields)
                {
                    try
                    {
                        var val = f.GetValue(hw);
                        Console.WriteLine($"  Field '{f.Name}' = {val}");
                    }
                    catch { }
                }

                foreach (var s in hw.Sensors)
                {
                    Console.WriteLine($"  Sensor: [{s.SensorType}] {s.Name} = {s.Value}");
                }
            }

            computer.Close();
        }
    }
}
