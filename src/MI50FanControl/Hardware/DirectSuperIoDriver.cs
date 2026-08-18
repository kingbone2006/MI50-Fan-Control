using System;
using System.Collections.Generic;
using System.Reflection;
using MI50FanControl.Services;

namespace MI50FanControl.Hardware
{
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
        private static readonly ushort[] CandidateBases = { 0x0A30, 0x0290, 0x0A00, 0x0A10, 0x0A20, 0x0A40, 0x0A50 };

        private ushort _baseAddress = 0x0A30;
        private string _chipName = "ITE IT8772E / IT8772F";
        private bool _isDetected = false;

        private object? _lpc;
        private MethodInfo? _readPortMethod;
        private MethodInfo? _writePortMethod;

        private readonly List<DirectFanChannel> _fans = new();

        public string ChipName => _chipName;
        public ushort BaseAddress => _baseAddress;
        public bool IsDetected => _isDetected;
        public IReadOnlyList<DirectFanChannel> Fans => _fans;

        public bool Initialize()
        {
            LogService.Instance.Hardware("DirectSuperIO", "Khởi tạo kết nối phần cứng ITE IT8772E/F...");

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
                LogService.Instance.Error("DirectSuperIO", $"Lỗi tạo LpcIo: {ex.Message}");
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

            _baseAddress = 0x0A30;
            _chipName = "ITE IT8772E / IT8772F";
            _isDetected = true;

            // Enable Monitoring & Tachometers
            WriteRegister(0x00, 0x01); // Start Monitor
            WriteRegister(0x0C, 0x1F); // Enable Fan Tachometers 1..5

            LogService.Instance.Success("DirectSuperIO", $"Đã kích hoạt chip phần cứng: '{_chipName}' tại Base 0x{_baseAddress:X4}");
            DiscoverFans();
            return true;
        }

        private void DiscoverFans()
        {
            _fans.Clear();

            string[] fanNames = { "Fan #1 (MI50 / CPU)", "Fan #2 (System / Chassis)", "Fan #3 (Auxiliary)" };

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
                LogService.Instance.Hardware("DirectFan", $"Cổng quạt: '{fan.Name}' = {rpm} RPM (Reg 0x{pwmReg:X2} = 0x{originalPwm:X2})");
            }
        }

        public void UpdateTelemetry()
        {
            if (!_isDetected) return;

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

        public bool SetFanPwm(int channelIndex, float percent)
        {
            if (!_isDetected) return false;

            try
            {
                float clamped = Math.Clamp(percent, 0f, 100f);

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

                LogService.Instance.Debug("DirectPWM", $"Ghi PWM Fan #{channelIndex + 1} -> {clamped:F0}% (Reg 0x{pwmReg:X2}=0x{pwmVal7Bit:X2}, Reg 0x{dutyReg:X2}={pwmDuty8Bit})");
                return true;
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
            catch { }
        }

        public void Dispose()
        {
            RestoreBiosDefault();
        }
    }
}
