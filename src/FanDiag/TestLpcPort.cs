using System;
using System.Reflection;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class TestLpcPort
    {
        public static void Run()
        {
            var asm = typeof(Computer).Assembly;
            var lpcPortType = asm.GetType("LibreHardwareMonitor.Hardware.Motherboard.Lpc.LpcPort");

            if (lpcPortType != null)
            {
                foreach (var ctor in lpcPortType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    var pars = string.Join(", ", Array.ConvertAll(ctor.GetParameters(), p => $"{p.ParameterType.Name} {p.Name}"));
                    Console.WriteLine($"LpcPort ctor({pars})");
                }
            }
        }
    }
}
