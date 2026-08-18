using System;
using System.Runtime.InteropServices;
using System.Threading;
using Microsoft.Win32.SafeHandles;

namespace FanDiag
{
    public class TestSmartGuardianRegisters
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern SafeFileHandle CreateFile(
            string lpFileName,
            uint dwDesiredAccess,
            uint dwShareMode,
            IntPtr lpSecurityAttributes,
            uint dwCreationDisposition,
            uint dwFlagsAndAttributes,
            IntPtr hTemplateFile);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool DeviceIoControl(
            SafeFileHandle hDevice,
            uint dwIoControlCode,
            byte[]? lpInBuffer,
            uint nInBufferSize,
            byte[]? lpOutBuffer,
            uint nOutBufferSize,
            out uint lpBytesReturned,
            IntPtr lpOverlapped);

        private static SafeFileHandle? _driver;
        private const ushort ADDR = 0x0A35;
        private const ushort DATA = 0x0A36;

        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" TEST ITE IT8772F SMARTGUARDIAN REGISTER MODES");
            Console.WriteLine("==================================================");

            _driver = CreateFile(
                "\\\\.\\SpeedFan",
                0x80000000 | 0x40000000,
                0,
                IntPtr.Zero,
                3,
                0,
                IntPtr.Zero);

            if (_driver.IsInvalid)
            {
                Console.WriteLine("Could not open \\\\.\\SpeedFan driver");
                return;
            }

            void WritePort(ushort port, byte val)
            {
                uint[] inBuf = { (uint)port, 1, (uint)val };
                byte[] inBytes = new byte[12];
                Buffer.BlockCopy(inBuf, 0, inBytes, 0, 12);
                byte[] outBytes = new byte[8];
                DeviceIoControl(_driver, 0x9C402434, inBytes, (uint)inBytes.Length, outBytes, (uint)outBytes.Length, out _, IntPtr.Zero);
            }

            byte ReadPort(ushort port)
            {
                uint[] inBuf = { (uint)port, 1 };
                byte[] inBytes = new byte[8];
                Buffer.BlockCopy(inBuf, 0, inBytes, 0, 8);
                byte[] outBytes = new byte[8];
                if (DeviceIoControl(_driver, 0x9C402430, inBytes, (uint)inBytes.Length, outBytes, (uint)outBytes.Length, out uint retBytes, IntPtr.Zero) && retBytes >= 1)
                {
                    return outBytes[0];
                }
                return 0xFF;
            }

            byte ReadReg(byte reg)
            {
                WritePort(ADDR, reg);
                return ReadPort(DATA);
            }

            void WriteReg(byte reg, byte val)
            {
                WritePort(ADDR, reg);
                WritePort(DATA, val);
            }

            int ReadFanRpm(int fanIdx)
            {
                byte lsb = ReadReg((byte)(0x0D + fanIdx));
                byte msb = ReadReg((byte)(0x18 + fanIdx));
                int count = (msb << 8) | lsb;
                return (count > 0 && count < 0xFFFF) ? (int)(1350000.0 / (count * 2)) : 0;
            }

            Console.WriteLine("Initial Registers:");
            Console.WriteLine($"  Reg 0x0C (Tachometer enable): 0x{ReadReg(0x0C):X2}");
            Console.WriteLine($"  Reg 0x13 (Fan Control):       0x{ReadReg(0x13):X2}");
            Console.WriteLine($"  Reg 0x14 (Fan Polarity/Freq): 0x{ReadReg(0x14):X2}");
            Console.WriteLine($"  Reg 0x15 (Fan 1 PWM):         0x{ReadReg(0x15):X2}");
            Console.WriteLine($"  Reg 0x16 (Fan 2 PWM):         0x{ReadReg(0x16):X2}");
            Console.WriteLine($"  Reg 0x63 (Fan 1 Ext Duty):    0x{ReadReg(0x63):X2}");
            Console.WriteLine($"  Reg 0x6B (Fan 2 Ext Duty):    0x{ReadReg(0x6B):X2}");
            Console.WriteLine($"  Fan 1 RPM: {ReadFanRpm(0)} RPM");
            Console.WriteLine($"  Fan 2 RPM: {ReadFanRpm(1)} RPM");

            // Test setting software PWM with 0x38 on Reg 0x13 and 0xD9 (70%) on Reg 0x15 & 0x16
            Console.WriteLine("\n[TEST] Applying Software PWM Mode (Reg 0x13 = 0x38, Reg 0x15/0x16 = 0xD9, Reg 0x63/0x6B = 178)...");
            WriteReg(0x00, 0x01); // Start monitor
            WriteReg(0x0C, 0x1F); // Enable all tachs
            WriteReg(0x13, 0x38); // PWM mode on Fan 1, 2, 3
            WriteReg(0x15, 0xD9); // 70% 7-bit Fan 1
            WriteReg(0x16, 0xD9); // 70% 7-bit Fan 2
            WriteReg(0x63, 178);  // 70% 8-bit Fan 1
            WriteReg(0x6B, 178);  // 70% 8-bit Fan 2

            for (int s = 1; s <= 5; s++)
            {
                Thread.Sleep(1000);
                Console.WriteLine($"  [{s}s] Fan 1: {ReadFanRpm(0),5} RPM | Fan 2: {ReadFanRpm(1),5} RPM | Reg 0x13: 0x{ReadReg(0x13):X2}");
            }
        }
    }
}
