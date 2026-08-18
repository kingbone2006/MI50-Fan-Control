using System;
using System.Linq;
using System.Reflection;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class DumpRing0
    {
        public static void Inspect()
        {
            var asm = typeof(Computer).Assembly;
            Console.WriteLine($"Assembly: {asm.FullName}");

            var types = asm.GetTypes().Where(t => t.Name.Contains("Ring0") || t.Name.Contains("Lpc") || t.Name.Contains("Port") || t.Name.Contains("IO") || t.Name.Contains("Driver") || t.Name.Contains("IT87")).ToList();
            foreach (var t in types)
            {
                Console.WriteLine($"Type: {t.FullName}");
                foreach (var m in t.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static | BindingFlags.Instance))
                {
                    if (m.DeclaringType == t)
                    {
                        var pars = string.Join(", ", Array.ConvertAll(m.GetParameters(), p => $"{p.ParameterType.Name} {p.Name}"));
                        Console.WriteLine($"   {m.ReturnType.Name} {m.Name}({pars})");
                    }
                }
            }
        }
    }
}
