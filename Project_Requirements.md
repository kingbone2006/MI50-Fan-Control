# TÀI LIỆU YÊU CẦU & THÔNG SỐ KỸ THUẬT TOÀN DIỆN (PROJECT SPECIFICATION)
## DỰ ÁN: AMD INSTINCT MI50 / RADEON PRO VII FAN CONTROLLER & HARDWARE MONITOR

---

## 📌 1. TỔNG QUAN DỰ ÁN & MỤC TIÊU CỐT LÕI
* **Tên ứng dụng:** AMD MI50 / Radeon PRO VII Fan Control (`MI50FanControl`).
* **Môi trường hoạt động:** Windows 10 / Windows 11 (64-bit).
* **Nền tảng công nghệ:** C# / .NET 8.0 WPF (Windows Presentation Foundation), Kiến trúc MVVM.
* **Mục tiêu chính:** 
  - Điều khiển tốc độ quạt làm mát gắn ngoài (cắm vào chân cắm quạt trên bo mạch chủ) dựa theo **nhiệt độ phần cứng thực tế của card đồ họa AMD Radeon Instinct MI50** (mod BIOS Radeon PRO VII 16GB).
  - Hoạt động **độc lập 100% (Standalone Native Controller)**: Tự giao tiếp với phần cứng ở cấp độ Ring0 / Driver, **tuyệt đối không phụ thuộc, không liên kết và không gửi lệnh qua SpeedFan hay phần mềm thứ 3 nào khác**.
  - **Tự động nhận diện động phần cứng (Dynamic Hardware Auto-Detection)**: Tự động phát hiện loại bo mạch chủ, chip điều khiển I/O và số lượng quạt thực tế đang cắm trên máy mà không dùng dữ liệu hardcode.

---

## 🌡️ 2. YÊU CẦU CẢM BIẾN NHIỆT ĐỘ GPU (AMD GPU REAL-TIME TELEMETRY)

### 2.1. Yêu Cầu Cốt Lõi Về Cảm Biến
* Đọc trực tiếp dữ liệu cảm biến thực tế theo thời gian thực từ driver AMD / phần cứng GPU.
* Tuyệt đối không hardcode, không dùng giá trị giả lập, không ước tính bù trừ.
* Chu kỳ cập nhật dữ liệu liên tục theo thời gian thực (~500ms – 1000ms).

### 2.2. Các Cảm Biến Hỗ Trợ
1. **GPU Core Temperature:** Nhiệt độ nhân GPU thực tế.
2. **GPU HotSpot Temperature:** Nhiệt độ điểm nóng nhất trên khuôn die silicon của GPU.
*(Không hỗ trợ và không hiển thị cảm biến GPU Memory/VRAM).*

### 2.3. Cơ Chế Kỹ Thuật Lấy Dữ Liệu
* **Giao tiếp trực tiếp Driver AMD (ADL SDK):**
  - Trích xuất trực tiếp dữ liệu telemetry phần cứng từ driver AMD:
    - Nhiệt độ GPU Core (°C).
    - Nhiệt độ GPU HotSpot (°C).
    - Công suất tiêu thụ GPU (Watts).
    - Xung nhịp GPU Clock (MHz).
* **Lựa chọn cảm biến điều khiển:**
  - Cho phép người dùng tùy chọn nguồn nhiệt độ để điều khiển quạt: **GPU HotSpot** hoặc **GPU Core**.
* **Đồng bộ giao diện:**
  - Toàn bộ giá trị nhiệt độ trên giao diện (Dashboard và Cài Đặt) phải cập nhật nhảy số liên tục từng giây theo thời gian thực.

---

## 🌀 3. YÊU CẦU ĐIỀU KHIỂN QUẠT & GIAO TIẾP BO MẠCH CHỦ (SUPERIO CONTROLLER)

### 3.1. Độc Lập Hoàn Toàn – Standalone Ring0 Kernel Driver
* Ứng dụng chạy với quyền Administrator (`requireAdministrator`) để nạp kernel driver Ring0 phục vụ việc đọc thông số và điều khiển quạt bo mạch chủ.
* Tự động khởi tạo và giải phóng driver an toàn.

### 3.2. Nhận Diện Động Bo Mạch Chủ & Chip SuperIO
* **Nhận diện bo mạch chủ:** Tự động nhận diện thông tin nhà sản xuất và model bo mạch chủ của máy.
* **Dò quét chip SuperIO:**
  - Tự động quét và nhận diện dòng chip điều khiển I/O trên bo mạch chủ (ITE, Nuvoton, Winbond, Fintek...).
  - Tự động kích hoạt bộ điều khiển môi trường và cổng I/O phần cứng.

