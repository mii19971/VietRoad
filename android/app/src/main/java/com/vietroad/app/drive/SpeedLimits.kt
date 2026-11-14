package com.vietroad.app.drive

import com.vietroad.app.data.RoadSpeedSegment
import kotlin.math.abs
import kotlin.math.cos
import kotlin.math.sqrt

/**
 * Tim doan duong xe dang di -> gioi han toc do hien tai. Thuan Kotlin -> unit test duoc.
 *
 * Moi doan = day diem noi lien. Xe "o tren" doan neu:
 *  - cach doan <= 25 m (sai so GPS + be rong duong), va
 *  - huong xe trung huong doan (lech <= 45 do). Duong 1 chieu: chi dung chieu; 2 chieu: ca 2 chieu.
 *    Nho vay o nut giao, duong cao toc va duong gom chay song song / cat ngang khong bi nham.
 * Chong "nhay" o cho 2 duong gan nhau: dang o doan nao thi giu doan do khi con trong 35 m,
 * tru khi doan khac gan hon han (hon 10 m).
 */
class SpeedLimitMatcher(
    private val maxDistanceM: Double = 25.0,
    private val keepDistanceM: Double = 35.0,
) {
    private var currentId: Long? = null

    fun match(segments: List<RoadSpeedSegment>, lat: Double, lng: Double, heading: Double?): RoadSpeedSegment? {
        var best: RoadSpeedSegment? = null
        var bestDist = Double.MAX_VALUE
        var current: RoadSpeedSegment? = null
        var currentDist = Double.MAX_VALUE

        for (seg in segments) {
            val d = distanceTo(seg, lat, lng, heading) ?: continue
            if (d < bestDist) { best = seg; bestDist = d }
            if (seg.id == currentId) { current = seg; currentDist = d }
        }

        val chosen = when {
            current != null && currentDist <= keepDistanceM && bestDist > currentDist - 10 -> current
            best != null && bestDist <= maxDistanceM -> best
            else -> null
        }
        currentId = chosen?.id
        return chosen
    }

    fun reset() { currentId = null }

    /** Khoang cach (m) tu xe toi doan, chi tinh cac khuc cung huong xe. null = khong khuc nao hop. */
    private fun distanceTo(seg: RoadSpeedSegment, lat: Double, lng: Double, heading: Double?): Double? {
        val p = seg.points
        if (p.size < 2) return null
        val mPerDegLat = 110_574.0
        val mPerDegLng = 111_320.0 * cos(Math.toRadians(lat))
        var best: Double? = null

        for (i in 0 until p.size - 1) {
            val aLat = p[i][0]; val aLng = p[i][1]; val bLat = p[i + 1][0]; val bLng = p[i + 1][1]
            // Bo nhanh khuc o qua xa (~200 m) truoc khi tinh chinh xac.
            if (minOf(aLat, bLat) - lat > 0.002 || lat - maxOf(aLat, bLat) > 0.002) continue
            if (minOf(aLng, bLng) - lng > 0.002 || lng - maxOf(aLng, bLng) > 0.002) continue

            if (heading != null) {
                val bearing = Geo.bearingDegrees(aLat, aLng, bLat, bLng)
                val diff = angleDiff(heading, bearing)
                val sameDirection = if (seg.oneway) diff <= 45 else minOf(diff, 180 - diff) <= 45
                if (!sameDirection) continue
            }

            // Hinh chieu vuong goc cua xe len khuc AB (toa do phang, goc tai xe).
            val ax = (aLng - lng) * mPerDegLng; val ay = (aLat - lat) * mPerDegLat
            val bx = (bLng - lng) * mPerDegLng; val by = (bLat - lat) * mPerDegLat
            val dx = bx - ax; val dy = by - ay
            val len2 = dx * dx + dy * dy
            val t = if (len2 == 0.0) 0.0 else (-(ax * dx + ay * dy) / len2).coerceIn(0.0, 1.0)
            val px = ax + t * dx; val py = ay + t * dy
            val d = sqrt(px * px + py * py)
            if (best == null || d < best) best = d
        }
        return best
    }

    private fun angleDiff(a: Double, b: Double): Double {
        val d = abs(a - b) % 360
        return if (d > 180) 360 - d else d
    }
}

/**
 * Quyet dinh LUC NAO canh bao qua toc do:
 *  - Chi canh bao khi vuot gioi han hon 5 km/h (dong ho xe / GPS lech vai km/h la binh thuong).
 *  - Noi 1 lan; van con qua toc do thi nhac lai sau 60 giay.
 *  - Giam xuong <= gioi han thi coi nhu het lan qua toc do (lan sau vuot lai se noi ngay).
 *  - Gioi han doi (vao doan duong khac) -> danh gia lai tu dau.
 */
class OverspeedPolicy(
    private val toleranceKmh: Int = 5,
    private val repeatMs: Long = 60_000,
) {
    private var warnedAt: Long? = null
    private var lastLimit: Int? = null

    fun isOver(speedKmh: Int?, limit: Int?): Boolean =
        speedKmh != null && limit != null && speedKmh > limit + toleranceKmh

    fun next(speedKmh: Int?, limit: Int?, nowMs: Long): String? {
        if (limit != lastLimit) { lastLimit = limit; warnedAt = null }
        if (speedKmh == null || limit == null) return null

        if (!isOver(speedKmh, limit)) {
            if (speedKmh <= limit) warnedAt = null
            return null
        }
        val last = warnedAt
        if (last != null && nowMs - last < repeatMs) return null
        warnedAt = nowMs
        return "Bạn đang chạy $speedKmh ki lô mét giờ, quá tốc độ cho phép $limit"
    }
}
