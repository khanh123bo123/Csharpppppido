-- Database Initialization Script for TourGuide System
-- Run this after `dotnet ef database update`

-- Insert default admin user
-- Password: AdminPassword123!
-- Email: admin@tourguidequan4.com
INSERT INTO Users (Email, PasswordHash, FullName, Role, IsActive, CreatedAt, UpdatedAt)
VALUES (
    'admin@tourguidequan4.com',
    '$2a$11$PLACEHOLDER_BCRYPT_HASH', -- Replace with actual BCrypt hash
    'Administrator',
    'Admin',
    1,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
);

-- Sample Location Data (District 4, Ho Chi Minh City)
INSERT INTO Locations (Name, Description, Latitude, Longitude, QrCodeData, CreatedAt)
VALUES 
(
    'Quán Hủ Tiếu Nam Vang',
    'Hủ tiếu nước nổi tiếng Sài Gòn, được thành lập từ năm 1960 với công thức độc đáo',
    10.7769,
    106.6970,
    'hu-tieu-nam-vang',
    CURRENT_TIMESTAMP
),
(
    'Bánh Mì Hòa Mã',
    'Bánh mì chảy dịu từ sáu giờ sáng, bánh giòn vỏ mỏng, nhân phong phú',
    10.7742,
    106.6955,
    'banh-mi-hoa-ma',
    CURRENT_TIMESTAMP
),
(
    'Cơm Tấm Suất',
    'Cơm tấm với sườn nướng vàng thơm, trứng chiên, chả tôm',
    10.7715,
    106.6980,
    'com-tam-suat',
    CURRENT_TIMESTAMP
);

-- Sample Localizations (Multilingual Content)
-- Location 1: Quán Hủ Tiếu Nam Vang - Vietnamese
INSERT INTO Localizations (
    LocationId, LanguageCode, LocalizedName, LocalizedDescription,
    TtsVoiceCode, AudioGenerationStatus, CreatedAt, UpdatedAt
)
VALUES (
    1, 'vi-VN',
    'Quán Hủ Tiếu Nam Vang',
    'Hủ tiếu nước nổi tiếng Sài Gòn, được thành lập từ năm 1960 với công thức độc đáo của gia đình',
    'vi-VN-HoaiMyNeural',
    'pending',
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
);

-- Location 1: Quán Hủ Tiếu Nam Vang - English
INSERT INTO Localizations (
    LocationId, LanguageCode, LocalizedName, LocalizedDescription,
    TtsVoiceCode, AudioGenerationStatus, CreatedAt, UpdatedAt
)
VALUES (
    1, 'en-US',
    'Hu Tieu Nam Vang',
    'Hu Tieu Nam Vang is a famous Vietnamese noodle soup restaurant established in 1960 with unique family recipe',
    'en-US-AriaNeural',
    'pending',
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
);

-- Location 1: Quán Hủ Tiếu Nam Vang - Chinese
INSERT INTO Localizations (
    LocationId, LanguageCode, LocalizedName, LocalizedDescription,
    TtsVoiceCode, AudioGenerationStatus, CreatedAt, UpdatedAt
)
VALUES (
    1, 'zh-CN',
    '南旺粉汤店',
    '南旺粉汤店是西贡著名的粉汤餐厅，始建于1960年，拥有独特的家族配方',
    'zh-CN-XiaoxiaoNeural',
    'pending',
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
);

-- Location 1: Quán Hủ Tiếu Nam Vang - Japanese
INSERT INTO Localizations (
    LocationId, LanguageCode, LocalizedName, LocalizedDescription,
    TtsVoiceCode, AudioGenerationStatus, CreatedAt, UpdatedAt
)
VALUES (
    1, 'ja-JP',
    'フーティウ ナムヴァン',
    'フーティウナムヴァンはサイゴン周辺の有名なヌードルスープレストランで、1960年に創業しました',
    'ja-JP-NanamiNeural',
    'pending',
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
);

-- Location 1: Quán Hủ Tiếu Nam Vang - Korean
INSERT INTO Localizations (
    LocationId, LanguageCode, LocalizedName, LocalizedDescription,
    TtsVoiceCode, AudioGenerationStatus, CreatedAt, UpdatedAt
)
VALUES (
    1, 'ko-KR',
    '후티우 남방 식당',
    '후티우 남방 식당은 1960년에 설립된 사이공의 유명한 국수 수프 레스토랑입니다',
    'ko-KR-SunHiNeural',
    'pending',
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
);

-- Notes:
-- 1. Always use CURRENT_TIMESTAMP or GETDATE() for timestamps
-- 2. For User passwords, generate BCrypt hashes in C# code:
--    var hash = BCrypt.Net.BCrypt.HashPassword("password");
-- 3. BIT = boolean (1 = true, 0 = false)
-- 4. Use CURRENT_TIMESTAMP for AUTO timestamps in SQLite
-- 5. After inserting sample data, run audio generation warmup in code
