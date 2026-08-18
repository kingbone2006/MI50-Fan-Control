using System;
using System.Reflection;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class TestNativeIt87XX
    {
        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" DIRECT INSTANTIATION OF LIBREHARDWAREMONITOR IT87XX");
            Console.WriteLine("==================================================");

            var asm = typeof(Computer).Assembly;
            var it87xxType = asm.GetType("LibreHardwareMonitor.Hardware.Motherboard.Lpc.IT87XX");
            var chipType = asm.GetType("LibreHardwareMonitor.Hardware.Motherboard.Lpc.Chip");

            if (it87xxType == null || chipType == null)
            {
                Console.WriteLine("Could not find IT87XX or Chip type");
                return;
            }

            Console.WriteLine($"Found types: IT87XX={it87xxType}, Chip={chipType}");

            // List ctors
            foreach (var ctor in it87xxType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
            {
                var pars = string.Join(", ", Array.ConvertAll(ctor.GetParameters(), p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"  ctor({pars})");
            }
        }
    }
}
