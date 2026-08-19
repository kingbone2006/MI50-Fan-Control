using System;
using System.Collections.Generic;
using System.Reflection;
using MI50FanControl.Services;

namespace MI50FanControl.Hardware
{
    public enum SuperIoVendor
    {
        Unknown,
        ITE,
        Nuvoton,
        Fintek,
        Winbond
    }

    public class DirectFanChannel
    {
        public int ChannelIndex { get; set; }
        public string Name { get; set; } = string.Empty;
        public float LiveRpm { get; set; }
        public float CurrentPwmPercent { get; set; }
        public byte OriginalPwmRegValue { get; set; }
        public byte OriginalControlMode { get; set; }
        public bool IsCustomControlActive { get; set; }
    }

    public class DirectSuperIoDriver : IDisposable
    {
        private ushort _configPort = 0x2E;
        private ushort _dataPort = 0x2F;
        private ushort _baseAddress = 0x0A30;
        private string _chipName = string.Empty;
        private SuperIoVendor _vendor = SuperIoVendor.Unknown;
        private bool _isDetected = false;

        private object? _lpc;
        private MethodInfo? _readPortMethod;
        private MethodInfo? _writePortMethod;

        private readonly List<DirectFanChannel> _fans = new();

        public string ChipName => _chipName;
        public SuperIoVendor Vendor => _vendor;
        public ushort BaseAddress => _baseAddress;
        public bool IsDetected => _isDetected;
        public IReadOnlyList<DirectFanChannel> Fans => _fans;

