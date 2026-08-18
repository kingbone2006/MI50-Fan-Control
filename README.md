<div align="center">

# 🌀 AMD MI50 / Radeon PRO VII Fan Controller

**Bộ điều khiển quạt phần cứng độc lập & Giám sát nhiệt độ thời gian thực cho AMD Instinct MI50 / Radeon PRO VII**  
*Standalone Hardware Fan Controller & Real-Time Telemetry Monitor for AMD Instinct MI50 / Radeon PRO VII*

---

[![GitHub Release](https://img.shields.io/github/v/release/USER_PLACEHOLDER/REPO_PLACEHOLDER?style=for-the-badge&color=0078d4&logo=github)](https://github.com/USER_PLACEHOLDER/REPO_PLACEHOLDER/releases/latest)
[![Platform](https://img.shields.io/badge/Platform-Windows%2010%20%7C%2011%20(64--bit)-0078d4?style=for-the-badge&logo=windows)](https://github.com/USER_PLACEHOLDER/REPO_PLACEHOLDER)
[![.NET](https://img.shields.io/badge/.NET-8.0%20WPF-512bd4?style=for-the-badge&logo=dotnet)](https://dotnet.microsoft.com/download/dotnet/8.0)
[![License](https://img.shields.io/badge/License-MIT-green?style=for-the-badge)](LICENSE)
[![Visitor Count](https://komarev.com/ghpvc/?username=USER_PLACEHOLDER&repo=REPO_PLACEHOLDER&color=0078d4&style=for-the-badge&label=REPO+VIEWS)](https://github.com/USER_PLACEHOLDER/REPO_PLACEHOLDER)
[![Hits](https://hits.seeyoufarm.com/api/count/incr/badge.svg?url=https%3A%2F%2Fgithub.com%2FUSER_PLACEHOLDER%2FREPO_PLACEHOLDER&count_bg=%230078D4&title_bg=%2324292E&icon=&icon_color=%23E7E7E7&title=Views&edge_flat=false)](https://hits.seeyoufarm.com)

<br/>

### 🚀 TẢI XUỐNG / DOWNLOAD

[![Direct Download](https://img.shields.io/badge/TẢI_XUỐNG_NGAY_(DIRECT_DOWNLOAD)-MI50FanControl__Setup.exe-brightgreen?style=for-the-badge&logo=windows&logoColor=white)](https://github.com/USER_PLACEHOLDER/REPO_PLACEHOLDER/releases/latest/download/MI50FanControl_Setup.exe)

[📦 **Xem tất cả bản phát hành (All Releases)**](https://github.com/USER_PLACEHOLDER/REPO_PLACEHOLDER/releases)

---

**[🇻🇳 Tiếng Việt](#-tiếng-việt)** | **[🇬🇧 English](#-english)**

</div>

---

# 🇻🇳 Tiếng Việt

## 📖 Giới thiệu
**MI50 Fan Control** là phần mềm điều khiển tốc độ quạt gắn ngoài dành riêng cho card đồ họa **AMD Radeon Instinct MI50** (đặc biệt là các bản mod BIOS **Radeon PRO VII 16GB**).

Phần mềm hoạt động **hoàn toàn độc lập (100% Standalone Native Ring0)**, tự động giao tiếp trực tiếp với chip điều khiển I/O trên bo mạch chủ thông qua Kernel Driver và đọc trực tiếp dữ liệu cảm biến từ driver AMD ADL. **Không phụ thuộc vào SpeedFan hay bất kỳ phần mềm bên thứ ba nào.**

---

## ✨ Tính năng nổi bật

### 1. 🌡️ Giám sát nhiệt độ AMD GPU theo thời gian thực (Real-Time Telemetry)
* Giao tiếp trực tiếp với AMD Driver qua thư viện ADL SDK native.
* Đọc chính xác **GPU Core Temperature** và **GPU HotSpot Temperature** theo thời gian thực (~500ms - 1000ms).
* Hiển thị công suất tiêu thụ GPU (**Watts**) và xung nhịp (**Clock MHz**).
* Tuyệt đối **không dùng giá trị giả lập hay ước tính**.

### 2. 🌀 Điều khiển quạt bo mạch chủ cấp độ Ring0 (SuperIO Controller)
* Tự động nhận diện bo mạch chủ và dò quét chip **SuperIO** (ITE, Nuvoton, Winbond, Fintek...).
* **Tự động nhận diện cổng quạt thực tế**: Chỉ hiển thị những cổng quạt thực sự có cắm quạt và đang quay (**RPM > 0**), không sinh quạt ảo.
* Điều khiển trực tiếp xung công suất PWM phần cứng từ **0% đến 100%**.
* Tự động khôi phục quyền điều khiển BIOS mặc định khi tắt ứng dụng.

### 3. 📈 Tùy chỉnh đường cong quạt thông minh (Fan Curve Editor)
* Tự do chỉnh sửa đường cong nhiệt độ $\to$ tốc độ quạt (%).
* Tích hợp thuật toán **Làm mượt (Smoothing)** và **Độ trễ nhiệt (Hysteresis)** giúp quạt tăng/giảm tốc êm ái, loại bỏ hoàn toàn hiện tượng rú quạt khi nhiệt độ GPU dao động nhẹ.

### 4. 🛡️ Bảo vệ quá nhiệt khẩn cấp (Emergency Overheat Protection)
* Tự động ép toàn bộ quạt lên **100% công suất** ngay lập tức nếu nhiệt độ GPU vượt ngưỡng giới hạn an toàn người dùng thiết lập.

### 5. 🎛️ Quản lý quạt linh hoạt (Fan Manager)
* Đặt tên tùy chỉnh cho từng cổng quạt (Quạt thổi MI50, Quạt hút case...).
* Tùy chọn chế độ hoạt động riêng cho từng quạt: **Theo đường cong (Curve)**, **Cố định thủ công (Manual)**, hoặc **BIOS mặc định**.
* Cài đặt giới hạn Min/Max PWM an toàn.
* Nút **Test 100%** trong 5 giây để kiểm tra quạt tức thì.

### 6. 🌐 Đa ngôn ngữ & Tiện ích hệ thống
* Hỗ trợ giao diện **Tiếng Việt** và **Tiếng Anh**.
* Hệ thống ngôn ngữ dạng JSON trong thư mục lang/ (i.json, en.json, 	emplate.json) giúp cộng đồng dễ dàng dịch thêm ngôn ngữ mới.
* Thu nhỏ về **Khay hệ thống (System Tray)**.
* Tự động **khởi động cùng Windows**.
* Tích hợp sẵn file gỡ cài đặt độc lập **Uninstall.exe** giúp gỡ bỏ sạch sẽ mọi file, shortcut và cấu hình khởi động.

---

## 💻 Yêu cầu hệ thống
* **Hệ điều hành:** Windows 10 / Windows 11 (64-bit).
* **Quyền thực thi:** Chạy dưới quyền Quản trị viên (**Administrator**) để nạp Ring0 Kernel Driver điều khiển quạt phần cứng.
* **Môi trường chạy:** .NET 8.0 Desktop Runtime x64 (Bộ cài đặt đã tích hợp sẵn hoặc tự động thông báo nếu thiếu).
* **Phần cứng tương thích:** AMD Radeon Instinct MI50 / Radeon PRO VII / Radeon VII / Vega 20.

---

## 📥 Cài đặt & Hướng dẫn sử dụng

### Cài đặt nhanh
1. Tải bộ cài đặt: [**MI50FanControl_Setup.exe**](https://github.com/USER_PLACEHOLDER/REPO_PLACEHOLDER/releases/latest/download/MI50FanControl_Setup.exe)
2. Chạy file MI50FanControl_Setup.exe với quyền Administrator và bấm **Cài đặt**.
3. Ứng dụng sẽ tự động được khởi tạo trên Desktop và Start Menu.

### Hướng dẫn sử dụng
1. Mở ứng dụng MI50 Fan Control từ Desktop.
2. Vào **Cài đặt (Settings)**:
   * Chọn cảm biến làm nguồn điều khiển: **GPU HotSpot** (khuyến nghị cho MI50) hoặc **GPU Core**.
   * Bật **Khởi động cùng Windows** và **Thu nhỏ xuống khay hệ thống** nếu muốn ứng dụng chạy ngầm liên tục.
3. Vào **Đường cong quạt (Fan Curve)** để tùy chỉnh các điểm nhiệt độ mong muốn.
4. Vào **Quản lý quạt (Fan Manager)** để đặt tên và gán cấu hình cho từng quạt tản nhiệt.

---

## 🛠️ Hướng dẫn biên dịch từ mã nguồn (Build from Source)

Yêu cầu: Đã cài đặt [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

`powershell
# 1. Clone repository
git clone https://github.com/USER_PLACEHOLDER/REPO_PLACEHOLDER.git
cd REPO_PLACEHOLDER

# 2. Build và Publish ứng dụng chính
dotnet publish src/MI50FanControl/MI50FanControl.csproj -c Release -r win-x64 --self-contained false -o publish

# 3. Đóng gói payload bộ cài
Compress-Archive -Path publish\* -DestinationPath src/MI50FanControl.Installer/app_payload.zip -Force

# 4. Build bộ cài đặt Single-file
dotnet publish src/MI50FanControl.Installer/MI50FanControl.Installer.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o Setup
`

---

<br/>

# 🇬🇧 English

## 📖 Overview
**MI50 Fan Control** is a dedicated external hardware fan controller and telemetry monitoring software designed specifically for **AMD Radeon Instinct MI50** graphics accelerators (particularly those running modded **Radeon PRO VII 16GB** BIOS).

The application operates as a **100% Standalone Native Ring0 Controller**, communicating directly with onboard SuperIO chips via a kernel driver and querying telemetry from the native AMD ADL driver. **Zero dependency on SpeedFan or any 3rd-party software.**

---

## ✨ Key Features

### 1. 🌡️ Real-Time AMD GPU Telemetry Monitoring
* Direct interface with AMD driver using native ADL SDK.
* Real-time polling (~500ms - 1000ms) for **GPU Core Temperature** and **GPU HotSpot Temperature**.
* Live monitoring of **Power Consumption (Watts)** and **Engine Clock (MHz)**.
* **100% authentic sensor readings** without emulation or guesswork.

### 2. 🌀 Ring0 Hardware SuperIO Fan Control
* Dynamic auto-detection of motherboard vendor, model, and **SuperIO chipset** (ITE, Nuvoton, Winbond, Fintek, etc.).
* **Dynamic Active Fan Detection**: Filters and displays only fan headers with physical fans spinning (**Live RPM > 0**), preventing ghost fans.
* Direct hardware **PWM speed control from 0% to 100%**.
* Automatically releases and restores BIOS default fan curves upon application exit.

### 3. 📈 Intelligent Fan Curve Editor
* Visual multi-point temperature-to-speed curve configuration.
* Integrated **Smoothing** and **Hysteresis** algorithms to eliminate sudden fan noise oscillations during short-lived temperature spikes.

### 4. 🛡️ Emergency Overheat Protection
* Automatically ramps all fans to **100% full speed** immediately when GPU temperature crosses the critical safety threshold.

### 5. 🎛️ Comprehensive Fan Manager
* Custom naming for individual fan headers.
* Per-fan control modes: **Curve Profile**, **Manual Override**, or **BIOS Default**.
* Min / Max PWM speed clamping for hardware safety.
* **5-Second 100% Test** button for quick physical fan verification.

### 6. 🌐 Localization & System Utilities
* Multilingual support with built-in **English** and **Vietnamese**.
* JSON-based localization files (i.json, en.json, 	emplate.json) in the lang/ folder for easy community translations.
* Seamless **System Tray** background operation.
* Automatic **Windows Startup** integration.
* Includes a dedicated, clean **Uninstall.exe** binary.

---

## 💻 System Requirements
* **OS:** Windows 10 / Windows 11 (64-bit).
* **Privileges:** Administrator privileges required (to load Ring0 Kernel Driver for hardware SuperIO control).
* **Runtime:** .NET 8.0 Desktop Runtime x64.
* **Supported Hardware:** AMD Radeon Instinct MI50, Radeon PRO VII, Radeon VII, and Vega 20 series GPUs.

---

## 📥 Download & Installation

### Quick Install
1. Download installer: [**MI50FanControl_Setup.exe**](https://github.com/USER_PLACEHOLDER/REPO_PLACEHOLDER/releases/latest/download/MI50FanControl_Setup.exe)
2. Run MI50FanControl_Setup.exe with Administrator rights and click **Install**.
3. Launch MI50 Fan Control from your Desktop or Start Menu.

---

## 🛠️ Build from Source

Prerequisites: [.NET 8.0 SDK](https://dotnet.microsoft.com/download/dotnet/8.0).

`powershell
# 1. Clone the repository
git clone https://github.com/USER_PLACEHOLDER/REPO_PLACEHOLDER.git
cd REPO_PLACEHOLDER

# 2. Publish main application
dotnet publish src/MI50FanControl/MI50FanControl.csproj -c Release -r win-x64 --self-contained false -o publish

# 3. Package payload
Compress-Archive -Path publish\* -DestinationPath src/MI50FanControl.Installer/app_payload.zip -Force

# 4. Publish Single-file Installer
dotnet publish src/MI50FanControl.Installer/MI50FanControl.Installer.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o Setup
`

---

## 📄 License
This project is licensed under the [MIT License](LICENSE).
