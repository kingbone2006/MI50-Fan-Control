using System;
using System.Reflection;

namespace FanDiag
{
    public class CheckInstallerResources
    {
        public static void Run()
        {
            try
            {
                var asm = Assembly.LoadFrom(@"C:\Users\MI50\Desktop\fancontrol\Setup\MI50FanControl_Setup.dll");
                Console.WriteLine("Resource names in Setup.dll:");
                foreach (var name in asm.GetManifestResourceNames())
                {
                    Console.WriteLine(" - " + name);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine("Error: " + ex.Message);
            }
        }
    }
}
