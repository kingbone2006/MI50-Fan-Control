using System;
using System.Linq;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class SuperIoDeepScan
    {
        public static void ScanAndTest()
        {
            Console.WriteLine("========================================");
            Console.WriteLine(" SUPERIO / FAN CONTROLS DEEP SCAN");
            Console.WriteLine("========================================");

            var computer = new Computer
            {
                IsMotherboardEnabled = true,
                IsControllerEnabled = true,
                IsCpuEnabled = true,
                IsGpuEnabled = true,
                IsMemoryEnabled = false
            };

            try
            {
                computer.Open();

                // Do 3 updates to ensure all sensor readings and controls settle
                for (int i = 0; i < 3; i++)
                {
                    foreach (var h in computer.Hardware)
                    {
                        h.Update();
                        foreach (var sub in h.SubHardware)
                        {
                            sub.Update();
                        }
                    }
                    Thread.Sleep(300);
                }

                Console.WriteLine("\n--- ALL HARDWARE & SENSORS & CONTROLS ---");
                var allHw = computer.Hardware.Concat(computer.Hardware.SelectMany(h => h.SubHardware)).ToList();

                foreach (var hw in allHw)
                {
                    Console.WriteLine($"\n[Hardware] {hw.Name} ({hw.HardwareType}) - ID: {hw.Identifier}");

                    var fans = hw.Sensors.Where(s => s.SensorType == SensorType.Fan).ToList();
                    var controls = hw.Sensors.Where(s => s.SensorType == SensorType.Control).ToList();

                    Console.WriteLine($"  Fan Sensors count: {fans.Count}");
                    foreach (var fan in fans)
                    {
                        Console.WriteLine($"    -> Fan: '{fan.Name}', Value: {fan.Value} RPM, Index: {fan.Index}, Id: {fan.Identifier}, Control: {fan.Control}");
                    }

                    Console.WriteLine($"  Control Sensors count: {controls.Count}");
                    foreach (var c in controls)
                    {
                        Console.WriteLine($"    -> Control: '{c.Name}', Value: {c.Value}%, Index: {c.Index}, Id: {c.Identifier}, ControlMode: {c.Control?.ControlMode}, Max: {c.Control?.MaxSoftwareValue}, Min: {c.Control?.MinSoftwareValue}");
                    }
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
            finally
            {
                computer.Close();
            }
        }
    }
}
