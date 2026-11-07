package com.vietroad.app.engine

import kotlin.math.abs
import kotlin.math.asin
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.floor
import kotlin.math.hypot
import kotlin.math.max
import kotlin.math.min
import kotlin.math.sin
import kotlin.math.sqrt

object Geo {
    private const val R = 6371000.0
    /** 1 ô lưới = 0.01° ≈ 1.1 km, dùng làm chỉ mục không gian trong SQLite */
    const val TILE_SCALE = 100.0

    private fun rad(d: Double) = Math.toRadians(d)

    fun distance(lat1: Double, lng1: Double, lat2: Double, lng2: Double): Double {
        val dLat = rad(lat2 - lat1)
        val dLng = rad(lng2 - lng1)
        val a = sin(dLat / 2) * sin(dLat / 2) +
            cos(rad(lat1)) * cos(rad(lat2)) * sin(dLng / 2) * sin(dLng / 2)
        return 2 * R * asin(min(1.0, sqrt(a)))
    }

    /** Góc phương vị từ điểm 1 tới điểm 2, 0..360. */
    fun bearing(lat1: Double, lng1: Double, lat2: Double, lng2: Double): Double {
        val p1 = rad(lat1)
        val p2 = rad(lat2)
        val dl = rad(lng2 - lng1)
        val y = sin(dl) * cos(p2)
        val x = cos(p1) * sin(p2) - sin(p1) * cos(p2) * cos(dl)
        return (Math.toDegrees(atan2(y, x)) + 360) % 360
    }

    /** Độ lệch nhỏ nhất giữa hai hướng, 0..180. */
    fun angleDiff(a: Double, b: Double): Double {
        val d = abs(a - b) % 360
        return if (d > 180) 360 - d else d
    }

    fun tileY(lat: Double) = floor(lat * TILE_SCALE).toInt()
    fun tileX(lng: Double) = floor(lng * TILE_SCALE).toInt()

    /** Độ lệch vĩ/kinh độ tương ứng bán kính r (m) quanh vĩ độ lat. */
    fun degreeSpan(lat: Double, r: Double): Pair<Double, Double> =
        Pair(r / 111320.0, r / (111320.0 * max(0.01, cos(rad(lat)))))

    /** Khoảng cách (m) từ điểm tới đoạn a-b, chiếu phẳng cục bộ. */
    fun distanceToSegment(lat: Double, lng: Double, a: DoubleArray, b: DoubleArray): Double {
        val kx = 111320.0 * cos(rad(lat))
        val ky = 110540.0
        val ax = (a[1] - lng) * kx
        val ay = (a[0] - lat) * ky
        val bx = (b[1] - lng) * kx
        val by = (b[0] - lat) * ky
        val dx = bx - ax
        val dy = by - ay
        val len2 = dx * dx + dy * dy
        var t = if (len2 > 0) -(ax * dx + ay * dy) / len2 else 0.0
        t = t.coerceIn(0.0, 1.0)
        return hypot(ax + t * dx, ay + t * dy)
    }

    data class Nearest(val distance: Double, val bearing: Double)

    fun nearestOnPolyline(lat: Double, lng: Double, points: List<DoubleArray>): Nearest {
        var best = Nearest(Double.MAX_VALUE, 0.0)
        for (i in 0 until points.size - 1) {
            val a = points[i]
            val b = points[i + 1]
            val d = distanceToSegment(lat, lng, a, b)
            if (d < best.distance) best = Nearest(d, bearing(a[0], a[1], b[0], b[1]))
        }
        return best
    }

    fun polylineLength(points: List<DoubleArray>): Double {
        var total = 0.0
        for (i in 0 until points.size - 1) {
            total += distance(points[i][0], points[i][1], points[i + 1][0], points[i + 1][1])
        }
        return total
    }

    data class AlongPoint(val lat: Double, val lng: Double, val bearing: Double)

    /** Điểm nằm cách đầu polyline [meters] mét, kèm hướng của đoạn chứa nó. */
    fun pointAt(points: List<DoubleArray>, meters: Double): AlongPoint? {
        if (points.size < 2) return null
        var remain = max(0.0, meters)
        for (i in 0 until points.size - 1) {
            val a = points[i]
            val b = points[i + 1]
            val len = distance(a[0], a[1], b[0], b[1])
            if (remain <= len || i == points.size - 2) {
                val t = if (len > 0) min(1.0, remain / len) else 0.0
                return AlongPoint(
                    a[0] + (b[0] - a[0]) * t,
                    a[1] + (b[1] - a[1]) * t,
                    bearing(a[0], a[1], b[0], b[1]),
                )
            }
            remain -= len
        }
        return null
    }
}
