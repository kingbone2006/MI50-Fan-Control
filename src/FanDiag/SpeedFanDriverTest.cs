using System;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Runtime.InteropServices;
using Microsoft.Win32.SafeHandles;

namespace FanDiag
{
    public class SpeedFanDriverTest
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
            Console.WriteLine(" SPEEDFAN DRIVER & SHARED MEMORY TEST");
            Console.WriteLine("==================================================");

            // 1. Check if SpeedFan Shared Memory is open
            try
            {
                using var mmf = MemoryMappedFile.OpenExisting("SFSharedMemory_ALM", MemoryMappedFileRights.ReadWrite);
                Console.WriteLine("[OK] Successfully connected to 'SFSharedMemory_ALM' Shared Memory!");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Shared Memory 'SFSharedMemory_ALM' not open: {ex.Message}");
            }

            // 2. Check direct connection to \\.\SpeedFan driver device
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

            if (!hDriver.IsInvalid)
            {
                Console.WriteLine("[OK] Successfully opened direct handle to '\\\\.\\SpeedFan' Kernel Driver!");
            }
            else
            {
                int err = Marshal.GetLastWin32Error();
                Console.WriteLine($"Could not open '\\\\.\\SpeedFan' (Win32 Error: {err})");
            }
        }
    }
}
