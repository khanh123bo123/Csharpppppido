-- Database Seed Script for TourGuide System
-- Phố Ẩm Thực Vĩnh Khánh, Quận 4, TP.HCM
-- Run: sqlite3 tourguide.db < DATABASE_SEED.sql

-- Clear existing data to avoid duplicates
DELETE FROM "Localizations";
DELETE FROM "Locations";
DELETE FROM "TourLocations";
DELETE FROM "Tours";
DELETE FROM "sqlite_sequence" WHERE "name" IN ('Locations', 'Tours', 'TourLocations', 'Localizations');

-- ============================================
-- LOCATIONS - Ẩm thực & Địa điểm Quận 4
-- ============================================

INSERT INTO "Locations" ("Name", "Description", "Latitude", "Longitude", "QrCodeData", "Category", "Address", "PhoneNumber", "ImageUrl", "CreatedAt")
VALUES 
(
    'Ốc Đào',
    'Quán ốc nổi tiếng nhất phố Vĩnh Khánh, thành lập từ năm 1998. Chuyên các món ốc hấp, ốc nướng mỡ hành, nghêu xào với hơn 50 món hải sản tươi sống. Không gian thoáng mát, phục vụ từ 4 giờ chiều đến khuya.',
    10.7578,
    106.6978,
    'LOC_ocdao001',
    'Hải sản',
    '212 Vĩnh Khánh, Phường 10, Quận 4, TP.HCM',
    '028 3826 1970',
    'https://images.unsplash.com/photo-1559847844-5315695dadae?w=400',
    CURRENT_TIMESTAMP
),
(
    'Ốc Oanh',
    'Quán ốc bình dân đông khách bậc nhất Vĩnh Khánh. Nổi tiếng với ốc len xào dừa, sò điệp nướng mỡ hành, và các loại ốc rang muối ớt cay nồng. Giá cả phải chăng, phù hợp cho nhóm bạn.',
    10.7581,
    106.6973,
    'LOC_ocoanh002',
    'Hải sản',
    '234 Vĩnh Khánh, Phường 10, Quận 4, TP.HCM',
    '028 3940 4068',
    'https://images.unsplash.com/photo-1615141982883-c7ad0e69fd62?w=400',
    CURRENT_TIMESTAMP
),
(
    'Quán Hủ Tiếu Nam Vang Liến Húa',
    'Hủ tiếu nước nổi tiếng Sài Gòn, thành lập từ năm 1960 với công thức gia truyền 3 đời. Nước lèo ngọt thanh từ xương heo, tôm khô, mực khô. Sợi hủ tiếu dai mềm kết hợp với thịt bằm, gan, tôm tươi.',
    10.7590,
    106.6965,
    'LOC_hutieu003',
    'Quán ăn',
    '66 Vĩnh Khánh, Phường 8, Quận 4, TP.HCM',
    '028 3826 5488',
    'https://images.unsplash.com/photo-1582878826629-29b7ad1cdc43?w=400',
    CURRENT_TIMESTAMP
),
(
    'Bánh Tráng Trộn Bà Già',
    'Xe bán bánh tráng trộn nổi tiếng ở đầu đường Vĩnh Khánh. Bánh tráng giòn rụm trộn với xoài xanh, rau răm, đậu phộng rang, khô bò, trứng cút và nước sốt chua cay đặc trưng Sài Gòn.',
    10.7575,
    106.6982,
    'LOC_banhtrang004',
    'Ăn vặt',
    '180 Vĩnh Khánh, Phường 10, Quận 4, TP.HCM',
    NULL,
    'https://images.unsplash.com/photo-1455619452474-d2be8b1e70cd?w=400',
    CURRENT_TIMESTAMP
),
(
    'Cơm Tấm Bụi Sài Gòn',
    'Cơm tấm sườn nướng than hồng, vàng ươm thơm phức. Phần ăn gồm sườn cốt lết nướng, bì, chả, trứng ốp la. Nước mắm pha chua ngọt hoàn hảo. Mở cửa từ 6h sáng đến 10h tối.',
    10.7568,
    106.6990,
    'LOC_comtam005',
    'Quán ăn',
    '290 Vĩnh Khánh, Phường 10, Quận 4, TP.HCM',
    '0909 123 456',
    'https://images.unsplash.com/photo-1569058242567-93de6f36f8eb?w=400',
    CURRENT_TIMESTAMP
),
(
    'Lẩu Dê Tư Trung',
    'Quán lẩu dê lâu đời nhất Quận 4. Dê núi tươi nhập hàng ngày từ Ninh Thuận. Menu có lẩu dê, dê nướng tảng, cà ri dê, dê hấp hành. Không gian rộng thoáng, phục vụ nhóm đông.',
    10.7595,
    106.6960,
    'LOC_laude006',
    'Nhà hàng',
    '45 Vĩnh Khánh, Phường 8, Quận 4, TP.HCM',
    '028 3825 7899',
    'https://images.unsplash.com/photo-1504674900247-0877df9cc836?w=400',
    CURRENT_TIMESTAMP
),
(
    'Bún Bò Huế Cô Liên',
    'Bún bò Huế chuẩn vị miền Trung tại Sài Gòn. Nước lèo ninh từ xương ống, sả, ruốc Huế, cay nồng mà đậm đà. Bún to sợi, chân giò, bò tái, giò heo thơm lừng. Quán nhỏ nhưng luôn đông khách.',
    10.7572,
    106.6975,
    'LOC_bunbo007',
    'Quán ăn',
    '156 Vĩnh Khánh, Phường 10, Quận 4, TP.HCM',
    '0938 765 432',
    'https://images.unsplash.com/photo-1576577445504-6af96477db52?w=400',
    CURRENT_TIMESTAMP
),
(
    'Chè Bưởi Chú Tám',
    'Quán chè truyền thống với hơn 20 loại chè. Nổi bật nhất là chè bưởi, chè ba màu, chè đậu hủ nước đường. Nguyên liệu tươi tự nhiên, nấu thủ công mỗi ngày. Giải khát hoàn hảo sau bữa hải sản.',
    10.7583,
    106.6968,
    'LOC_chebuoi008',
    'Ăn vặt',
    '98 Vĩnh Khánh, Phường 8, Quận 4, TP.HCM',
    NULL,
    'https://images.unsplash.com/photo-1551024506-0bccd828d307?w=400',
    CURRENT_TIMESTAMP
),
(
    'Bánh Khọt Vũng Tàu',
    'Bánh khọt giòn rụm, nhân tôm tươi, ăn kèm rau sống cuốn bánh tráng. Nước mắm pha kiểu miền Nam ngọt thanh. Làm tại chỗ trên khuôn gang nóng hổi. Một trong những món ăn đường phố được yêu thích nhất.',
    10.7586,
    106.6985,
    'LOC_banhkhot009',
    'Ăn vặt',
    '168 Vĩnh Khánh, Phường 10, Quận 4, TP.HCM',
    '0912 345 678',
    'https://images.unsplash.com/photo-1562967916-eb82221dfb44?w=400',
    CURRENT_TIMESTAMP
),
(
    'Nướng & Beer Garden',
    'Quán nướng BBQ phong cách sân vườn. Thịt bò Úc nướng, cánh gà nướng mật ong, hải sản nướng. Bia tươi từ thùng, cocktail. Nhạc sống cuối tuần. Không gian lý tưởng cho nhóm bạn và gia đình.',
    10.7565,
    106.6995,
    'LOC_nuongbeer010',
    'Nhà hàng',
    '310 Vĩnh Khánh, Phường 10, Quận 4, TP.HCM',
    '028 3940 8888',
    'https://images.unsplash.com/photo-1555939594-58d7cb561ad1?w=400',
    CURRENT_TIMESTAMP
),
(
    'Phở Bò Tái Lăn Cô Ba',
    'Phở bò truyền thống Sài Gòn với nước lèo trong vắt, ninh xương 12 tiếng. Bò tái mềm, nạm gầu béo ngậy. Quẩy giòn ăn kèm. Mở cửa lúc 5h sáng, thường hết hàng trước 10h.',
    10.7600,
    106.6955,
    'LOC_phobo011',
    'Quán ăn',
    '28 Vĩnh Khánh, Phường 8, Quận 4, TP.HCM',
    '0901 234 567',
    'https://images.unsplash.com/photo-1503764654157-72d979d9af2f?w=400',
    CURRENT_TIMESTAMP
),
(
    'Trà Sữa Bobapop',
    'Chuỗi trà sữa với hơn 30 loại thức uống. Trà sữa trân châu đường đen, matcha latte, kem cheese. Topping phong phú: trân châu, thạch, pudding. Không gian trẻ trung, wifi mạnh.',
    10.7570,
    106.6988,
    'LOC_trasua012',
    'Giải khát',
    '200 Vĩnh Khánh, Phường 10, Quận 4, TP.HCM',
    '0888 999 000',
    'https://images.unsplash.com/photo-1558857563-b371033873b8?w=400',
    CURRENT_TIMESTAMP
);

