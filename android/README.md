# VietRoad: cảnh báo biển báo & tốc độ offline cho Android box

Ứng dụng Kotlin cảnh báo camera, biển báo và tốc độ **hoàn toàn không cần Internet**:
- Ứng dụng **không xin quyền `INTERNET`**.
- Dữ liệu nằm trong SQLite trên máy.
- Vị trí lấy từ GPS (vệ tinh, không cần mạng).
- Giọng đọc dùng TTS offline của Android.

## Tính năng

| Tính năng | Mô tả |
|---|---|
| Cảnh báo phía trước | Camera tốc độ/đèn đỏ, biển cấm, biển nguy hiểm, điểm đen, trạm thu phí... (35 loại, mã theo QCVN 41:2019) |
| Đúng chiều, đúng xe | Chỉ cảnh báo điểm nằm **trước đầu xe**, **đúng chiều đi** (theo hướng GPS) và **đúng loại xe** (ô tô/xe tải/xe khách) |
| Giới hạn tốc độ | Theo đoạn đường (vùng tốc độ), hoặc nhớ biển P.127 vừa đi qua; biển DP.134 sẽ huỷ giới hạn |
| Vượt tốc độ | Nền chuyển đỏ, bíp và đọc cảnh báo (8 giây/lần nếu vượt ≥ 10 km/h, 15 giây/lần nếu vượt ít hơn) |
| Giọng nói tiếng Việt | "Còn 500 mét, phía trước có camera giám sát tốc độ", đọc nhắc lại khi đã rất gần với các điểm quan trọng |
| Bong bóng nổi | Hiển thị tốc độ, biển giới hạn và cảnh báo gần nhất đè lên app bản đồ/nhạc |
| Android box | Tự chạy khi bật máy (bật chìa khoá), giao diện ngang chữ lớn, điều khiển được bằng remote, hỗ trợ Android 5.0 trở lên |
| Thêm điểm trên đường | Tài xế bấm "Thêm điểm" để lưu camera/biển mới tại vị trí hiện tại, gắn theo chiều đang chạy |
| Cập nhật dữ liệu offline | Nhập/xuất JSON hoặc GeoJSON qua USB/thẻ nhớ, hoặc chép tệp vào thư mục import |
| Mô phỏng | Chạy thử trên tuyến mẫu, không cần GPS, có đoạn tự tăng tốc để thử cảnh báo vượt tốc độ |

## Build

Yêu cầu: Android Studio (Koala trở lên) hoặc JDK 17 + Android SDK 34.

1. Android Studio → **Open** → chọn thư mục `android/`, Studio sẽ tự tải Gradle 8.7 và các thư viện.
2. **Run** lên box/điện thoại, hoặc **Build → Build APK(s)**.

Dùng dòng lệnh (sau khi đã có Gradle wrapper, Studio sẽ tự tạo, hoặc chạy `gradle wrapper`):

```
./gradlew assembleRelease        # APK: app/build/outputs/apk/release/
./gradlew testDebugUnitTest      # test bộ máy cảnh báo
adb install -r app/build/outputs/apk/release/app-release.apk
```

> Bản release đang được ký bằng khoá debug để tiện cài thử. Trước khi phân phối, hãy tạo khoá phát hành riêng.

## Cài đặt trên Android box

1. Cài APK, mở app, cấp **quyền vị trí** (và quyền thông báo nếu máy hỏi).
2. Vào **Cài đặt → Cài giọng tiếng Việt** và tải gói giọng *Tiếng Việt* của Google TTS. Bước này cần mạng **một lần duy nhất**; sau đó đọc offline.
3. Bật **Hiện bong bóng tốc độ** nếu muốn dùng song song với app bản đồ.
4. Một số box (Android 10 trở lên) chặn app tự chạy nền khi khởi động. Nếu vậy, hãy vào cài đặt pin/khởi động của box để cho phép VietRoad tự khởi động.

## Dữ liệu

> ⚠️ **Dữ liệu đi kèm (`app/src/main/assets/vietroad_data.json`) chỉ là DỮ LIỆU MẪU** (41 điểm, 4 đoạn tốc độ ở Hà Nội, TP.HCM, Đà Nẵng, toạ độ gần đúng). Dữ liệu này dùng để chạy thử và mô phỏng, **không phản ánh biển báo thực tế**. Trước khi phát hành, cần thay bằng dữ liệu khảo sát thật.

Có 2 cách thay dữ liệu:
- **Đóng gói vào app**: thay tệp `vietroad_data.json` rồi build lại. Tệp có thể sinh từ script, xem `tools/gen-sample-data.mjs`.
- **Cập nhật trên box (offline)**: Cài đặt → *Nhập từ tệp*, hoặc chép tệp vào `Android/data/com.vietroad.app/files/import/` rồi bấm *Nhập từ thư mục import*.

### Định dạng JSON

