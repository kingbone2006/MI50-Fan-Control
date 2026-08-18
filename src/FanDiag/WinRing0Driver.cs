using System;
using System.IO;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FanDiag
{
    public class WinRing0Driver : IDisposable
    {
        private const uint GENERIC_READ = 0x80000000;
        private const uint GENERIC_WRITE = 0x40000000;
        private const uint OPEN_EXISTING = 3;
        private const uint FILE_ATTRIBUTE_NORMAL = 0x80;

        private const uint IOCTL_READ_IO_PORT_BYTE = 0x9C4060C4;
        private const uint IOCTL_WRITE_IO_PORT_BYTE = 0x9C4060C8;

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

        private SafeFileHandle? _handle;
        public bool IsOpen => _handle != null && !_handle.IsInvalid;

        public bool Open()
        {
            string[] driverNames = { "\\\\.\\WinRing0_1_2_0", "\\\\.\\WinRing0x64", "\\\\.\\WinRing0" };
            foreach (var name in driverNames)
            {
                _handle = CreateFile(name, GENERIC_READ | GENERIC_WRITE, 0, IntPtr.Zero, OPEN_EXISTING, FILE_ATTRIBUTE_NORMAL, IntPtr.Zero);
                if (IsOpen)
                {
                    Console.WriteLine($"WinRing0 Driver opened successfully: '{name}'");
                    return true;
                }
            }
            return false;
        }

        public byte ReadIoPort(ushort port)
        {
            if (!IsOpen) return 0xFF;
            byte[] inBuffer = BitConverter.GetBytes((uint)port);
            byte[] outBuffer = new byte[4];
            DeviceIoControl(_handle!, IOCTL_READ_IO_PORT_BYTE, inBuffer, (uint)inBuffer.Length, outBuffer, (uint)outBuffer.Length, out _, IntPtr.Zero);
            return outBuffer[0];
        }

        public void WriteIoPort(ushort port, byte value)
        {
            if (!IsOpen) return;
            byte[] inBuffer = new byte[8];
            Array.Copy(BitConverter.GetBytes((uint)port), 0, inBuffer, 0, 4);
            inBuffer[4] = value;
            DeviceIoControl(_handle!, IOCTL_WRITE_IO_PORT_BYTE, inBuffer, (uint)inBuffer.Length, null, 0, out _, IntPtr.Zero);
        }

        public void Dispose()
        {
            _handle?.Dispose();
            _handle = null;
        }
    }
}
