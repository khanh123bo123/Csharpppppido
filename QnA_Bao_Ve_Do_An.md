# 🎓 BỘ CÂU HỎI BẢO VỆ ĐỒ ÁN: TOURIST GUIDE SYSTEM

Dưới đây là cẩm nang tổng hợp "tất tần tật" các chức năng cốt lõi của dự án. Tài liệu này được thiết kế theo đúng "khẩu vị" của các thầy hội đồng: **Hỏi Chức năng -> Chỉ ra Code (Hàm) -> Chỉ ra Sơ đồ (UML)**.

---

## 1. Tính năng: Bắt tọa độ GPS và Phát âm thanh tự động (Geofencing)
**Thầy hỏi:** *"Tính năng cốt lõi của hệ thống là tự động phát âm thanh khi đến gần địa điểm. Tính năng này hoạt động như thế nào, nằm ở hàm nào và sơ đồ nào thể hiện?"*

*   **Cách hoạt động:** App sẽ chạy ngầm (Background Service) và liên tục lấy tọa độ GPS của người dùng (ví dụ 5s/lần). Sau đó, nó dùng thuật toán tính khoảng cách (Haversine) để xem khoảng cách từ user đến các điểm POI có nhỏ hơn Bán kính quy định (Radius) hay không. Nếu có, nó sẽ gọi hệ thống Text-to-Speech (TTS) của hệ điều hành để đọc đoạn văn bản tiếng Việt lên.
*   **Hàm/Service biểu diễn (Mở các file này lên):** 
    *   Lấy GPS: Mở file `TouristGuideApp/Services/LocationService.cs` hoặc `GeofenceService.cs`.
    *   Tính khoảng cách: Thuật toán `CalculateHaversineDistanceKm` ở dòng 83 file `TouristGuideWeb/Controllers/DashboardController.cs`.
    *   Phát âm thanh: Mở file `TouristGuideApp/Services/AudioService.cs` (hàm gọi `TextToSpeech.SpeakAsync`).
*   **Sơ đồ thể hiện:** 
    *   **Mục 3. GPS & Geofence Engine** (Sơ đồ State Diagram chỉ ra luồng chạy từ hàm `OnLocationChanged` đến `ProcessGeofenceEvent` và chống lặp âm thanh).
    *   **Mục 8. End-to-End Flow** (Sơ đồ Sequence Diagram mô tả chuỗi gọi hàm từ `MainViewModel` qua `GeofenceService` tới `AudioService`).

---

## 2. Tính năng: Đồng bộ dữ liệu & Chế độ Offline
**Thầy hỏi:** *"Làm sao App trên điện thoại có thể chạy được khi khách du lịch không có 4G/Wifi? Đồng bộ dữ liệu bằng hàm nào?"*

*   **Cách hoạt động:** App sử dụng kiến trúc "Offline-First". Mọi dữ liệu (Tọa độ POI, nội dung thuyết minh) đều được lưu sẵn trong một Database thu nhỏ trên điện thoại (`SQLite`). Khi mất mạng, app đọc thẳng từ SQLite ra để phát âm thanh. Khi có mạng, nó sẽ gọi lên Server lấy các dữ liệu mới nhất (dựa vào `LastSyncToken`) để cập nhật vào SQLite.
*   **Hàm/Service biểu diễn (Mở các file này lên):** 
    *   Phía Mobile App: Mở file `TouristGuideApp/Services/SyncService.cs`.
    *   Phía Web API: Mở các file trong thư mục `TourGuideApi/Controllers/` (ví dụ `LocationsController.cs`).
*   **Sơ đồ thể hiện:** 
    *   **Mục 6. Chế độ Offline & Sync Service** (Sơ đồ Sequence Diagram thể hiện luồng gọi hàm `GetPoisAsync()` khi Offline và API `SyncController` khi Online).

---

## 3. Tính năng: Thống kê số lượng Thiết bị Đang hoạt động (Online)
**Thầy hỏi:** *"Làm sao trang Web Admin biết được có bao nhiêu thiết bị đang bật App? Logic xử lý là gì?"*

*   **Cách hoạt động:** Các thiết bị Mobile khi mở App sẽ liên tục gửi các gói tin "Heartbeat" (Nhịp tim) hoặc lưu log về Server. Server đếm số lượng các user có hoạt động trong vòng vài phút đổ lại.
*   **Hàm/Service biểu diễn (Mở các file này lên):** 
    *   Logic lấy số liệu: Hàm `GetOnlineCountAsync()` trong `TouristGuideWeb/Services/LocationApiService.cs`.
*   **Sơ đồ thể hiện:** 
    *   **Mục 10. Web Dashboard Metrics** (Sơ đồ Sequence chỉ rõ quá trình `Index.cshtml` gọi `DashboardController` và lấy số liệu qua `LocationApiService`).

---

