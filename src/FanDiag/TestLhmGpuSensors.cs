using System;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class TestLhmGpuSensors
    {
        public static void Run()
        {
            var computer = new Computer
            {
                IsGpuEnabled = true
            };
            computer.Open();

            foreach (var hardware in computer.Hardware)
            {
                Console.WriteLine($"Hardware: {hardware.Name} ({hardware.HardwareType})");
                hardware.Update();

                foreach (var sensor in hardware.Sensors)
                {
                    Console.WriteLine($"  Sensor: [{sensor.SensorType}] {sensor.Name} = {sensor.Value}");
                }

                foreach (var subHardware in hardware.SubHardware)
                {
                    subHardware.Update();
                    Console.WriteLine($"  SubHardware: {subHardware.Name}");
                    foreach (var sensor in subHardware.Sensors)
                    {
                        Console.WriteLine($"    Sensor: [{sensor.SensorType}] {sensor.Name} = {sensor.Value}");
                    }
                }
            }

            computer.Close();
        }
    }
}
