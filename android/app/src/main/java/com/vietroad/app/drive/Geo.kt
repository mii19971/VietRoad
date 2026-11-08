package com.vietroad.app.drive

import kotlin.math.abs
import kotlin.math.asin
import kotlin.math.atan2
import kotlin.math.cos
import kotlin.math.pow
import kotlin.math.sin
import kotlin.math.sqrt

/** Ham hinh hoc tren mat cau Trai Dat. Thuan Kotlin -> viet unit test duoc. */
object Geo {
    private const val EARTH_RADIUS_M = 6_371_000.0

    private fun Double.toRad() = Math.toRadians(this)

    /** Khoang cach (met) theo cong thuc Haversine - giong GeoUtils.DistanceMeters ben server. */
    fun distanceMeters(lat1: Double, lng1: Double, lat2: Double, lng2: Double): Double {
        val dLat = (lat2 - lat1).toRad()
        val dLng = (lng2 - lng1).toRad()
        val a = sin(dLat / 2).pow(2) + cos(lat1.toRad()) * cos(lat2.toRad()) * sin(dLng / 2).pow(2)
        return 2 * EARTH_RADIUS_M * asin(sqrt(a))
    }

    /** Huong (do, 0 = Bac, 90 = Dong) di tu diem 1 toi diem 2. */
    fun bearingDegrees(lat1: Double, lng1: Double, lat2: Double, lng2: Double): Double {
        val dLng = (lng2 - lng1).toRad()
        val y = sin(dLng) * cos(lat2.toRad())
        val x = cos(lat1.toRad()) * sin(lat2.toRad()) - sin(lat1.toRad()) * cos(lat2.toRad()) * cos(dLng)
        return (Math.toDegrees(atan2(y, x)) + 360) % 360
    }

    /** Goc lech nho nhat giua 2 huong, 0..180. Vd 350 va 10 -> 20. */
    fun angleDifference(a: Double, b: Double): Double {
        val diff = abs(a - b) % 360
        return if (diff > 180) 360 - diff else diff
    }
}
