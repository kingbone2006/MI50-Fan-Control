<div align="center">

# 🌀 AMD MI50 / Radeon PRO VII Fan Controller

**Phần mềm điều khiển quạt bo mạch chủ & Giám sát nhiệt độ thời gian thực cho AMD Instinct MI50 / Radeon PRO VII**  
*Motherboard Fan Controller & Real-Time Hardware Telemetry Monitor for AMD Instinct MI50 / Radeon PRO VII*

---

[![GitHub Release](https://img.shields.io/github/v/release/kingbone2006/MI50-Fan-Control?style=for-the-badge&color=0078d4&logo=github)](https://github.com/kingbone2006/MI50-Fan-Control/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(64--bit)-0078d4?style=for-the-badge&logo=windows)](https://github.com/kingbone2006/MI50-Fan-Control)
[![.NET](https://img.shields.io/badge/.NET-8.0%20WPF-512bd4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![Ko-fi](https://img.shields.io/badge/Ko--fi-Support-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/kingbone2006)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)

<br/>

[![Moe Visitor Counter](https://count.getloli.com/@kingbone2006-MI50-Fan-Control)](https://github.com/kingbone2006/MI50-Fan-Control)

<br/>

### 🚀 TẢI XUỐNG BỘ CÀI ĐẶT / DOWNLOAD INSTALLER

[![Direct Download](https://img.shields.io/badge/TẢI_XUỐNG_NGAY_(DIRECT_DOWNLOAD)-MI50FanControl__Setup.exe-brightgreen?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/kingbone2006/MI50-Fan-Control/releases/latest/download/MI50FanControl_Setup.exe)

[📦 **Xem tất cả bản phát hành (All Releases)**](https://github.com/kingbone2006/MI50-Fan-Control/releases)

---

**[🇻🇳 Tiếng Việt](#-tiếng-việt)** | **[🇬🇧 English](#-english)**

</div>

---

# 🇻🇳 Tiếng Việt

## 📖 Giới thiệu
**MI50 Fan Control** là phần mềm chuyên dụng hỗ trợ điều khiển tốc độ quạt tản nhiệt bo mạch chủ gắn ngoài dành cho card đồ họa **AMD Radeon Instinct MI50** (bao gồm các phiên bản mod BIOS **Radeon PRO VII 16GB**).

Phần mềm tích hợp engine **SpeedFan** ngầm để điều khiển trực tiếp tốc độ quạt (PWM) của các chân cắm quạt trên bo mạch chủ, giúp **dễ dàng tương thích và hỗ trợ nhiều loại chip quản lý I/O (SuperIO) trên nhiều bo mạch chủ khác nhau**. Đồng thời, phần mềm kết hợp giao tiếp trực tiếp Driver AMD để hiển thị các thông số phần cứng thiết yếu theo thời gian thực (Nhiệt độ nhân GPU, Nhiệt độ HotSpot, Xung nhịp GPU Clock và Bộ nhớ VRAM).

---

## ✨ Tính năng nổi bật

### 1. 🌡️ Giám sát thông số AMD GPU theo thời gian thực
* Đọc trực tiếp từ Driver AMD:
  * **GPU Core Temperature:** Nhiệt độ nhân GPU thực tế (°C).
  * **GPU HotSpot Temperature:** Nhiệt độ điểm nóng nhất trên khuôn chip GPU (°C).
  * **GPU Engine Clock:** Xung nhịp xử lý GPU (MHz).
  * **GPU VRAM:** Thông tin bộ nhớ đồ họa.
* Cập nhật liên tục theo chu kỳ thời gian thực (~500ms - 1000ms).

### 2. 🌀 Điều khiển quạt bo mạch chủ qua SpeedFan Engine
* Tích hợp engine **SpeedFan** giúp dễ dàng tương thích và hỗ trợ nhiều loại chip quản lý I/O (SuperIO) trên nhiều bo mạch chủ khác nhau.
* **Tự động nhận diện quạt thực tế**: Chỉ hiển thị các cổng quạt đang có quạt cắm và hoạt động (**RPM > 0**).
* Điều chỉnh công suất xung quạt từ **0% đến 100%**.
* Khôi phục quyền điều khiển mặc định khi thoát phần mềm.

### 3. 📈 Tùy chỉnh đường cong quạt thông minh (Fan Curve Editor)
* Tự do chỉnh sửa các mốc nhiệt độ (°C) $\to$ tốc độ quạt (%).
* Tích hợp thuật toán **Làm mượt (Smoothing)** và **Độ trễ nhiệt (Hysteresis)** giúp quạt tăng/giảm tốc êm ái, tránh hiện tượng rú quạt khi nhiệt độ GPU thay đổi ngắn hạn.

### 4. 🛡️ Chế độ bảo vệ quá nhiệt khẩn cấp
* Tự động ép toàn bộ quạt lên **100% công suất** ngay lập tức nếu nhiệt độ GPU vượt ngưỡng giới hạn an toàn người dùng thiết lập.

### 5. 🎛️ Quản lý quạt linh hoạt (Fan Manager)
* Đặt tên tùy chỉnh cho từng cổng quạt (Quạt thổi MI50, Quạt hút case...).
* Tùy chọn chế độ hoạt động riêng cho từng quạt: **Theo đường cong (Curve)**, **Cố định thủ công (Manual)**, hoặc **BIOS mặc định**.
* Cài đặt giới hạn Min/Max PWM an toàn cho quạt.
* Nút **Test 100%** trong 5 giây để kiểm tra quạt tức thì.

### 6. 🌐 Đa ngôn ngữ & Tiện ích hệ thống
* Hỗ trợ **Tiếng Anh** và **Tiếng Việt**, rất mong đón nhận những bản dịch được đóng góp bởi các bạn!
* Chạy ngầm mượt mà tại **Khay hệ thống (System Tray)**.
* Tự động **khởi động cùng Windows**.

---

## 💻 Yêu cầu hệ thống
* **Hệ điều hành:** Windows 10 / Windows 11 (64-bit).
* **Quyền thực thi:** Chạy dưới quyền Quản trị viên (**Administrator**) để nạp driver điều khiển quạt.
* **Môi trường .NET:** [.NET 8.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0).
* **Phần cứng tương thích:** AMD Radeon Instinct MI50 / Radeon PRO VII / Radeon VII / Vega 20.

---

## 📥 Cài đặt & Hướng dẫn sử dụng

### 1. Tải về các thành phần cần thiết
* **Tải .NET 8.0 Desktop Runtime (x64):**
  * 👉 [Tải trực tiếp từ Microsoft (.exe)](https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe)
  * 👉 [Trang chủ tải .NET 8.0](https://dotnet.microsoft.com/download/dotnet/8.0)
* **Tải bộ cài đặt phần mềm:**
  * 👉 [**Tải xuống MI50FanControl_Setup.exe**](https://github.com/kingbone2006/MI50-Fan-Control/releases/latest/download/MI50FanControl_Setup.exe)

### 2. Cài đặt
1. Cài đặt **.NET 8.0 Desktop Runtime** nếu máy chưa có.
2. Chạy file MI50FanControl_Setup.exe với quyền Administrator và bấm **Cài đặt**.
3. Bộ cài đặt sẽ tự động thiết lập và tạo shortcut trên Desktop và Start Menu.

### 3. Hướng dẫn sử dụng
1. Mở ứng dụng MI50 Fan Control từ Desktop.
2. Vào tab **Cài đặt (Settings)**:
   * Chọn cảm biến làm nguồn điều khiển: **GPU HotSpot** (khuyến nghị cho MI50) hoặc **GPU Core**.
   * Bật **Khởi động cùng Windows** và **Thu nhỏ xuống khay hệ thống** nếu muốn ứng dụng chạy ngầm liên tục.
3. Vào tab **Đường cong quạt (Fan Curve)** để tùy chỉnh mốc nhiệt độ mong muốn.
4. Vào tab **Quản lý quạt (Fan Manager)** để đặt tên và gán cấu hình cho từng quạt tản nhiệt.

---

## 🗑️ Hướng dẫn gỡ cài đặt (Uninstallation Guide)

Phần mềm đi kèm file thực thi gỡ cài đặt độc lập **Uninstall.exe** để đảm bảo gỡ bỏ sạch sẽ:

1. **Cách 1 - Gỡ qua Settings / Control Panel:**
   * Mở **Settings** của Windows $\to$ vào mục **Apps** $\to$ **Installed Apps** (hoặc Control Panel $\to$ Programs and Features).
   * Tìm **AMD MI50 Fan Control** và chọn **Uninstall**.
2. **Cách 2 - Chạy trực tiếp file Uninstall.exe:**
   * Truy cập vào thư mục đã cài đặt phần mềm (thường là C:\Program Files\MI50FanControl hoặc thư mục bạn đã chọn khi cài).
   * Chạy file **Uninstall.exe** với quyền Administrator và nhấn **Gỡ cài đặt**.
3. Trình gỡ cài đặt sẽ tự động:
   * Dừng toàn bộ tiến trình ứng dụng đang chạy.
   * Xóa sạch các phím tắt trên Desktop, Start Menu và cấu hình khởi động cùng Windows.
   * Xóa toàn bộ file cài đặt khỏi hệ thống.

---

## ☕ Ủng hộ dự án (Donate)

Nếu bạn thích Project này và thấy nó hữu ích, bạn có thể ủng hộ và mời tôi 1 ly cà phê thông qua mã QR bên dưới:

<div align="center">

<img src="assets/donate_qr.jpg" alt="Mã QR Donate" width="280" style="border-radius: 12px; box-shadow: 0 4px 12px rgba(0,0,0,0.15);" />

<br/>

*Cảm ơn sự đồng hành và ủng hộ của các bạn! ❤️*

</div>

---

<br/>

# 🇬🇧 English

## 📖 Overview
**MI50 Fan Control** is a dedicated external hardware fan controller and telemetry monitoring software designed specifically for **AMD Radeon Instinct MI50** graphics accelerators (including those flashed with modded **Radeon PRO VII 16GB** BIOS).

The application integrates a background **SpeedFan** engine to manage and adjust motherboard fan headers (PWM), **enabling broad and seamless compatibility with various SuperIO hardware controller chips across a wide range of motherboards**. This is combined with direct native AMD driver telemetry to monitor essential real-time hardware statistics (GPU Core Temperature, GPU HotSpot Temperature, GPU Engine Clock, and VRAM memory information).

---

## ✨ Key Features

### 1. 🌡️ Real-Time AMD GPU Telemetry
* Direct AMD Driver polling:
  * **GPU Core Temperature:** Actual GPU die temperature (°C).
  * **GPU HotSpot Temperature:** Highest hot-spot sensor reading (°C).
  * **GPU Engine Clock:** GPU core clock frequency (MHz).
  * **GPU VRAM:** Video memory telemetry.
* Real-time polling cycle (~500ms - 1000ms).

### 2. 🌀 Motherboard Fan Speed Control via SpeedFan Engine
* Built-in **SpeedFan** engine integration provides broad compatibility across various SuperIO hardware controller chipsets on multiple motherboard models.
* **Active Fan Detection**: Filters and displays only fan headers with physical fans spinning (**Live RPM > 0**).
* Full range hardware PWM speed adjustment from **0% to 100%**.
* Restores default hardware fan profiles upon application exit.

### 3. 📈 Intelligent Fan Curve Editor
* Multi-point temperature-to-speed curve mapping.
* Integrated **Smoothing** and **Hysteresis** algorithms to eliminate sudden fan noise oscillations during short-lived temperature spikes.

### 4. 🛡️ Emergency Overheat Protection
* Automatically ramps all fans to **100% full speed** immediately when GPU temperature crosses the critical safety threshold.

### 5. 🎛️ Comprehensive Fan Manager
* Custom naming for individual fan headers.
* Per-fan control modes: **Curve Profile**, **Manual Override**, or **BIOS Default**.
* Min / Max PWM speed limits for hardware safety.
* **5-Second 100% Test** button for quick physical fan verification.

### 6. 🌐 Multilingual & System Utilities
* Supports **English** and **Vietnamese**. Community translations and contributions are warmly welcomed!
* Seamless **System Tray** background operation.
* Automatic **Windows Startup** integration.

---

## 💻 System Requirements
* **OS:** Windows 10 / Windows 11 (64-bit).
* **Privileges:** Administrator privileges required.
* **Runtime:** [.NET 8.0 Desktop Runtime (x64)](https://dotnet.microsoft.com/download/dotnet/8.0).
* **Supported Hardware:** AMD Radeon Instinct MI50, Radeon PRO VII, Radeon VII, and Vega 20 series GPUs.

---

## 📥 Download & Installation

### 1. Download Prerequisites & Installer
* **Download .NET 8.0 Desktop Runtime (x64):**
  * 👉 [Direct Download from Microsoft (.exe)](https://aka.ms/dotnet/8.0/windowsdesktop-runtime-win-x64.exe)
  * 👉 [Official .NET 8.0 Download Page](https://dotnet.microsoft.com/download/dotnet/8.0)
* **Download Software Installer:**
  * 👉 [**Download MI50FanControl_Setup.exe**](https://github.com/kingbone2006/MI50-Fan-Control/releases/latest/download/MI50FanControl_Setup.exe)

### 2. Installation
1. Install **.NET 8.0 Desktop Runtime (x64)** if not already installed.
2. Run MI50FanControl_Setup.exe as Administrator and click **Install**.
3. Launch MI50 Fan Control from your Desktop or Start Menu.

---

## 🗑️ Uninstallation Guide

The software includes a dedicated **Uninstall.exe** binary for a clean removal:

1. **Option 1 - Windows Settings / Control Panel:**
   * Open Windows **Settings** $\to$ **Apps** $\to$ **Installed Apps** (or Control Panel $\to$ Programs and Features).
   * Locate **AMD MI50 Fan Control** and click **Uninstall**.
2. **Option 2 - Run Uninstall.exe directly:**
   * Navigate to the application install folder (typically C:\Program Files\MI50FanControl).
   * Run **Uninstall.exe** as Administrator and click **Uninstall**.
3. The uninstaller will automatically:
   * Terminate running application processes.
   * Remove Desktop and Start Menu shortcuts and startup registry keys.
   * Clean up all installed files and directories.

---

## ☕ Support & Donate

If you enjoy this project and would like to support its ongoing development, feel free to buy me a coffee via Ko-fi:

<div align="center">

[![Buy Me A Coffee](https://img.shields.io/badge/Ko--fi-Buy%20Me%20A%20Coffee-FF5E5B?style=for-the-badge&logo=ko-fi&logoColor=white)](https://ko-fi.com/kingbone2006)

👉 **[https://ko-fi.com/kingbone2006](https://ko-fi.com/kingbone2006)**

<br/>

*Thank you so much for your generous support! ❤️*

</div>

---

## 📄 License
This project is licensed under the [MIT License](LICENSE).
