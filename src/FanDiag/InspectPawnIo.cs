using System;
using System.Reflection;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class InspectPawnIo
    {
        public static void Run()
        {
            var asm = typeof(Computer).Assembly;
            var lpcIoType = asm.GetType("LibreHardwareMonitor.PawnIo.LpcIo");

            if (lpcIoType != null)
            {
                foreach (var c in lpcIoType.GetConstructors(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    var pars = string.Join(", ", Array.ConvertAll(c.GetParameters(), p => $"{p.ParameterType.Name} {p.Name}"));
                    Console.WriteLine($"LpcIo ctor({pars})");
                }
            }
        }
    }
}
