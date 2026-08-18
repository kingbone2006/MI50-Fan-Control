using System;
using System.Security.Principal;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class SuperIoTest
    {
        public static void TestSuperIo()
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            bool isAdmin = principal.IsInRole(WindowsBuiltInRole.Administrator);
            Console.WriteLine($"Is Administrator: {isAdmin}");

            var computer = new Computer
            {
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsCpuEnabled = true,
                IsGpuEnabled = false
            };
            computer.Open();

            Console.WriteLine($"\n--- MOTHERBOARD & SUPERIO DETECTION ---");
            foreach (var hw in computer.Hardware)
            {
                Console.WriteLine($"Hardware: [{hw.HardwareType}] Name: {hw.Name}, Identifier: {hw.Identifier}");
                hw.Update();

                foreach (var sub in hw.SubHardware)
                {
                    Console.WriteLine($"  SubHardware: [{sub.HardwareType}] Name: {sub.Name}, Identifier: {sub.Identifier}");
                    sub.Update();

                    foreach (var s in sub.Sensors)
                    {
                        Console.WriteLine($"    Sensor: [{s.SensorType}] {s.Name} (Index: {s.Index}) = {s.Value} (Control Mode: {s.Control?.ControlMode})");
                    }
                }

                foreach (var s in hw.Sensors)
                {
                    Console.WriteLine($"  Sensor: [{s.SensorType}] {s.Name} (Index: {s.Index}) = {s.Value} (Control Mode: {s.Control?.ControlMode})");
                }
            }

            computer.Close();
        }
    }
}
