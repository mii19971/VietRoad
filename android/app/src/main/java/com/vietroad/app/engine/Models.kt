package com.vietroad.app.engine

enum class Category(val label: String) {
    CAMERA("Camera giao thông"),
    SPEED("Tốc độ"),
    PROHIBITION("Biển cấm"),
    DANGER("Biển nguy hiểm"),
    MANDATORY("Biển hiệu lệnh"),
    HAZARD("Điểm nguy hiểm"),
    INFO("Chỉ dẫn / Tiện ích"),
}

/** Loại biển/điểm. [tts] có thể chứa {limit} để chèn giá trị tốc độ của điểm. */
data class SignType(
    val code: String,
    val name: String,
    val category: Category,
    val tts: String,
    /** 1 = quan trọng nhất */
    val priority: Int,
    /** Khoảng cách bắt đầu cảnh báo (m) ở tốc độ ≤ 80 km/h */
    val warnDistance: Int,
)

/** Một điểm cảnh báo trên đường: biển báo, camera, điểm đen, trạm thu phí... */
data class RoadFeature(
    val id: Long,
    val typeCode: String,
    val lat: Double,
    val lng: Double,
    /** Hướng xe đi tới mà điểm có hiệu lực (0 = Bắc, 90 = Đông). null = mọi hướng. */
    val heading: Double?,
    val headingTolerance: Double = 45.0,
    val speedLimit: Int? = null,
    /** "all" hoặc danh sách "car,truck,bus,motorbike" */
    val vehicles: String = "all",
    val roadName: String? = null,
    val note: String? = null,
)

/** Đoạn đường có giới hạn tốc độ. points = danh sách [lat, lng]. */
data class SpeedZone(
    val id: Long,
    val name: String?,
    val points: List<DoubleArray>,
    val maxSpeedCar: Int,
    val maxSpeedTruck: Int?,
    val oneway: Boolean,
)

data class VehicleState(
    val lat: Double,
    val lng: Double,
    val heading: Double?,
    /** km/h */
    val speed: Double,
    val vehicleType: String = "car",
    /** Giới hạn ghi nhớ từ biển tốc độ gần nhất đã đi qua */
    val lastSignLimit: Int? = null,
)

data class Warning(
    val feature: RoadFeature,
    val type: SignType,
    val distance: Int,
    val tts: String,
)

data class EngineResult(
    val speedLimit: Int?,
    val limitFromZone: Boolean,
    val overspeed: Boolean,
    val dangerOverspeed: Boolean,
    /** Giá trị cần lưu lại và truyền vào lần đánh giá tiếp theo */
    val lastSignLimit: Int?,
    /** Sắp xếp theo khoảng cách tăng dần */
    val warnings: List<Warning>,
)
