using System;
using System.IO;
using System.Runtime.InteropServices;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class LpcProbe
    {
        public static void ProbeSuperIoAndAdl()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" 1. DIRECT SUPERIO LPC IO PROBING");
            Console.WriteLine("==================================================");

            // Open LibreHardwareMonitor Computer first so its embedded driver is extracted & loaded
            var computer = new Computer { IsMotherboardEnabled = true, IsCpuEnabled = true, IsGpuEnabled = true };
            try { computer.Open(); } catch { }

            using var driver = new WinRing0Driver();
            if (!driver.Open())
            {
                Console.WriteLine("Could not open WinRing0 device driver handle.");
            }
            else
            {
                Console.WriteLine("Driver open OK. Scanning ports...");

                // Probe port pairs
                ProbePortPair(driver, 0x2E, 0x2F);
                ProbePortPair(driver, 0x4E, 0x4F);

                // ITE
                ProbeIte(driver, 0x2E, 0x2F);
                ProbeIte(driver, 0x4E, 0x4F);

                // Nuvoton / Winbond
                ProbeNuvoton(driver, 0x2E, 0x2F);
                ProbeNuvoton(driver, 0x4E, 0x4F);

                // Fintek
                ProbeFintek(driver, 0x4E, 0x4F);
                ProbeFintek(driver, 0x2E, 0x2F);
            }

            try { computer.Close(); } catch { }
        }

        private static void ProbePortPair(WinRing0Driver driver, ushort regPort, ushort valPort)
        {
            try
            {
                driver.WriteIoPort(regPort, 0x20);
                byte id1 = driver.ReadIoPort(valPort);
                driver.WriteIoPort(regPort, 0x21);
                byte id2 = driver.ReadIoPort(valPort);
                Console.WriteLine($"Port Pair (0x{regPort:X2}, 0x{valPort:X2}): Reg 0x20=0x{id1:X2}, Reg 0x21=0x{id2:X2}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error reading (0x{regPort:X2}): {ex.Message}");
            }
        }

        private static void ProbeIte(WinRing0Driver driver, ushort regPort, ushort valPort)
        {
            try
            {
                driver.WriteIoPort(regPort, 0x87);
                driver.WriteIoPort(regPort, 0x01);
                driver.WriteIoPort(regPort, 0x55);
                driver.WriteIoPort(regPort, (byte)(regPort == 0x4E ? 0xAA : 0x55));

                driver.WriteIoPort(regPort, 0x20);
                byte chipId1 = driver.ReadIoPort(valPort);
                driver.WriteIoPort(regPort, 0x21);
                byte chipId2 = driver.ReadIoPort(valPort);
                ushort chipId = (ushort)((chipId1 << 8) | chipId2);

                Console.WriteLine($"ITE Probe at 0x{regPort:X2}: Chip ID = 0x{chipId:X4} (0x{chipId1:X2}, 0x{chipId2:X2})");

                if (chipId != 0x0000 && chipId != 0xFFFF)
                {
                    driver.WriteIoPort(regPort, 0x07);
                    driver.WriteIoPort(valPort, 0x04);

                    driver.WriteIoPort(regPort, 0x60);
                    byte baseHigh = driver.ReadIoPort(valPort);
                    driver.WriteIoPort(regPort, 0x61);
                    byte baseLow = driver.ReadIoPort(valPort);
                    ushort hwMonitorBase = (ushort)((baseHigh << 8) | baseLow);

                    Console.WriteLine($"  -> ITE Chip detected! HW Monitor Base Address: 0x{hwMonitorBase:X4}");

                    if (hwMonitorBase != 0 && hwMonitorBase != 0xFFFF)
                    {
                        ReadIteFans(driver, hwMonitorBase);
                    }
                }

                driver.WriteIoPort(regPort, 0x02);
                driver.WriteIoPort(valPort, 0x02);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ITE probe error at 0x{regPort:X2}: {ex.Message}");
            }
        }

        private static void ReadIteFans(WinRing0Driver driver, ushort baseAddr)
        {
            try
            {
                Console.WriteLine($"  --- ITE Fans at Base 0x{baseAddr:X4} ---");
                for (int i = 0; i < 5; i++)
                {
                    byte lsbReg = (byte)(i < 3 ? (0x0D + i) : (0x80 + (i - 3) * 2));
                    byte msbReg = (byte)(i < 3 ? (0x18 + i) : (0x81 + (i - 3) * 2));

                    driver.WriteIoPort((ushort)(baseAddr + 5), lsbReg);
                    byte lsb = driver.ReadIoPort((ushort)(baseAddr + 6));
                    driver.WriteIoPort((ushort)(baseAddr + 5), msbReg);
                    byte msb = driver.ReadIoPort((ushort)(baseAddr + 6));

                    int count = (msb << 8) | lsb;
                    int rpm = count > 0 && count < 0xFFFF ? (int)(1350000.0 / (count * 2)) : 0;

                    byte pwmReg = (byte)(0x15 + i);
                    driver.WriteIoPort((ushort)(baseAddr + 5), pwmReg);
                    byte pwmVal = driver.ReadIoPort((ushort)(baseAddr + 6));
                    int pwmPercent = (int)Math.Round((pwmVal / 255.0) * 100);

                    Console.WriteLine($"    Fan #{i + 1}: RawCount=0x{count:X4} -> {rpm} RPM | PWM Reg 0x{pwmReg:X2} = 0x{pwmVal:X2} ({pwmPercent}%)");
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"ReadIteFans error: {ex.Message}");
            }
        }

        private static void ProbeNuvoton(WinRing0Driver driver, ushort regPort, ushort valPort)
        {
            try
            {
                driver.WriteIoPort(regPort, 0x87);
                driver.WriteIoPort(regPort, 0x87);

                driver.WriteIoPort(regPort, 0x20);
                byte chipId1 = driver.ReadIoPort(valPort);
                driver.WriteIoPort(regPort, 0x21);
                byte chipId2 = driver.ReadIoPort(valPort);
                ushort chipId = (ushort)((chipId1 << 8) | chipId2);

                Console.WriteLine($"Nuvoton Probe at 0x{regPort:X2}: Chip ID = 0x{chipId:X4} (0x{chipId1:X2}, 0x{chipId2:X2})");

                if (chipId != 0x0000 && chipId != 0xFFFF)
                {
                    for (byte ld = 0x07; ld <= 0x0C; ld++)
                    {
                        driver.WriteIoPort(regPort, 0x07);
                        driver.WriteIoPort(valPort, ld);

                        driver.WriteIoPort(regPort, 0x60);
                        byte bH = driver.ReadIoPort(valPort);
                        driver.WriteIoPort(regPort, 0x61);
                        byte bL = driver.ReadIoPort(valPort);
                        ushort addr = (ushort)((bH << 8) | bL);
                        if (addr != 0 && addr != 0xFFFF)
                        {
                            Console.WriteLine($"  -> Nuvoton LD 0x{ld:X2} Base Address: 0x{addr:X4}");
                        }
                    }
                }

                driver.WriteIoPort(regPort, 0xAA);
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Nuvoton probe error at 0x{regPort:X2}: {ex.Message}");
            }
        }

        private static void ProbeFintek(WinRing0Driver driver, ushort regPort, ushort valPort)
        {
            try
            {
                driver.WriteIoPort(regPort, 0x87);
                driver.WriteIoPort(regPort, 0x87);

                driver.WriteIoPort(regPort, 0x20);
                byte id1 = driver.ReadIoPort(valPort);
                driver.WriteIoPort(regPort, 0x21);
                byte id2 = driver.ReadIoPort(valPort);
                Console.WriteLine($"Fintek Probe at 0x{regPort:X2}: Chip ID = 0x{id1:X2} 0x{id2:X2}");

                driver.WriteIoPort(regPort, 0xAA);
            }
            catch { }
        }
    }
}
