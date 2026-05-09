# Hướng Dẫn Phát Triển & Deploy Nhanh (Tourist Guide System)

Tài liệu này hướng dẫn quy trình chuẩn dành cho lập trình viên để tăng tốc độ code, kiểm thử (test) tức thì không cần đợi server, và quy trình deploy tự động lên Azure.

---

## 🚀 1. QUY TRÌNH TEST NHANH (KHÔNG CẦN AZURE)
*Mục đích: Viết code tới đâu, thấy kết quả tới đó trong vòng 1 giây.*

### Bước 1: Khởi động hệ thống Backend và Web (Localhost)
Mở 2 cửa sổ **Terminal / PowerShell** tại thư mục gốc của dự án (`Csharpppppido`):

1. **Chạy API:**
   ```powershell
   dotnet watch run --project TourGuideApi\TourGuideApi.csproj
   ```
   *(Ghi nhớ cái Cổng (Port) mà API đang chạy, ví dụ: `http://localhost:5222`)*

2. **Chạy Web Admin:**
   ```powershell
   dotnet watch run --project TouristGuideWeb\TouristGuideWeb.csproj
   ```

Tính năng `watch` (Hot Reload) sẽ tự động theo dõi file của bạn. Mỗi khi bạn sửa code và bấm `Ctrl + S`, hệ thống tự cập nhật giao diện và logic mà không cần phải gõ lại lệnh tắt/mở!

### Bước 2: Khởi động Mobile App & Kết nối Localhost
Cái máy ảo Android (Emulator) là một chiếc "điện thoại" độc lập nên nó không hiểu chữ `localhost` của máy tính. Mã bí mật để nó kết nối ngược ra máy tính là `10.0.2.2`.

1. Mở Visual Studio và chạy dự án `TouristGuideApp` trên máy ảo Android (nhấn F5).
2. Khi App mở lên, vào trang **Cài đặt (Settings)** bên trong App.
3. Ở ô thiết lập link API, nhập:
   ```text
   http://10.0.2.2:5222/
   ```
   *(Thay số `5222` bằng số Cổng của API ở Bước 1)*
4. Nhấn Lưu! Bây giờ App, Web và API của bạn đã thông nhau 100% offline. Bạn sửa data trên Web localhost, App sẽ tự động nhận diện thay đổi.

---

## ☁️ 2. QUY TRÌNH DEPLOY LÊN AZURE BẰNG TERMINAL
*Mục đích: Khi code đã test xong và chạy hoàn hảo ở Localhost, bạn muốn đưa lên mạng cho người thật dùng.*

Chúng ta đã tạo sẵn kịch bản triển khai tự động tên là `deploy.ps1`. Chỉ với 1 dòng lệnh duy nhất, hệ thống sẽ tự động: Đóng gói (Publish) -> Nén Zip -> Đẩy thẳng lên máy chủ Azure Linux.

### Yêu cầu trước khi chạy (Chỉ làm 1 lần):
- Đảm bảo máy đã cài đặt Azure CLI.
- Mở Terminal và gõ `az login` để đăng nhập vào tài khoản Azure.
- Mở file `deploy.ps1` bằng trình soạn thảo và sửa biến `$ResourceGroup` thành tên Resource Group của bạn trên Azure.

### Lệnh Deploy Siêu Tốc:
Mở Terminal tại thư mục gốc và chạy 1 trong các lệnh sau tuỳ theo nhu cầu:

**Cập nhật API lên mạng:**
```powershell
.\deploy.ps1 api
```

**Cập nhật Web Admin lên mạng:**
```powershell
.\deploy.ps1 web
```

*(Sau khi lệnh chạy xong 100%, bạn lên App/Settings đổi lại link API về dạng `https://sharpppio-api.azurewebsites.net` để dùng bản online).*

---
**📝 Ghi chú quan trọng:** 
Database của bạn đã được cấu hình trong `TourGuideApi\Program.cs` để tự động chạy Migration mỗi lần API được Deploy mới. Vì vậy, cứ mỗi khi đổi CSDL, bạn chỉ việc gõ `.\deploy.ps1 api` là xong.