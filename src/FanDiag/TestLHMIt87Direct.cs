using System;
using System.Reflection;
using LibreHardwareMonitor.Hardware;

namespace FanDiag
{
    public class TestLHMIt87Direct
    {
        public static void Run()
        {
            Console.WriteLine("==================================================");
            Console.WriteLine(" TEST DIRECT LIBREHARDWAREMONITOR IT87XX OBJECT");
            Console.WriteLine("==================================================");

            var computer = new Computer { IsMotherboardEnabled = true, IsCpuEnabled = true, IsGpuEnabled = true };
            try { computer.Open(); } catch { }

            var asm = typeof(Computer).Assembly;
            var it87xxType = asm.GetType("LibreHardwareMonitor.Hardware.Motherboard.Lpc.IT87XX");
            var lpcPortType = asm.GetType("LibreHardwareMonitor.Hardware.Motherboard.Lpc.LpcPort");
            var chipType = asm.GetType("LibreHardwareMonitor.Hardware.Motherboard.Lpc.Chip");

            if (it87xxType == null || lpcPortType == null || chipType == null)
            {
                Console.WriteLine("Missing types");
                return;
            }

            object chipVal = Enum.Parse(chipType, "IT8772F");
            object lpcPort = Activator.CreateInstance(lpcPortType, new object[] { (ushort)0x2E, (ushort)0x2F })!;

            // ctor(LpcPort port, Chip chip, UInt16 address, UInt16 gpioAddress, Byte version, Motherboard motherboard, IGigabyteController gigabyteController)
            object it87Obj = Activator.CreateInstance(it87xxType, new object?[] { lpcPort, chipVal, (ushort)0x0A30, (ushort)0, (byte)0, null, null })!;

            Console.WriteLine($"Successfully instantiated IT87XX at 0x0A30!");

            var updateMethod = it87xxType.GetMethod("Update", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var fansProp = it87xxType.GetProperty("Fans", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var controlsProp = it87xxType.GetProperty("Controls", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
            var setControlMethod = it87xxType.GetMethod("SetControl", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);

            updateMethod?.Invoke(it87Obj, null);

            var fans = (float?[])fansProp!.GetValue(it87Obj)!;
            var controls = (float?[])controlsProp!.GetValue(it87Obj)!;

            for (int i = 0; i < fans.Length; i++)
            {
                Console.WriteLine($"  Fan [{i}]: {fans[i]} RPM");
            }
            for (int i = 0; i < controls.Length; i++)
            {
                Console.WriteLine($"  Control [{i}]: {controls[i]} %");
            }

            Console.WriteLine("\nSetting Control 0, 1, 2 to 70%...");
            setControlMethod?.Invoke(it87Obj, new object?[] { 0, 70f });
            setControlMethod?.Invoke(it87Obj, new object?[] { 1, 70f });
            setControlMethod?.Invoke(it87Obj, new object?[] { 2, 70f });

            updateMethod?.Invoke(it87Obj, null);
            Console.WriteLine("Done setting 70% via LibreHardwareMonitor IT87XX!");

            try { computer.Close(); } catch { }
        }
    }
}
