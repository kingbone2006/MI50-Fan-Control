using System;
using System.Reflection;
using System.Threading;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class LhmCheck
    {
        public static void Check()
        {
            var computer = new Computer
            {
                IsGpuEnabled = true,
                IsMotherboardEnabled = true,
                IsControllerEnabled = true
            };
            computer.Open();

            var gpu = computer.Hardware.FirstOrDefault(h => h.HardwareType == HardwareType.GpuAmd);
            if (gpu != null)
            {
                Console.WriteLine($"Found AMD GPU: {gpu.Name}");

                for (int loop = 0; loop < 5; loop++)
                {
                    gpu.Update();
                    Thread.Sleep(500);

                    var tempCoreField = gpu.GetType().GetField("_temperatureCore", BindingFlags.Instance | BindingFlags.NonPublic);
                    var tempHotspotField = gpu.GetType().GetField("_temperatureHotSpot", BindingFlags.Instance | BindingFlags.NonPublic);
                    var powerField = gpu.GetType().GetField("_powerTotal", BindingFlags.Instance | BindingFlags.NonPublic) 
                                     ?? gpu.GetType().GetField("_powerCore", BindingFlags.Instance | BindingFlags.NonPublic);
                    var clockField = gpu.GetType().GetField("_coreClock", BindingFlags.Instance | BindingFlags.NonPublic);

                    var coreSensor = tempCoreField?.GetValue(gpu) as ISensor;
                    var hotspotSensor = tempHotspotField?.GetValue(gpu) as ISensor;
                    var powerSensor = powerField?.GetValue(gpu) as ISensor;
                    var clockSensor = clockField?.GetValue(gpu) as ISensor;

                    Console.WriteLine($"[Loop {loop+1}] CoreTemp: {coreSensor?.Value} C, Hotspot: {hotspotSensor?.Value} C, Power: {powerSensor?.Value} W, Clock: {clockSensor?.Value} MHz");
                }
            }

            computer.Close();
        }
    }
}
