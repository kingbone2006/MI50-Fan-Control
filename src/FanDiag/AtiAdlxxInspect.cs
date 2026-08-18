using System;
using System.Reflection;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class AtiAdlxxInspect
    {
        public static void Inspect()
        {
            Console.WriteLine("--- INSPECTING LIBREHARDWAREMONITOR.INTEROP.ATIADLXX ---");
            var asm = typeof(Computer).Assembly;
            var atiType = asm.GetType("LibreHardwareMonitor.Interop.AtiAdlxx");
            if (atiType == null)
            {
                Console.WriteLine("AtiAdlxx type not found.");
                return;
            }

            var nestedTypes = atiType.GetNestedTypes(BindingFlags.Public | BindingFlags.NonPublic);
            foreach (var nt in nestedTypes)
            {
                Console.WriteLine($"\nStruct/Type: {nt.Name}");
                foreach (var f in nt.GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance))
                {
                    Console.WriteLine($"  Field: {f.Name} ({f.FieldType.Name})");
                }
            }

            Console.WriteLine("\n--- METHODS ---");
            foreach (var m in atiType.GetMethods(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static))
            {
                var prms = string.Join(", ", m.GetParameters().Select(p => $"{p.ParameterType.Name} {p.Name}"));
                Console.WriteLine($"  {m.ReturnType.Name} {m.Name}({prms})");
            }
        }
    }
}
