using System;
using System.Runtime.InteropServices;

namespace FanDiag
{
    public class DumpSpeedfanMemory
    {
        [DllImport("kernel32.dll", SetLastError = true, CharSet = CharSet.Auto)]
        private static extern IntPtr OpenFileMapping(uint dwDesiredAccess, bool bInheritHandle, string lpName);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr MapViewOfFile(IntPtr hFileMappingObject, uint dwDesiredAccess, uint dwFileOffsetHigh, uint dwFileOffsetLow, UIntPtr dwNumberOfBytesToMap);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool UnmapViewOfFile(IntPtr lpBaseAddress);

        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool CloseHandle(IntPtr hObject);

        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" DUMP RAW SFSHAREDMEMORY_ALM BUFFER");
            Console.WriteLine("==================================================");

            IntPtr hMap = OpenFileMapping(0x0004, false, "SFSharedMemory_ALM");
            if (hMap == IntPtr.Zero)
            {
                Console.WriteLine("Could not open SFSharedMemory_ALM");
                return;
            }

            IntPtr pView = MapViewOfFile(hMap, 0x0004, 0, 0, UIntPtr.Zero);
            if (pView != IntPtr.Zero)
            {
                byte[] raw = new byte[512];
                Marshal.Copy(pView, raw, 0, 512);

                Console.WriteLine("Offset  00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F");
                Console.WriteLine("---------------------------------------------------------");
                for (int row = 0; row < 32; row++)
                {
                    Console.Write($"  {row * 16:X3}:  ");
                    for (int col = 0; col < 16; col++)
                    {
                        Console.Write($"{raw[row * 16 + col]:X2} ");
                        if (col == 7) Console.Write(" ");
                    }
                    Console.WriteLine();
                }

                UnmapViewOfFile(pView);
            }
            CloseHandle(hMap);
        }
    }
}
