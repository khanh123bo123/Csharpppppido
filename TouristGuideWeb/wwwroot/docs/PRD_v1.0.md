# TÀI LIỆU YÊU CẦU SẢN PHẨM (PRD) - HỆ THỐNG DU LỊCH VĨNH KHÁNH

## 1. Tổng quan dự án
Hệ thống **Vĩnh Khánh Tourist Guide** là giải pháp chuyển đổi số cho Phố ẩm thực Vĩnh Khánh (Quận 4), hỗ trợ du khách tiếp cận thông tin văn hóa, ẩm thực thông qua công nghệ quét mã QR, thuyết minh tự động (TTS) và bản đồ tương tác. Hệ thống cũng cung cấp công cụ quản lý chuyên sâu cho ban quản lý và các chủ hộ kinh doanh.

## 2. Đối tượng người dùng (Actors)
1.  **Khách du lịch (Guest/User):** Người sử dụng ứng dụng di động để khám phá phố ẩm thực.
2.  **Chủ quán (Owner):** Người quản lý thông tin và theo dõi đánh giá của địa điểm cụ thể.
3.  **Quản trị viên (Admin):** Người có quyền cao nhất, quản lý toàn bộ hệ thống, ngôn ngữ và tài khoản.

## 3. Các tính năng chính (Core Features)

### 3.1. Ứng dụng Di động (Mobile App - .NET MAUI)
*   **Khám phá địa điểm:** Hiển thị danh sách các quán ăn, di tích theo danh mục (Nhà hàng, Cafe, Lịch sử...).
*   **Quét mã QR:** Truy cập nhanh thông tin chi tiết địa điểm bằng cách quét mã QR tại hiện trường.
*   **Thuyết minh tự động (TTS):** Nghe thuyết minh về lịch sử, đặc điểm món ăn bằng nhiều ngôn ngữ (Việt, Anh, Trung, Nhật, Hàn).
*   **Bản đồ tương tác:** Định vị vị trí người dùng và hiển thị các điểm đến xung quanh với nhãn tên trực quan.
*   **Đánh giá & Phản hồi:** Cho phép người dùng đánh giá sao cho các địa điểm.
*   **Chế độ Offline:** Tải trước dữ liệu và bản đồ để sử dụng khi không có kết nối mạng.
*   **Theo dõi Online:** Tự động báo cáo trạng thái đang hoạt động về hệ thống quản trị (Heartbeat mỗi 30 giây).

### 3.2. Hệ thống Web Quản trị (Web Admin - ASP.NET Core)
*   **Bảng điều khiển (Dashboard):** Theo dõi thời gian thực số lượng thiết bị online, tổng lượt nghe TTS, và thống kê đánh giá mới nhất.
*   **Quản lý địa điểm (POI Management):** Thêm, sửa, xóa thông tin quán ăn, tọa độ và hình ảnh.
*   **Hệ thống đa ngôn ngữ:** Tự động dịch và tạo file âm thanh thuyết minh cho nhiều ngôn ngữ khác nhau thông qua Edge-TTS.
*   **Theo dõi thiết bị:** Giám sát danh sách và số lượng khách du lịch đang hiện diện tại phố ẩm thực.
*   **Quản lý QR Code:** Tự động tạo và quản lý mã QR định danh cho từng địa điểm.

## 4. Yêu cầu kỹ thuật (Technical Requirements)
*   **Backend:** ASP.NET Core API 10.0, SQLite (Entity Framework Core).
*   **Mobile:** .NET MAUI (Targeting Android 10.0+).
*   **Frontend Web:** ASP.NET Core MVC, Vanilla CSS, Chart.js, Leaflet.
*   **Công nghệ lõi:** Edge-TTS (Voice), Ollama (AI Translation), IMemoryCache (Performance).

## 5. Kế hoạch triển khai
*   **Giai đoạn 1:** Xây dựng cơ sở dữ liệu và API lõi.
*   **Giai đoạn 2:** Phát triển Mobile App và tính năng Offline.
*   **Giai đoạn 3:** Hoàn thiện Web Dashboard và hệ thống theo dõi Online.
*   **Giai đoạn 4:** Kiểm thử và bàn giao (Bản hiện tại v1.0).