## 4. Tính năng: Tính toán khoảng cách (Ví dụ: Các điểm gần Hà Nội nhất)
**Thầy hỏi:** *"Trên Dashboard tôi thấy có phần gợi ý địa điểm gần nhất, hệ thống tính toán bằng công thức nào và ở đâu?"*

*   **Cách hoạt động:** Hệ thống sử dụng công thức **Haversine** để tính toán khoảng cách đường chim bay giữa 2 điểm tọa độ (Kinh độ/Vĩ độ) trên bề mặt hình cầu của Trái Đất, sau đó sắp xếp (`OrderBy`) theo khoảng cách tăng dần và lấy ra 5 điểm gần nhất (`Take(5)`).
*   **Hàm/Service biểu diễn (Mở các file này lên):** 
    *   Hàm `CalculateHaversineDistanceKm(lat1, lon1, lat2, lon2)` nằm ở **dòng 83** trong file `TouristGuideWeb/Controllers/DashboardController.cs`.
*   **Sơ đồ thể hiện:** 
    *   **Mục 11. Thuật toán Khoảng cách (Haversine)** (Sơ đồ Sequence Diagram chỉ rõ vòng lặp tính toán khoảng cách của `DashboardController` gọi hàm `CalculateHaversineDistanceKm` và lấy ra top 5 điểm gần nhất).

---

## 5. Tính năng: Phân quyền truy cập (Admin vs Owner)
**Thầy hỏi:** *"Web Portal có chức năng đăng nhập, làm sao hệ thống phân biệt được đâu là Admin hệ thống, đâu là Chủ quán (Owner) để hiển thị dữ liệu cho đúng?"*

*   **Cách hoạt động:** Khi người dùng đăng nhập thành công, Server sinh ra một chuỗi mã hóa gọi là `JWT Token`, bên trong chuỗi này có chứa "Role" (Vai trò) của họ. Khi user vào trang Dashboard, Server check Role này. Nếu là Admin thì thấy hết mọi dữ liệu, nếu là Owner thì chỉ được filter (`.Where(l => l.OwnerEmail == User.Identity.Name)`) các địa điểm của riêng họ.
*   **Hàm/Service biểu diễn (Mở các file này lên):** 
    *   Xác thực: Code `[Authorize(Roles = "Admin,Owner")]` ở **dòng 8** file `TouristGuideWeb/Controllers/DashboardController.cs`.
    *   Kiểm tra Role filter dữ liệu: Code `User.IsInRole("Owner")` ở **dòng 30** file `DashboardController.cs`.
*   **Sơ đồ thể hiện:** 
    *   **Mục 7. Xác thực & Phân Quyền (RBAC)** (Sơ đồ Flowchart phân chia rõ các `[Authorize(Roles="Owner")]` và `Admin` trên Web Portal).

---

## 6. Tính năng: Đa ngôn ngữ (Localization & Audio TTS)
**Thầy hỏi:** *"App hỗ trợ đa ngôn ngữ như thế nào? Phải thu âm sẵn tất cả giọng đọc mp3 rồi nén vào App à?"*

*   **Cách hoạt động:** Không cần thu âm file mp3. Hệ thống chỉ cần lưu văn bản (Text) các ngôn ngữ dưới Database (bảng `TRANSLATION`). Khi user đổi ngôn ngữ, App sẽ truy vấn văn bản đó và đưa cho bộ xử lý **Native Text-to-Speech (AVSpeech trên iOS / Android TTS)** để AI của điện thoại tự động đọc thành tiếng theo đúng giọng chuẩn của ngôn ngữ đó. Việc này tiết kiệm dung lượng lưu trữ cực kỳ lớn.
*   **Hàm/Service biểu diễn (Mở các file này lên):** 
    *   Lấy chữ từ thư viện: Mở file `TouristGuideApp/Services/LocalizationResourceManager.cs`.
    *   Biến chữ thành âm: Mở file `TouristGuideApp/Services/AudioService.cs` (gọi hàm `TextToSpeech`).
*   **Sơ đồ thể hiện:** 
    *   **Mục 4. Database ERD** (Hiển thị bảng `TRANSLATION` lưu nhiều ngôn ngữ theo `PoiId`).
    *   **Mục 5. Audio & Đa Ngôn Ngữ** (Sơ đồ Graph mô tả hàm `TextToSpeech.SpeakAsync()` xử lý dữ liệu từ Local Context).

---

> 💡 **Mẹo khi trả lời hội đồng:** Hãy mở sẵn file mã nguồn (Visual Studio) ở tab `DashboardController.cs` và mở sẵn trang web `PRD_VinhKhanh_Guide.html`. Khi thầy hỏi tới đâu, bạn chiếu sơ đồ ở PRD lên, nói logic tổng quan trước, sau đó bật code lên chỉ đúng vào hàm đó. Đảm bảo đạt điểm tối đa!