### 3.3. Phát Hiện Số Lượng Quạt Thực Tế Đang Hoạt Động
* Tự động quét toàn bộ các cổng quạt trên bo mạch chủ.
* Đọc xung nhịp cảm biến và tính toán tốc độ vòng quay thực tế (Live RPM).
* **Quy tắc lọc:** Chỉ khởi tạo và hiển thị lên giao diện những cổng nào **thực sự có quạt đang cắm và quay (RPM > 0)**. Máy có bao nhiêu quạt đang hoạt động thì hiển thị đúng bấy nhiêu quạt (không tạo quạt ảo).

### 3.4. Điều Khiển Tốc Độ Quạt Phần Cứng (Direct Hardware PWM Control)
* Tự động chuyển cổng quạt sang chế độ điều khiển phần mềm (Software PWM Mode).
* Điều chỉnh trực tiếp xung công suất quạt (từ 0% đến 100%) tương ứng theo đường cong nhiệt độ của GPU.
* Khôi phục quyền điều khiển tự động cho BIOS khi tắt ứng dụng.

---

## 🎨 4. YÊU CẦU GIAO DIỆN, ĐA NGÔN NGỮ & FILE GỠ CÀI ĐẶT RIÊNG

### 4.1. Giao Diện Người Dùng (UI/UX)
* Giao diện phong cách tối hiện đại (Dark Modern Theme), bố cục rõ ràng, không bị chồng đè phần tử.
* Sử dụng icon ứng dụng hình chiếc quạt tùy chỉnh trên toàn bộ hệ thống.

### 4.2. Các Phân Hệ Chức Năng
1. **Live Dashboard:**
   - Hiển thị đồng hồ nhiệt độ GPU lớn theo thời gian thực (°C).
   - Tên cảm biến đang được chọn để điều khiển quạt.
   - Danh sách quạt thực tế: Tên quạt, tốc độ vòng quay thực tế (Live RPM), công suất PWM hiện tại, thanh trượt hiển thị.
   - Nút **Test 100%** quạt trong 5 giây.
   - Nút **Ghi đè thủ công (Manual Override)** để chỉnh nhanh tốc độ quạt chung.
   - Dropdown chọn nhanh cấu hình đường cong quạt.

2. **Đường Cong Quạt (Curve Editor):**
   - Tạo, chỉnh sửa, đổi tên, xóa cấu hình đường cong quạt.
   - Tùy chỉnh các mốc nhiệt độ (°C) $\to$ tốc độ quạt (%).
   - Tích hợp thuật toán làm mượt (Smoothing & Hysteresis) để tốc độ quạt tăng giảm êm ái, tránh rú quạt khi nhiệt độ dao động nhẹ.

3. **Quản Lý Cổng Quạt (Fan Manager):**
   - Đặt tên tùy chỉnh cho từng quạt.
   - Chọn chế độ điều khiển riêng cho từng quạt: Theo đường cong, Thủ công cố định, hoặc Mặc định BIOS.
   - Thiết lập giới hạn Min/Max PWM an toàn cho từng quạt.

4. **Cài Đặt & Phần Cứng (Settings & Hardware):**
   - Chọn loại cảm biến nhiệt độ GPU: **GPU HotSpot** hoặc **GPU Core**. Có hiển thị giá trị thời gian thực của từng cảm biến.
   - Hiển thị thông tin phần cứng đã nhận diện (Tên Bo mạch chủ, Tên chip SuperIO).
   - **Đa ngôn ngữ (Localization):**
     - Chuyển đổi ngôn ngữ Tiếng Việt, Tiếng Anh...
     - Toàn bộ chuỗi văn bản nằm trong thư mục `lang/` dưới dạng file JSON (`vi.json`, `en.json`, `template.json`) để bên thứ ba dễ dàng đóng góp bản dịch mới.
     - Có nút mở nhanh thư mục `lang/`.
   - **Khởi động cùng Windows:** Tự động ghi/xóa cấu hình khởi động cùng hệ thống.
   - **Khay hệ thống (System Tray):** Thu nhỏ về khay hệ thống khi khởi động hoặc khi bấm thu nhỏ/đóng cửa sổ.
   - **Bảo vệ quá nhiệt khẩn cấp:** Tự động ép toàn bộ quạt lên 100% khi nhiệt độ GPU vượt ngưỡng giới hạn an toàn.

### 4.3. Yêu Cầu File `Uninstall.exe` Riêng Biệt
* **Bắt buộc:** Trong thư mục gốc cài đặt của ứng dụng phải có sẵn một file thực thi độc lập mang tên **`Uninstall.exe`**.
* **Chức năng:**
  - Chạy độc lập, hiển thị xác nhận trước khi gỡ.
  - Tắt toàn bộ tiến trình ứng dụng đang chạy.
  - Xóa sạch Shortcut Desktop, Start Menu, cấu hình khởi động và đăng ký gỡ cài đặt trong Windows.
  - Xóa sạch toàn bộ file và thư mục cài đặt phần mềm.