```json
{
  "features": [
    { "uid": "3f1c2a9e-0b7d-4c5e-9a41-2d8f6e1b7c30",
      "type_code": "CAM_SPEED", "lat": 21.0123, "lng": 105.6, "heading": 92,
      "speed_limit": null, "vehicles": "all", "road_name": "Đại lộ Thăng Long", "province": "Hà Nội",
      "data_source": "survey-2025-hn", "confidence": 0.9, "verified_at": "2025-03-01",
      "expires_at": null, "status": "active" },
    { "type_code": "P.127", "lat": 21.0134, "lng": 105.53, "heading": 92, "speed_limit": 100 }
  ],
  "zones": [
    { "uid": "8a0d4e2b-6c1f-4b9a-b3e7-5f2c9d8a1e46",
      "name": "Đại lộ Thăng Long", "points": [[21.0136, 105.527], [21.0125, 105.56]],
      "max_speed_car": 100, "max_speed_truck": 90, "oneway": false,
      "data_source": "osm", "confidence": 0.6, "verified_at": null, "status": "active" }
  ]
}
```

- `heading`: hướng xe đi tới mà biển có hiệu lực (0 = Bắc, 90 = Đông, 180 = Nam, 270 = Tây). Để `null` nếu biển áp dụng cho mọi hướng.
- `vehicles`: `all` hoặc danh sách `car,truck,bus,motorbike`.
- Ứng dụng cũng nhận **GeoJSON** FeatureCollection: mỗi `Point` là một điểm cảnh báo, mỗi `LineString` có `max_speed_car` là một đoạn tốc độ. `id` của Feature được dùng làm `uid`.
- Danh sách mã `type_code` hợp lệ: xem `data/SignCatalog.kt`. Bản ghi có mã lạ, toạ độ sai hoặc trường quản lý sai định dạng sẽ bị bỏ qua và được đếm vào số "bỏ qua".

#### Trường quản lý dữ liệu (tuỳ chọn, dùng chung cho `features` và `zones`)

| Trường | Ý nghĩa | Mặc định nếu thiếu |
|---|---|---|
| `uid` | Mã định danh duy nhất (tối đa 64 ký tự, nên dùng UUID). Nhập bản ghi có `uid` đã tồn tại sẽ **cập nhật** bản ghi đó thay vì thêm mới. | Mã ổn định sinh từ loại + toạ độ + hướng (vùng: tên + điểm đầu/cuối) |
| `data_source` | Nguồn dữ liệu, ví dụ `osm`, `survey-2025-hn`, `partner-x`, `user` | `null` (không rõ) |
| `confidence` | Mức độ tin cậy, từ 0 đến 1 | `0.5` |
| `verified_at` | Ngày xác minh: `"2025-03-01"`, `"2025-03-01T08:00:00Z"` hoặc số giây/mili giây epoch (giờ UTC) | `null` (chưa xác minh) |
| `expires_at` | Hết hiệu lực kể từ thời điểm này, cùng định dạng với `verified_at` | `null` (không hết hạn) |
| `status` | `active`: đang cảnh báo · `pending`: chờ xác minh, chưa cảnh báo · `inactive`: đã gỡ | `active` |

Chỉ bản ghi `status = active` và chưa tới `expires_at` mới được dùng để cảnh báo. Để gỡ một điểm trên box, hãy nhập lại bản ghi đó (cùng `uid`) với `"status": "inactive"`. Dữ liệu mẫu đi kèm app có `data_source = "sample"` và `confidence = 0.2`.

## Cấu trúc mã

```
app/src/main/java/com/vietroad/app/
├── engine/            # Thuần Kotlin, không phụ thuộc Android (có unit test)
│   ├── Models.kt          SignType, RoadFeature, SpeedZone, EngineResult...
│   ├── Geo.kt             Khoảng cách, góc phương vị, khoảng cách tới đường
│   └── WarningEngine.kt   Lọc phía trước/đúng chiều/đúng xe, giới hạn tốc độ, vượt tốc
├── data/
│   ├── FeatureDb.kt       SQLite + chỉ mục lưới 0.01° để tra cứu điểm quanh xe
│   ├── DataIO.kt          Nhập/xuất JSON & GeoJSON
│   ├── SignCatalog.kt     35 loại biển/điểm, câu đọc TTS, khoảng cách cảnh báo
│   └── Prefs.kt           Cài đặt
├── service/
│   ├── DriveService.kt    Foreground service: GPS 1 Hz, bộ đệm 3 km quanh xe, mô phỏng
│   ├── Announcer.kt       TTS tiếng Việt + bíp, chống đọc lặp
│   ├── OverlayBubble.kt   Bong bóng nổi
│   ├── BootReceiver.kt    Tự chạy khi bật máy
│   └── DriveState.kt      Kênh trạng thái service → UI
└── ui/
    ├── MainActivity.kt    Màn hình lái xe
    ├── SettingsActivity.kt
    └── SignView.kt        Vẽ biển báo bằng Canvas (không cần ảnh)
```
