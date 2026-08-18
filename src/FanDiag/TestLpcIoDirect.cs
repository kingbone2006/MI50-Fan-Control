using System;
using System.Reflection;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class TestLpcIoDirect
    {
        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" TEST LPCIO DIRECT ACCESS VIA REFLECTION");
            Console.WriteLine("==================================================");

            var asm = typeof(Computer).Assembly;
            var lpcIoType = asm.GetType("LibreHardwareMonitor.PawnIo.LpcIo");

            if (lpcIoType == null)
            {
                Console.WriteLine("LpcIo type not found");
                return;
            }

            object? lpc = Activator.CreateInstance(lpcIoType);
            if (lpc == null)
            {
                Console.WriteLine("Could not instantiate LpcIo");
                return;
            }

            var readPortMethod = lpcIoType.GetMethod("ReadPort", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var writePortMethod = lpcIoType.GetMethod("WritePort", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var closeMethod = lpcIoType.GetMethod("Close", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            Console.WriteLine($"Methods: Read={readPortMethod != null}, Write={writePortMethod != null}");

            ushort baseAddr = 0x0A30;
            ushort addrPort = (ushort)(baseAddr + 5);
            ushort dataPort = (ushort)(baseAddr + 6);

            writePortMethod?.Invoke(lpc, new object[] { addrPort, (byte)0x58 });
            byte vId = (byte)(readPortMethod?.Invoke(lpc, new object[] { dataPort }) ?? (byte)0);

            writePortMethod?.Invoke(lpc, new object[] { addrPort, (byte)0x5B });
            byte c1 = (byte)(readPortMethod?.Invoke(lpc, new object[] { dataPort }) ?? (byte)0);

            writePortMethod?.Invoke(lpc, new object[] { addrPort, (byte)0x5C });
            byte c2 = (byte)(readPortMethod?.Invoke(lpc, new object[] { dataPort }) ?? (byte)0);

            Console.WriteLine($"Vendor ID: 0x{vId:X2}, Chip ID: 0x{c1:X2} 0x{c2:X2}");

            closeMethod?.Invoke(lpc, null);
        }
    }
}
