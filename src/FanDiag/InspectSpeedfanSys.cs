using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FanDiag
{
    public class InspectSpeedfanSys
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
            Console.WriteLine(" INSPECTING SPEEDFAN.SYS IOCTL CODES");
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

            Console.WriteLine("Successfully opened \\\\.\\SpeedFan!");

            // Test various known speedfan.sys IOCTL codes for Port I/O
            // Standard IOCTLs in SpeedFan: 0x9C402xxx
            // Let's test reading ISA port 0x0A35 / 0x0A36
            uint[] candidateIoctls = {
                0x9C402424, 0x9C402428, 0x9C40242C, 0x9C402430, 0x9C402434, 0x9C402438, 0x9C40243C, 0x9C402440,
                0x9C406424, 0x9C406428, 0x9C40642C, 0x9C406430, 0x9C406434, 0x9C406438,
                0x9C40A424, 0x9C40A428, 0x9C40A42C, 0x9C40A430
            };

            foreach (var code in candidateIoctls)
            {
                // Input buffer with port 0x0A35
                byte[] inBuf = new byte[16];
                BitConverter.GetBytes((ushort)0x0A35).CopyTo(inBuf, 0);
                byte[] outBuf = new byte[16];

                bool success = DeviceIoControl(hDriver, code, inBuf, (uint)inBuf.Length, outBuf, (uint)outBuf.Length, out uint bytesReturned, IntPtr.Zero);
                if (success)
                {
                    Console.WriteLine($"[IOCTL 0x{code:X8}] SUCCESS! Returned {bytesReturned} bytes: {BitConverter.ToString(outBuf, 0, (int)bytesReturned)}");
                }
                else
                {
                    int err = Marshal.GetLastWin32Error();
                    if (err != 1 && err != 87) // 1 = ERROR_INVALID_FUNCTION, 87 = ERROR_INVALID_PARAMETER
                    {
                        Console.WriteLine($"[IOCTL 0x{code:X8}] Error: {err}");
                    }
                }
            }
        }
    }
}
