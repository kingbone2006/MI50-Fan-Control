using System;
using System.Reflection;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class DumpAllRegisters
    {
        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" DUMP ALL REGISTERS OF IT8772F AT 0x0A30");
            Console.WriteLine("==================================================");

            var computer = new Computer { IsMotherboardEnabled = true, IsCpuEnabled = true, IsGpuEnabled = true };
            try { computer.Open(); } catch { }

            var asm = typeof(Computer).Assembly;
            var lpcIoType = asm.GetType("LibreHardwareMonitor.PawnIo.LpcIo");
            if (lpcIoType == null) return;

            object? lpc = Activator.CreateInstance(lpcIoType);
            if (lpc == null) return;

            var readPortMethod = lpcIoType.GetMethod("ReadPort", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var writePortMethod = lpcIoType.GetMethod("WritePort", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            byte Read(ushort port) => (byte)(readPortMethod?.Invoke(lpc, new object[] { port }) ?? (byte)0xFF);
            void Write(ushort port, byte val) => writePortMethod?.Invoke(lpc, new object[] { port, val });

            ushort baseAddr = 0x0A30;
            ushort addrPort = (ushort)(baseAddr + 5);
            ushort dataPort = (ushort)(baseAddr + 6);

            byte ReadReg(byte reg)
            {
                Write(addrPort, reg);
                return Read(dataPort);
            }

            Console.WriteLine("Offset  00 01 02 03 04 05 06 07  08 09 0A 0B 0C 0D 0E 0F");
            Console.WriteLine("---------------------------------------------------------");

            for (int row = 0; row < 16; row++)
            {
                Console.Write($"  {row * 16:X2}:   ");
                for (int col = 0; col < 16; col++)
                {
                    byte reg = (byte)(row * 16 + col);
                    byte val = ReadReg(reg);
                    Console.Write($"{val:X2} ");
                    if (col == 7) Console.Write(" ");
                }
                Console.WriteLine();
            }

            try { computer.Close(); } catch { }
        }
    }
}
