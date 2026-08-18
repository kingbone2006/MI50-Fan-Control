using System;
using System.Linq;
using System.Reflection;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class PrintAllTypes
    {
        public static void Run()
        {
            var asm = typeof(Computer).Assembly;
            Console.WriteLine($"All Types in {asm.GetName().Name}:");
            foreach (var t in asm.GetTypes().OrderBy(t => t.FullName))
            {
                if (t.Namespace != null && (t.Namespace.Contains("Motherboard") || t.Namespace.Contains("PawnIo") || t.Namespace.Contains("Driver") || t.Namespace.Contains("Interop") || t.Name.Contains("IO") || t.Name.Contains("Port")))
                {
                    Console.WriteLine($"  {t.FullName}");
                }
            }
        }
    }
}
