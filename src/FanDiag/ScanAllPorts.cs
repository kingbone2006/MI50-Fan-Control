using System;
using System.Reflection;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class ScanAllPorts
    {
        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" SCANNING WITH COMPUTER.OPEN() + LPCIO");
            Console.WriteLine("==================================================");

            var computer = new Computer
            {
                IsMotherboardEnabled = true,
                IsCpuEnabled = true,
                IsGpuEnabled = true
            };
            try { computer.Open(); } catch (Exception ex) { Console.WriteLine($"computer.Open() error: {ex.Message}"); }

            var asm = typeof(Computer).Assembly;
            var lpcIoType = asm.GetType("LibreHardwareMonitor.PawnIo.LpcIo");
            if (lpcIoType == null) return;

            object? lpc = Activator.CreateInstance(lpcIoType);
            if (lpc == null) return;

            var readPortMethod = lpcIoType.GetMethod("ReadPort", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var writePortMethod = lpcIoType.GetMethod("WritePort", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var closeMethod = lpcIoType.GetMethod("Close", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            byte Read(ushort port) => (byte)(readPortMethod?.Invoke(lpc, new object[] { port }) ?? (byte)0xFF);
            void Write(ushort port, byte val) => writePortMethod?.Invoke(lpc, new object[] { port, val });

            // Probing Candidate ISA Bases
            ushort[] candidateBases = { 0x0A30, 0x0290, 0x0A00, 0x0A10, 0x0A20, 0x0A40, 0x0A50, 0x0680, 0x0280 };
            Console.WriteLine("\n--- Probing Candidate ISA Bases ---");
            foreach (var b in candidateBases)
            {
                ushort aP = (ushort)(b + 5);
                ushort dP = (ushort)(b + 6);

                Write(aP, 0x58);
                byte vId = Read(dP);
                Write(aP, 0x5B);
                byte c1 = Read(dP);
                Write(aP, 0x5C);
                byte c2 = Read(dP);

                Write(aP, 0x00);
                byte cfg = Read(dP);

                Console.WriteLine($"  Base 0x{b:X4}: Vendor=0x{vId:X2}, ChipID=0x{c1:X2} 0x{c2:X2}, Cfg=0x{cfg:X2}");
            }

            closeMethod?.Invoke(lpc, null);
            try { computer.Close(); } catch { }
        }
    }
}