        public bool Initialize()
        {
            LogService.Instance.Hardware("DirectSuperIO", "Khởi tạo kết nối LPC Direct I/O Driver...");

            try
            {
                var asm = typeof(LibreHardwareMonitor.Hardware.Computer).Assembly;
                var lpcIoType = asm.GetType("LibreHardwareMonitor.PawnIo.LpcIo");

                if (lpcIoType != null)
                {
                    _lpc = Activator.CreateInstance(lpcIoType);
                    _readPortMethod = lpcIoType.GetMethod("ReadPort", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                    _writePortMethod = lpcIoType.GetMethod("WritePort", BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance);
                }
            }
            catch (Exception ex)
            {
                LogService.Instance.Warn("DirectSuperIO", $"Lỗi nạp LpcIo Ring0: {ex.Message}");
            }

            return ProbeHardware();
        }

        public byte ReadPort(ushort port)
        {
            if (_lpc == null || _readPortMethod == null) return 0xFF;
            try
            {
                var result = _readPortMethod.Invoke(_lpc, new object[] { port });
                return result is byte b ? b : (byte)0xFF;
            }
            catch
            {
                return 0xFF;
            }
        }

        public void WritePort(ushort port, byte value)
        {
            if (_lpc == null || _writePortMethod == null) return;
            try
            {
                _writePortMethod.Invoke(_lpc, new object[] { port, value });
            }
            catch { }
        }

        public byte ReadRegister(byte reg)
        {
            ushort addrPort = (ushort)(_baseAddress + 5);
            ushort dataPort = (ushort)(_baseAddress + 6);
            WritePort(addrPort, reg);
            return ReadPort(dataPort);
        }

        public void WriteRegister(byte reg, byte value)
        {
            ushort addrPort = (ushort)(_baseAddress + 5);
            ushort dataPort = (ushort)(_baseAddress + 6);
            WritePort(addrPort, reg);
            WritePort(dataPort, value);
        }

        public bool ProbeHardware()
        {
            _fans.Clear();
            _isDetected = false;
            _chipName = string.Empty;
            _vendor = SuperIoVendor.Unknown;

            // Quét tự động trên cả 2 cổng LPC chuẩn: 0x2E/0x2F và 0x4E/0x4F
            ushort[] ports = { 0x2E, 0x4E };

            foreach (var cfgPort in ports)
            {
                ushort datPort = (ushort)(cfgPort + 1);

                // 1. Thử nhận diện chip họ ITE
                if (ProbeIte(cfgPort, datPort))
                {
                    _configPort = cfgPort;
                    _dataPort = datPort;
                    _vendor = SuperIoVendor.ITE;
                    _isDetected = true;
                    LogService.Instance.Success("DirectSuperIO", $"[Dynamic Probe] Phát hiện chip ITE: '{_chipName}' tại Cổng 0x{_configPort:X2}, Base 0x{_baseAddress:X4}");
                    DiscoverIteFans();
                    return true;
                }

                // 2. Thử nhận diện chip họ Nuvoton / Winbond
                if (ProbeNuvoton(cfgPort, datPort))
                {
                    _configPort = cfgPort;
                    _dataPort = datPort;
                    _isDetected = true;
                    LogService.Instance.Success("DirectSuperIO", $"[Dynamic Probe] Phát hiện chip: '{_chipName}' tại Cổng 0x{_configPort:X2}, Base 0x{_baseAddress:X4}");
                    DiscoverNuvotonFans();
                    return true;
                }

                // 3. Thử nhận diện chip họ Fintek
                if (ProbeFintek(cfgPort, datPort))
                {
                    _configPort = cfgPort;
                    _dataPort = datPort;
                    _vendor = SuperIoVendor.Fintek;
                    _isDetected = true;
                    LogService.Instance.Success("DirectSuperIO", $"[Dynamic Probe] Phát hiện chip Fintek: '{_chipName}' tại Cổng 0x{_configPort:X2}, Base 0x{_baseAddress:X4}");
                    DiscoverFintekFans();
                    return true;
                }
            }

            LogService.Instance.Info("DirectSuperIO", "Không tìm thấy chip SuperIO qua LPC Ports trực tiếp (Chuyển sang chế độ đa tầng LibreHardwareMonitor & Argus Engine).");
            return false;
        }

        private bool ProbeIte(ushort cfg, ushort dat)
        {
            try
            {
                // Sequence mở khoá ITE
                WritePort(cfg, 0x87);
                WritePort(cfg, 0x01);
                WritePort(cfg, 0x55);
                WritePort(cfg, (byte)(cfg == 0x4E ? 0xAA : 0x55));

                // Đọc Chip ID High & Low
                WritePort(cfg, 0x20);
                byte id1 = ReadPort(dat);
                WritePort(cfg, 0x21);
                byte id2 = ReadPort(dat);

                ushort chipId = (ushort)((id1 << 8) | id2);

                if (id1 == 0x87 || id1 == 0x86)
                {
                    _chipName = MapIteChipName(chipId);

                    // Đọc Base Address từ Logical Device 4 (Hardware Monitor)
                    WritePort(cfg, 0x07);
                    WritePort(dat, 0x04);
                    WritePort(cfg, 0x60);
                    byte baseMsb = ReadPort(dat);
                    WritePort(cfg, 0x61);
                    byte baseLsb = ReadPort(dat);

                    ushort baseAddr = (ushort)((baseMsb << 8) | baseLsb);
                    _baseAddress = (baseAddr != 0 && baseAddr != 0xFFFF) ? baseAddr : (ushort)0x0A30;

                    // Đóng cấu hình ITE
                    WritePort(cfg, 0x02);
                    WritePort(dat, 0x02);

                    return true;
                }

                // Đóng cấu hình ITE
                WritePort(cfg, 0x02);
                WritePort(dat, 0x02);
            }
            catch { }

            return false;
        }

        private static string MapIteChipName(ushort chipId)
        {
            return chipId switch
            {
                0x8772 => "ITE IT8772E / IT8772F",
                0x8686 => "ITE IT8686E",
                0x8688 => "ITE IT8688E",
                0x8689 => "ITE IT8689E",
                0x8620 => "ITE IT8620E",
                0x8628 => "ITE IT8628E",
                0x8625 => "ITE IT8625E",
                0x8655 => "ITE IT8655E",
                0x8665 => "ITE IT8665E",
                0x8728 => "ITE IT8728F",
                0x8786 => "ITE IT8786E",
                0x8790 => "ITE IT8790E",
                0x8792 => "ITE IT8792E",
                0x8732 => "ITE IT8732F",
                0x8718 => "ITE IT8718F",
                0x8720 => "ITE IT8720F",
                0x8721 => "ITE IT8721F",
                0x8705 => "ITE IT8705F",
                0x8712 => "ITE IT8712F",
                0x8716 => "ITE IT8716F",
                _ => $"ITE IT{chipId:X4}E/F"
            };
        }

        private bool ProbeNuvoton(ushort cfg, ushort dat)
        {
            try
            {
                // Sequence mở khoá Nuvoton / Winbond
                WritePort(cfg, 0x87);
                WritePort(cfg, 0x87);

                // Đọc Device ID & Revision ID
                WritePort(cfg, 0x20);
                byte devId = ReadPort(dat);
                WritePort(cfg, 0x21);
                byte revId = ReadPort(dat);

                string name = MapNuvotonChipName(devId, revId, out var vendor);
                if (!string.IsNullOrEmpty(name))
                {
                    _chipName = name;
                    _vendor = vendor;

                    // Đọc Base Address từ Logical Device 0x0B hoặc 0x08 (Hardware Monitor)
                    WritePort(cfg, 0x07);
                    WritePort(dat, 0x0B);
                    WritePort(cfg, 0x60);
                    byte baseMsb = ReadPort(dat);
                    WritePort(cfg, 0x61);
                    byte baseLsb = ReadPort(dat);

                    ushort baseAddr = (ushort)((baseMsb << 8) | baseLsb);
                    if (baseAddr == 0 || baseAddr == 0xFFFF)
                    {
                        WritePort(cfg, 0x07);
                        WritePort(dat, 0x08);
                        WritePort(cfg, 0x60);
                        baseMsb = ReadPort(dat);
                        WritePort(cfg, 0x61);
                        baseLsb = ReadPort(dat);
                        baseAddr = (ushort)((baseMsb << 8) | baseLsb);
                    }

                    _baseAddress = (baseAddr != 0 && baseAddr != 0xFFFF) ? baseAddr : (ushort)0x0290;

                    // Thoát cấu hình
                    WritePort(cfg, 0xAA);
                    return true;
                }

                WritePort(cfg, 0xAA);
            }
            catch { }

            return false;
        }

        private static string MapNuvotonChipName(byte devId, byte revId, out SuperIoVendor vendor)
        {
            vendor = SuperIoVendor.Nuvoton;
            switch (devId)
            {
                case 0xC5:
                    return revId >= 0x60 ? "Nuvoton NCT6791D" : "Nuvoton NCT6776F";
                case 0xC8:
                    return "Nuvoton NCT6779D";
                case 0xD1:
                    return "Nuvoton NCT6792D";
                case 0xD3:
                    return "Nuvoton NCT6793D";
                case 0xD4:
                    int revHigh = revId & 0xF0;
                    return revHigh switch
                    {
                        0x10 => "Nuvoton NCT6795D",
                        0x20 => "Nuvoton NCT6796D",
                        0x40 => "Nuvoton NCT6797D",
                        0x50 => "Nuvoton NCT6798D",
                        0x80 => "Nuvoton NCT6799D",
                        _ => $"Nuvoton NCT679xD (Rev 0x{revId:X2})"
                    };
                case 0xC7:
                    return "Nuvoton NCT6683D";
                case 0xC9:
                    return "Nuvoton NCT6686D";
                case 0xD5:
                    return "Nuvoton NCT6687D";
                case 0xB4:
                    return "Nuvoton NCT5532D";
                case 0xD2:
                    return "Nuvoton NCT5577D";
                case 0xA0:
                    vendor = SuperIoVendor.Winbond;
                    return "Winbond W83667HG";
                case 0xB0:
                    vendor = SuperIoVendor.Winbond;
                    return "Winbond W83627DHG";
                case 0xB3:
                    vendor = SuperIoVendor.Winbond;
                    return "Winbond W83677HG";
                default:
                    vendor = SuperIoVendor.Unknown;
                    return string.Empty;
            }
        }

        private bool ProbeFintek(ushort cfg, ushort dat)
        {
            try
            {
                WritePort(cfg, 0x87);
                WritePort(cfg, 0x87);

                WritePort(cfg, 0x23);
                byte v1 = ReadPort(dat);
                WritePort(cfg, 0x24);
                byte v2 = ReadPort(dat);

                if (v1 == 0x19 && v2 == 0x34)
                {
                    WritePort(cfg, 0x20);
                    byte id1 = ReadPort(dat);
                    WritePort(cfg, 0x21);
                    byte id2 = ReadPort(dat);

                    ushort chipId = (ushort)((id1 << 8) | id2);
                    _chipName = chipId switch
                    {
                        0x0507 => "Fintek F71808A",
                        0x0601 => "Fintek F71862",
                        0x0849 => "Fintek F71869",
                        0x0901 => "Fintek F71882",
                        0x0723 => "Fintek F71889AD",
                        0x1005 => "Fintek F71889ED",
                        0x1007 => "Fintek F71889F",
                        0x1008 => "Fintek F71878AD",
                        _ => $"Fintek F{chipId:X4}"
                    };

                    _baseAddress = 0x0290;
                    WritePort(cfg, 0xAA);
                    return true;
                }

                WritePort(cfg, 0xAA);
            }
            catch { }

            return false;
        }

        private void DiscoverIteFans()
        {
            _fans.Clear();

            // Kích hoạt Monitoring & Tachometers
            WriteRegister(0x00, 0x01); // Start Monitor
            WriteRegister(0x0C, 0x1F); // Enable Fan Tachometers 1..5

            string[] fanNames = { "Fan #1 (MI50 / CPU)", "Fan #2 (System / Chassis)", "Fan #3 (Auxiliary / SYS)", "Fan #4 (Optional)", "Fan #5 (Optional)" };

            for (int f = 0; f < 3; f++)
            {
                byte lsbReg = (byte)(0x0D + f);
                byte msbReg = (byte)(0x18 + f);

                byte lsb = ReadRegister(lsbReg);
                byte msb = ReadRegister(msbReg);

                int count = (msb << 8) | lsb;
                int rpm = count > 0 && count < 0xFFFF ? (int)(1350000.0 / (count * 2)) : 0;

                byte pwmReg = (byte)(0x15 + f);
                byte originalPwm = ReadRegister(pwmReg);
                byte originalCtrl = ReadRegister(0x13);

                var fan = new DirectFanChannel
                {
                    ChannelIndex = f,
                    Name = fanNames[f],
                    LiveRpm = rpm,
                    OriginalPwmRegValue = originalPwm,
                    OriginalControlMode = originalCtrl,
                    CurrentPwmPercent = (float)Math.Round(((originalPwm & 0x7F) / 127.0) * 100)
                };

                _fans.Add(fan);
                LogService.Instance.Hardware("DirectFan", $"Cổng quạt ITE: '{fan.Name}' = {rpm} RPM (Reg 0x{pwmReg:X2} = 0x{originalPwm:X2})");
            }
        }

        private void DiscoverNuvotonFans()
        {
            _fans.Clear();
            string[] fanNames = { "Fan #1 (CPU_FAN)", "Fan #2 (SYS_FAN1)", "Fan #3 (SYS_FAN2)", "Fan #4 (CHA_FAN)", "Fan #5 (AUX_FAN)" };

            for (int f = 0; f < 3; f++)
            {
                var fan = new DirectFanChannel
                {
                    ChannelIndex = f,
                    Name = fanNames[f],
                    LiveRpm = 0,
                    OriginalPwmRegValue = 0x80,
                    OriginalControlMode = 0,
                    CurrentPwmPercent = 50
                };
                _fans.Add(fan);
            }
        }

        private void DiscoverFintekFans()
        {
            _fans.Clear();
            string[] fanNames = { "Fan #1 (CPU_FAN)", "Fan #2 (SYS_FAN)", "Fan #3 (AUX_FAN)" };
            for (int f = 0; f < 3; f++)
            {
                var fan = new DirectFanChannel
                {
                    ChannelIndex = f,
                    Name = fanNames[f],
                    LiveRpm = 0,
                    OriginalPwmRegValue = 0x80,
                    OriginalControlMode = 0,
                    CurrentPwmPercent = 50
                };
                _fans.Add(fan);
            }
        }

        public void UpdateTelemetry()
        {
            if (!_isDetected) return;

            if (_vendor == SuperIoVendor.ITE)
            {
                foreach (var fan in _fans)
                {
                    int f = fan.ChannelIndex;
                    byte lsbReg = (byte)(0x0D + f);
                    byte msbReg = (byte)(0x18 + f);

                    byte lsb = ReadRegister(lsbReg);
                    byte msb = ReadRegister(msbReg);

                    int count = (msb << 8) | lsb;
                    fan.LiveRpm = count > 0 && count < 0xFFFF ? (int)(1350000.0 / (count * 2)) : 0;

                    byte pwmReg = (byte)(0x15 + f);
                    byte pwmVal = ReadRegister(pwmReg);
                    fan.CurrentPwmPercent = (float)Math.Round(((pwmVal & 0x7F) / 127.0) * 100);
                }
            }
        }

        public bool SetFanPwm(int channelIndex, float percent)
        {
            if (!_isDetected) return false;

            try
            {
                float clamped = Math.Clamp(percent, 0f, 100f);

                if (_vendor == SuperIoVendor.ITE)
                {
                    // 1. Linux it87 kernel & SpeedFan manual mode (Reg 0x13 bit = 0)
                    byte ctrlMode = ReadRegister(0x13);
                    byte newCtrlMode = (byte)(ctrlMode & ~(1 << channelIndex));
                    WriteRegister(0x13, newCtrlMode);

                    // 2. 7-bit direct mode with Bit 7 (0x80)
                    byte pwmDuty7Bit = (byte)Math.Round((clamped / 100.0) * 127);
                    byte pwmVal7Bit = (byte)(pwmDuty7Bit | 0x80);
                    byte pwmReg = (byte)(0x15 + channelIndex);
                    WriteRegister(pwmReg, pwmVal7Bit);

                    // 3. 8-bit duty cycle register (Reg 0x63, 0x6B, 0x73)
                    byte pwmDuty8Bit = (byte)Math.Round((clamped / 100.0) * 255);
                    byte dutyReg = (byte)(0x63 + channelIndex * 8);
                    WriteRegister(dutyReg, pwmDuty8Bit);

                    var fan = _fans.Find(f => f.ChannelIndex == channelIndex);
                    if (fan != null)
                    {
                        fan.CurrentPwmPercent = clamped;
                        fan.IsCustomControlActive = true;
                    }

                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                LogService.Instance.Error("DirectPWM", $"Lỗi ghi PWM channel {channelIndex}: {ex.Message}");
                return false;
            }
        }

        public void SetAllFansPwm(float percent)
        {
            if (!_isDetected) return;
            foreach (var fan in _fans)
            {
                SetFanPwm(fan.ChannelIndex, percent);
            }
        }

        public void RestoreBiosDefault(int? channelIndex = null)
        {
            if (!_isDetected) return;

            try
            {
                if (_vendor == SuperIoVendor.ITE)
                {
                    if (channelIndex.HasValue)
                    {
                        byte ctrlMode = ReadRegister(0x13);
                        byte newCtrlMode = (byte)(ctrlMode | (1 << channelIndex.Value));
                        WriteRegister(0x13, newCtrlMode);

                        var fan = _fans.Find(f => f.ChannelIndex == channelIndex.Value);
                        if (fan != null)
                        {
                            WriteRegister((byte)(0x15 + channelIndex.Value), fan.OriginalPwmRegValue);
                            fan.IsCustomControlActive = false;
                        }
                    }
                    else
                    {
                        foreach (var fan in _fans)
                        {
                            byte ctrlMode = ReadRegister(0x13);
                            byte newCtrlMode = (byte)(ctrlMode | (1 << fan.ChannelIndex));
                            WriteRegister(0x13, newCtrlMode);
                            WriteRegister((byte)(0x15 + fan.ChannelIndex), fan.OriginalPwmRegValue);
                            fan.IsCustomControlActive = false;
                        }
                        LogService.Instance.Info("DirectSuperIO", "Đã khôi phục toàn bộ cổng quạt về quyền điều khiển tự động của BIOS.");
                    }
                }
            }
            catch { }
        }

        public void Dispose()
        {
            RestoreBiosDefault();
        }
    }
}
