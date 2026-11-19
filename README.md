# Driver Guard Pro

Công cụ giám sát sự kiện phần cứng (PnP), sao lưu kho driver cục bộ và tự động khắc phục xung đột thiết bị trên nền tảng Windows 10/11 (x64).

## Cơ chế hoạt động (Under the Hood)

Thay vì chỉ thực hiện các thao tác cài đè thủ công, ứng dụng tương tác trực tiếp với tầng quản lý thiết bị và cấu hình hệ thống của Windows thông qua:

* **Real-time WMI Event Watcher:** Lắng nghe sự kiện `__InstanceOperationEvent` trên lớp `Win32_PnPEntity` (chu kỳ polling 2s) để bắt tức thì các thay đổi trạng thái phần cứng và mã lỗi `ConfigManagerErrorCode`.
* **PnPUtil Automation:** Điều khiển trực tiếp `pnputil.exe` ở chế độ chạy ngầm để trích xuất toàn bộ gói driver bên thứ 3 (`/export-driver`), quét lại phần cứng bị ẩn (`/scan-devices`), khởi động lại thiết bị (`/disable-device` / `/enable-device`) và ép nạp lại gói `.inf` sạch (`/add-driver ... /install /force`).
* **Registry & Power Policy Enforcement:**
  * Chặn Windows Update tự động ghi đè driver thông qua khóa `HKLM\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate` (`ExcludeWUDriversInQualityUpdate = 1`).
  * Vô hiệu hóa *Fast Startup* (`HiberbootEnabled = 0`) nhằm ngăn Windows lưu cache trạng thái lỗi của driver cũ sau khi tắt máy.
  * Dọn dẹp các khóa bộ lọc rác `UpperFilters` và `LowerFilters` tại `Control\Class` của nhóm thiết bị Bluetooth (`{e0cbf06c-cd8b-4647-bb8a-263b43f0f974}`) và Network (`{4d36e972-e325-11ce-bfc1-08002be10318}`) để xử lý dứt điểm lỗi Code 19 và Code 39.
  * Tắt cơ chế tự ngắt nguồn tiết kiệm pin (`EnhancedPowerManagementEnabled = 0`) trên toàn bộ adapter mạng không dây.
* **Hardware ID & OEM Lookup:** Truy vấn `Win32_ComputerSystem` và mảng `HardwareID` để điều hướng người dùng tới đúng trang hỗ trợ của hãng sản xuất (HP, Dell, Asus, Lenovo, Acer, MSI) hoặc tra cứu trực tiếp trên Microsoft Update Catalog khi thiết bị mất hoàn toàn driver.
* **Self-Updating via GitHub Releases:** Kiểm tra phiên bản thông qua GitHub REST API (`/releases/latest`) và tự động thay thế tệp thực thi đang chạy bằng batch script nền.

## Yêu cầu hệ thống & Quyền hạn

* **Hệ điều hành:** Windows 10 hoặc Windows 11 kiến trúc **x64**.
* **Môi trường chạy:** Không yêu cầu cài đặt trước .NET Runtime (đã đóng gói theo chuẩn Self-Contained Single-File).
* **Quyền hạn:** Bắt buộc chạy dưới quyền **Administrator** (tích hợp sẵn qua `app.manifest`) để thực thi `pnputil`, ghi khóa `HKLM` Registry và khởi động lại dịch vụ hệ thống (`bthserv`).

## Cài đặt & Vận hành

1. Truy cập mục **[Releases](https://github.com)** và tải về tệp thực thi mới nhất.
2. Đặt tệp `.exe` vào một thư mục cố định độc lập (ví dụ: `D:\Project\DriverGuard\`). Ứng dụng sẽ khởi tạo thư mục `DriverBackup` ngay tại đường dẫn gốc của tệp chạy.
3. Khởi chạy ứng dụng và xác nhận hộp thoại UAC.
4. Tại lần chạy đầu tiên:
   * Bấm **Sao Lưu Driver** để trích xuất toàn bộ driver ổn định hiện tại vào kho dự phòng cục bộ.
   * Kích hoạt tùy chọn chặn Windows Update ghi đè driver và khởi động cùng hệ thống.
5. Đóng cửa sổ chính (`X`) để đưa ứng dụng về trạng thái chạy ngầm dưới System Tray.

## Build từ mã nguồn

Dự án được phát triển trên **.NET 10 (`net10.0-windows`)**. Để tự biên dịch và đóng gói thành một tệp `.exe` độc lập:

```bash
git clone https://github.com
cd DriverGuardPro
dotnet publish -c Release -r win-x64 --self-contained true -p:PublishSingleFile=true -p:IncludeNativeLibrariesForSelfExtract=true -p:EnableCompressionInSingleFile=true -p:DebugType=None
```

Tệp thành phẩm sẽ nằm tại: `bin\Release\net10.0-windows\win-x64\publish\`.

## Tác giả

* **Lê Trọng Quyền Linh** – [quyenlinh1408-hl](https://github.com)
