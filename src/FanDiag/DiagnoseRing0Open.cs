using System;
using System.Reflection;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class DiagnoseRing0Open
    {
        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" RING0.OPEN() DETAILED DIAGNOSIS");
            Console.WriteLine("==================================================");

            var asm = typeof(Computer).Assembly;
            var ring0Type = asm.GetTypes().FirstOrDefault(t => t.Name == "Ring0");
            Console.WriteLine($"Ring0 Type FullName: {ring0Type?.FullName}");

            if (ring0Type != null)
            {
                var openMethod = ring0Type.GetMethod("Open", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                Console.WriteLine($"openMethod: {openMethod}");
                try
                {
                    openMethod?.Invoke(null, null);
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception calling Ring0.Open(): {ex.InnerException?.Message ?? ex.Message}");
                    Console.WriteLine(ex.InnerException?.StackTrace ?? ex.StackTrace);
                }

                var isOpenProp = ring0Type.GetProperty("IsOpen", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                var isOpen = isOpenProp?.GetValue(null);
                Console.WriteLine($"Ring0.IsOpen: {isOpen}");

                // Check Report or Error property if any
                var reportProp = ring0Type.GetProperty("Report", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static);
                if (reportProp != null)
                {
                    Console.WriteLine($"Ring0.Report:\n{reportProp.GetValue(null)}");
                }
            }
        }
    }
}
