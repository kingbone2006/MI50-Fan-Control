using System;
using System.Diagnostics;
using System.IO;

namespace FanDiag
{
    public class TestSilentInstallDriver
    {
        public static void Run()
        {
            string sfInst = @"C:\Users\MI50\Downloads\ABDM\Programs\instspeedfan452.exe";
            if (!File.Exists(sfInst))
            {
                Console.WriteLine("instspeedfan452.exe not found");
                return;
            }

            Console.WriteLine("Running instspeedfan452.exe /S...");
            var psi = new ProcessStartInfo
            {
                FileName = sfInst,
                Arguments = "/S",
                UseShellExecute = true,
                Verb = "runas"
            };

            try
            {
                var p = Process.Start(psi);
                p?.WaitForExit(10000);
                Console.WriteLine("Done installing SpeedFan driver!");

                // Check if speedfan.sys exists now!
                string sys1 = @"C:\Windows\SysWOW64\speedfan.sys";
                string sys2 = @"C:\Windows\System32\drivers\speedfan.sys";
                Console.WriteLine($"sys1 exists = {File.Exists(sys1)}");
                Console.WriteLine($"sys2 exists = {File.Exists(sys2)}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error: {ex.Message}");
            }
        }
    }
}