-- ============================================
-- TOURS - Lộ trình ẩm thực
-- ============================================

INSERT INTO "Tours" ("Name", "Description", "EstimatedDurationMinutes", "EstimatedDistanceKm", "IsActive", "CreatedAt", "UpdatedAt")
VALUES 
(
    'Tour Ẩm Thực Vĩnh Khánh Buổi Tối',
    'Khám phá 5 quán ăn nổi tiếng nhất phố ẩm thực Vĩnh Khánh trong 3 tiếng. Bắt đầu từ ốc, hải sản, đến lẩu dê và kết thúc bằng chè ngọt.',
    180,
    2.5,
    1,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
),
(
    'Tour Ăn Sáng Quận 4',
    'Bữa sáng Sài Gòn đậm vị với phở, hủ tiếu, cơm tấm. Tour kéo dài 2 tiếng, đi bộ qua các con hẻm đặc trưng.',
    120,
    1.8,
    1,
    CURRENT_TIMESTAMP,
    CURRENT_TIMESTAMP
);

-- Tour 1: Tour Ẩm Thực Buổi Tối (5 điểm)
INSERT INTO "TourLocations" ("TourId", "LocationId", "OrderIndex")
VALUES 
(1, 1, 1),  -- Ốc Đào
(1, 2, 2),  -- Ốc Oanh
(1, 6, 3),  -- Lẩu Dê Tư Trung
(1, 10, 4), -- Nướng & Beer
(1, 8, 5);  -- Chè Bưởi Chú Tám

-- Tour 2: Tour Ăn Sáng (4 điểm)
INSERT INTO "TourLocations" ("TourId", "LocationId", "OrderIndex")
VALUES 
(2, 11, 1), -- Phở Bò Tái Lăn
(2, 3, 2),  -- Hủ Tiếu Nam Vang
(2, 5, 3),  -- Cơm Tấm Bụi
(2, 9, 4);  -- Bánh Khọt Vũng Tàu
