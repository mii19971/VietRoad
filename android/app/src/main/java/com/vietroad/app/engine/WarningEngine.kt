package com.vietroad.app.engine

import kotlin.math.max
import kotlin.math.roundToInt

/**
 * Bộ máy cảnh báo: từ vị trí/hướng/tốc độ xe và dữ liệu lân cận, xác định
 * - các điểm cần cảnh báo ở PHÍA TRƯỚC, đúng CHIỀU đi và đúng LOẠI xe,
 * - giới hạn tốc độ hiện hành (ưu tiên vùng tốc độ, sau đó là biển P.127 vừa đi qua),
 * - trạng thái vượt tốc độ.
 * Không phụ thuộc Android → kiểm thử được bằng JUnit thuần.
 */
class WarningEngine(private val types: Map<String, SignType>) {

    data class Config(
        /** Điểm phải nằm trong ±aheadAngle so với hướng xe mới tính là phía trước */
        val aheadAngle: Double = 40.0,
        /** Xe cách tim đường ≤ khoảng này thì coi là đang đi trên vùng tốc độ */
        val zoneMatchDistance: Double = 25.0,
        val zoneAngle: Double = 40.0,
        /** Trong bán kính này coi như xe đang đi ngang qua biển */
        val signPassDistance: Double = 40.0,
        /** Vượt quá giới hạn bao nhiêu km/h thì mới cảnh báo */
        val overspeedTolerance: Int = 3,
        val dangerOverspeed: Int = 10,
        /** Dưới tốc độ này hướng GPS không tin cậy */
        val minHeadingSpeed: Double = 5.0,
    )

    var config = Config()

    fun evaluate(s: VehicleState, features: List<RoadFeature>, zones: List<SpeedZone>): EngineResult {
        val c = config
        val speed = max(0.0, s.speed)
        val heading = if (s.heading != null && speed >= c.minHeadingSpeed) s.heading else null
        val warnings = ArrayList<Warning>()
        var passed: RoadFeature? = null
        var passedDist = Double.MAX_VALUE

        for (f in features) {
            val type = types[f.typeCode] ?: continue
            if (!appliesTo(f, s.vehicleType) || !directionMatches(f, heading)) continue

            val d = Geo.distance(s.lat, s.lng, f.lat, f.lng)
            if (d > warnDistance(type, speed)) continue

            val isSpeedSign = f.typeCode == SPEED_SIGN || f.typeCode == END_SPEED_SIGN
            if (isSpeedSign && d <= c.signPassDistance && d < passedDist) {
                passed = f
                passedDist = d
            }

            // Ngoài bán kính "đang đi qua" thì điểm phải nằm phía trước đầu xe
            if (heading != null && d > c.signPassDistance &&
                Geo.angleDiff(heading, Geo.bearing(s.lat, s.lng, f.lat, f.lng)) > c.aheadAngle
            ) continue

            warnings += Warning(f, type, d.roundToInt(), fillTts(type, f))
        }
        warnings.sortBy { it.distance }

        var carry = s.lastSignLimit
        passed?.let { carry = if (it.typeCode == END_SPEED_SIGN) null else (it.speedLimit ?: carry) }

        val zl = zoneLimit(zones, s.lat, s.lng, heading, s.vehicleType)
        val limit = zl ?: carry
        val over = if (limit != null) speed - limit else 0.0
        val overspeed = limit != null && over > c.overspeedTolerance

        return EngineResult(
            speedLimit = limit,
            limitFromZone = zl != null,
            overspeed = overspeed,
            dangerOverspeed = overspeed && over >= c.dangerOverspeed,
            lastSignLimit = carry,
            warnings = warnings,
        )
    }

    /** Giới hạn tốc độ của vùng (đoạn đường) mà xe đang đi trên, nếu có. */
    fun zoneLimit(zones: List<SpeedZone>, lat: Double, lng: Double, heading: Double?, vehicleType: String): Int? {
        var best: SpeedZone? = null
        var bestDist = Double.MAX_VALUE
        for (z in zones) {
            val near = Geo.nearestOnPolyline(lat, lng, z.points)
            if (near.distance > config.zoneMatchDistance) continue
            if (heading != null) {
                val forward = Geo.angleDiff(heading, near.bearing) <= config.zoneAngle
                val backward = !z.oneway && Geo.angleDiff(heading, (near.bearing + 180) % 360) <= config.zoneAngle
                if (!forward && !backward) continue
            }
            if (near.distance < bestDist) {
                best = z
                bestDist = near.distance
            }
        }
        val z = best ?: return null
        return if (vehicleType == "truck" && z.maxSpeedTruck != null) z.maxSpeedTruck else z.maxSpeedCar
    }

    companion object {
        const val SPEED_SIGN = "P.127"
        const val END_SPEED_SIGN = "DP.134"

        fun appliesTo(f: RoadFeature, vehicleType: String): Boolean {
            if (f.vehicles.isBlank() || f.vehicles == "all") return true
            return f.vehicles.split(',').any { it.trim() == vehicleType }
        }

        fun directionMatches(f: RoadFeature, heading: Double?): Boolean {
            if (f.heading == null || heading == null) return true
            return Geo.angleDiff(heading, f.heading) <= f.headingTolerance
        }

        /** Đi càng nhanh càng phải cảnh báo sớm. */
        fun warnDistance(type: SignType, speedKmh: Double): Double =
            if (speedKmh > 80) type.warnDistance * 1.5 else type.warnDistance.toDouble()

        fun fillTts(type: SignType, f: RoadFeature): String =
            type.tts.replace("{limit}", f.speedLimit?.toString() ?: "")
    }
}
