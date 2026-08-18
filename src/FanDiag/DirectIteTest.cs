using System;
using System.IO;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class DirectIteTest
    {
        public static void TestIteA30()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" DIRECT ITE IT8772F PROBE AT BASE 0x0A30");
            Console.WriteLine("==================================================");

            // Open LibreHardwareMonitor Computer so it drops and opens WinRing0 driver
            var computer = new Computer { IsMotherboardEnabled = true, IsCpuEnabled = true };
            try { computer.Open(); } catch { }

            using var driver = new WinRing0Driver();
            if (!driver.Open())
            {
                Console.WriteLine("Driver open failed.");
                try { computer.Close(); } catch { }
                return;
            }

            ushort[] candidateBases = { 0x0A30, 0x0290, 0x0A00, 0x0A10, 0x0A20, 0x0A40, 0x0A50 };

            foreach (var baseAddr in candidateBases)
            {
                Console.WriteLine($"\n--- Probing Candidate Base Address 0x{baseAddr:X4} ---");
                ushort addrPort = (ushort)(baseAddr + 5);
                ushort dataPort = (ushort)(baseAddr + 6);

                // Read Vendor ID (Reg 0x58 = 0x90) and Chip ID (Reg 0x5B = 0x87, Reg 0x5C = 0x72)
                driver.WriteIoPort(addrPort, 0x58);
                byte vendorId = driver.ReadIoPort(dataPort);

                driver.WriteIoPort(addrPort, 0x5B);
                byte chipId1 = driver.ReadIoPort(dataPort);

                driver.WriteIoPort(addrPort, 0x5C);
                byte chipId2 = driver.ReadIoPort(dataPort);

                Console.WriteLine($"  Vendor ID Reg 0x58: 0x{vendorId:X2} (Expected 0x90 for ITE)");
                Console.WriteLine($"  Chip ID Reg 0x5B/0x5C: 0x{chipId1:X2} 0x{chipId2:X2} (Expected 0x87 0x72 for IT8772)");

                if (vendorId == 0x90 || (chipId1 == 0x87 && chipId2 == 0x72))
                {
                    Console.WriteLine($"  >>> MATCHED ITE IT8772F at 0x{baseAddr:X4}! <<<");

                    // Read 5 Fans Tachometer
                    for (int f = 0; f < 5; f++)
                    {
                        byte lsbReg = (byte)(f < 3 ? (0x0D + f) : (0x80 + (f - 3) * 2));
                        byte msbReg = (byte)(f < 3 ? (0x18 + f) : (0x81 + (f - 3) * 2));

                        driver.WriteIoPort(addrPort, lsbReg);
                        byte lsb = driver.ReadIoPort(dataPort);
                        driver.WriteIoPort(addrPort, msbReg);
                        byte msb = driver.ReadIoPort(dataPort);

                        int count = (msb << 8) | lsb;
                        int rpm = count > 0 && count < 0xFFFF ? (int)(1350000.0 / (count * 2)) : 0;

                        // Read PWM Reg
                        byte pwmReg = (byte)(0x15 + f);
                        driver.WriteIoPort(addrPort, pwmReg);
                        byte pwmVal = driver.ReadIoPort(dataPort);

                        Console.WriteLine($"    [Fan #{f + 1}] Count=0x{count:X4} -> {rpm,5} RPM | PWM Reg 0x{pwmReg:X2}=0x{pwmVal:X2}");
                    }
                }
            }

            try { computer.Close(); } catch { }
        }
    }
}