---

## 📦 5. CẤU TRÚC THƯ MỤC DỰ ÁN & ĐÓNG GÓI BỘ CÀI ĐẶT

### 5.1. Cấu Trúc Thư Mục Chuẩn
```text
gmncodefancontrolmi50/
├── PROJECT_REQUIREMENTS.md            # Tài liệu yêu cầu kỹ thuật
├── MI50FanControl_Setup.exe           # Bộ cài đặt hoàn chỉnh
├── publish/                           # Thư mục ứng dụng đã publish
│   ├── MI50FanControl.exe             # Ứng dụng chính
│   ├── Uninstall.exe                  # FILE THỰC THI GỠ CÀI ĐẶT RIÊNG BIỆT
│   ├── appsettings.json
│   ├── lang/
│   │   ├── vi.json
│   │   ├── en.json
│   │   └── template.json
│   └── *.dll
└── src/
    ├── MI50FanControl/                # Dự án WPF chính
    │   ├── Assets/                    # Icon quạt
    │   ├── Hardware/                  # Giao tiếp phần cứng (Native AMD Driver & Ring0 SuperIO)
    │   ├── Models/                    # Data models
    │   ├── Services/                  # Business logic
    │   ├── ViewModels/                # MVVM ViewModels
    │   └── Views/                     # XAML Views
    ├── MI50FanControl.Installer/      # Bộ cài đặt Single-file
    └── FanDiag/                       # Tool console kiểm tra phần cứng
```

### 5.2. Lệnh Build & Đóng Gói (PowerShell)
```powershell
# 1. Publish ứng dụng chính
dotnet publish src/MI50FanControl/MI50FanControl.csproj -c Release -r win-x64 --self-contained false -o publish

# 2. Nén payload vào bộ cài
Compress-Archive -Path publish\* -DestinationPath src/MI50FanControl.Installer/app_payload.zip -Force

# 3. Publish bộ cài đặt Single-file
dotnet publish src/MI50FanControl.Installer/MI50FanControl.Installer.csproj -c Release -r win-x64 --self-contained false -p:PublishSingleFile=true -o Setup

# 4. Copy bộ cài ra thư mục gốc
Copy-Item -Path Setup/MI50FanControl_Setup.exe -Destination ./MI50FanControl_Setup.exe -Force
```

---

## 🎯 6. BẢNG TIÊU CHÍ NGHIỆM THU (ACCEPTANCE CRITERIA)

| STT | Hạng mục kiểm tra | Tiêu chí đạt |
| :---: | :--- | :--- |
| **1** | **Nhiệt độ GPU Core & HotSpot** | **Đọc đúng và cập nhật liên tục giá trị thực tế của cảm biến GPU Core và GPU HotSpot từ phần cứng/driver AMD. Bỏ hoàn toàn cảm biến Memory.** |
| **2** | **File `Uninstall.exe` riêng biệt** | **Trong thư mục gốc cài đặt của phần mềm bắt buộc có sẵn file `Uninstall.exe` riêng biệt để thực hiện gỡ cài đặt sạch sẽ.** |
| **3** | Độc lập không dùng SpeedFan | Ứng dụng tự chạy điều khiển quạt bình thường khi SpeedFan đã bị tắt/gỡ bỏ hoàn toàn. |
| **4** | Nhận diện Bo mạch & Chip I/O | Tự động nhận diện và hiển thị đúng tên Bo mạch chủ và tên chip SuperIO. |
| **5** | Số lượng quạt hiển thị | Máy cắm bao nhiêu quạt đang quay thì hiển thị đúng bấy nhiêu quạt (không tạo quạt ảo). |
| **6** | Tốc độ vòng quay (Live RPM) | Hiển thị số vòng quay RPM thực tế của quạt đang quay trên máy. |
| **7** | Điều khiển tốc độ quạt | Khi điều chỉnh thanh trượt hoặc nhiệt độ GPU thay đổi, tốc độ quạt vật lý thực tế phải tăng/giảm theo. |
| **8** | Đa ngôn ngữ (Lang) | Hỗ trợ đổi ngôn ngữ, có thư mục `lang/` chứa file `template.json` cho cộng đồng đóng góp bản dịch. |
| **9** | Bộ cài đặt `Setup.exe` | File `MI50FanControl_Setup.exe` cài đặt nhanh chóng, tự tạo shortcut Desktop/Start Menu. |
