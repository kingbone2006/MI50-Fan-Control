using System;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FanDiag
{
    public class TestSpeedfanIoPort
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

        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" TEST SPEEDFAN DRIVER PORT I/O (0x9C402430 / 0x9C402434)");
            Console.WriteLine("==================================================");

            const uint GENERIC_READ = 0x80000000;
            const uint GENERIC_WRITE = 0x40000000;
            const uint OPEN_EXISTING = 3;

            using var hDriver = CreateFile(
                "\\\\.\\SpeedFan",
                GENERIC_READ | GENERIC_WRITE,
                0,
                IntPtr.Zero,
                OPEN_EXISTING,
                0,
                IntPtr.Zero);

            if (hDriver.IsInvalid)
            {
                Console.WriteLine("Could not open \\\\.\\SpeedFan");
                return;
            }

            // Test structure for ReadPort (0x9C402430):
            // Buffer: uint32 port, uint32 size (1 = byte, 2 = word, 4 = dword)
            byte ReadPort(ushort port)
            {
                uint[] inBuf = { (uint)port, 1 };
                byte[] inBytes = new byte[8];
                Buffer.BlockCopy(inBuf, 0, inBytes, 0, 8);

                byte[] outBytes = new byte[8];
                bool ok = DeviceIoControl(hDriver, 0x9C402430, inBytes, (uint)inBytes.Length, outBytes, (uint)outBytes.Length, out uint retBytes, IntPtr.Zero);
                if (ok && retBytes >= 1)
                {
                    return outBytes[0];
                }
                return 0xFF;
            }

            void WritePort(ushort port, byte val)
            {
                uint[] inBuf = { (uint)port, 1, (uint)val };
                byte[] inBytes = new byte[12];
                Buffer.BlockCopy(inBuf, 0, inBytes, 0, 12);

                byte[] outBytes = new byte[8];
                DeviceIoControl(hDriver, 0x9C402434, inBytes, (uint)inBytes.Length, outBytes, (uint)outBytes.Length, out uint retBytes, IntPtr.Zero);
            }

            ushort baseAddr = 0x0A30;
            ushort addrPort = (ushort)(baseAddr + 5);
            ushort dataPort = (ushort)(baseAddr + 6);

            WritePort(addrPort, 0x58);
            byte vId = ReadPort(dataPort);

            WritePort(addrPort, 0x5B);
            byte c1 = ReadPort(dataPort);

            WritePort(addrPort, 0x5C);
            byte c2 = ReadPort(dataPort);

            Console.WriteLine($"Vendor ID: 0x{vId:X2}, Chip ID: 0x{c1:X2} 0x{c2:X2}");

            // Read Fan 1 & Fan 2 Tachometers
            for (int f = 0; f < 2; f++)
            {
                byte lsbReg = (byte)(0x0D + f);
                byte msbReg = (byte)(0x18 + f);

                WritePort(addrPort, lsbReg);
                byte lsb = ReadPort(dataPort);

                WritePort(addrPort, msbReg);
                byte msb = ReadPort(dataPort);

                int count = (msb << 8) | lsb;
                int rpm = count > 0 && count < 0xFFFF ? (int)(1350000.0 / (count * 2)) : 0;
                Console.WriteLine($"  Fan {f + 1}: Count=0x{count:X4} -> {rpm} RPM");
            }
        }
    }
}
