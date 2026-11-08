package com.vietroad.app.drive

import com.vietroad.app.data.RouteResponse
import com.vietroad.app.data.RouteStep
import kotlin.math.cos
import kotlin.math.roundToInt
import kotlin.math.sqrt

/** Tien do tren tuyen tai 1 thoi diem. */
data class NavProgress(
    val nextStepIndex: Int,     // chi dan sap toi (vi tri trong route.steps)
    val distanceToStepM: Int,   // con bao xa toi cho re do
    val remainingM: Int,        // con bao xa toi dich
    val remainingS: Int,        // con khoang bao nhieu giay
    val offRouteM: Int,         // xe cach tuyen bao xa (lon = di sai duong)
)

/**
 * Theo doi xe tren 1 tuyen duong. Thuan Kotlin (khong dung Android) -> unit test duoc.
 *
 * Y tuong: tuyen duong = day doan thang noi cac diem. Moi lan co GPS:
 *  1. Tim doan thang GAN xe nhat, "chieu" vi tri xe xuong doan do (hinh chieu vuong goc).
 *  2. Tu do biet xe da di duoc bao nhieu met tren tuyen ("traveled").
 *  3. Chi dan sap toi = chi dan dau tien nam xa hon "traveled".
 *  4. Khoang cach tu xe toi hinh chieu = xe lech tuyen bao xa (de phat hien di sai duong).
 */
class Navigator(val route: RouteResponse) {
    private val lats = DoubleArray(route.points.size) { route.points[it][0] }
    private val lngs = DoubleArray(route.points.size) { route.points[it][1] }

    // cum[i] = quang duong (met) tu diem dau toi diem i theo tuyen.
    private val cum = DoubleArray(lats.size).also { c ->
        for (i in 1 until lats.size) c[i] = c[i - 1] + Geo.distanceMeters(lats[i - 1], lngs[i - 1], lats[i], lngs[i])
    }
    private val totalM = cum.lastOrNull() ?: 0.0

    // Vi tri (met tinh tu dau tuyen) cua tung chi dan.
    private val stepAlong = DoubleArray(route.steps.size) { cum[route.steps[it].pointIndex.coerceIn(0, lats.size - 1)] }

    // Doan thang khop lan truoc: lan sau chi tim quanh do (nhanh hon, va khong nham sang
    // doan khac cua tuyen tinh co di ngang gan cho nay, vd duong vong quay lai).
    private var lastSegment = 0

    fun update(lat: Double, lng: Double): NavProgress {
        val lastStep = route.steps.lastIndex.coerceAtLeast(0)
        if (lats.size < 2) return NavProgress(lastStep, 0, 0, 0, 0)

        var best = nearestSegment(lat, lng, (lastSegment - 3).coerceAtLeast(0), (lastSegment + SEARCH_AHEAD).coerceAtMost(lats.size - 2))
        // Lech xa (GPS nhay, vua tinh lai duong...): tim tren toan tuyen.
        if (best.distanceM > 100) best = nearestSegment(lat, lng, 0, lats.size - 2)
        lastSegment = best.segment

        val traveled = cum[best.segment] + best.t * (cum[best.segment + 1] - cum[best.segment])
        val next = stepAlong.indices.firstOrNull { stepAlong[it] > traveled + PASSED_MARGIN_M } ?: lastStep
        val remaining = (totalM - traveled).coerceAtLeast(0.0)

        return NavProgress(
            nextStepIndex = next,
            distanceToStepM = (stepAlong.getOrElse(next) { totalM } - traveled).coerceAtLeast(0.0).roundToInt(),
            remainingM = remaining.roundToInt(),
            remainingS = if (totalM > 0) (route.durationS * remaining / totalM).roundToInt() else 0,
            offRouteM = best.distanceM.roundToInt(),
        )
    }

    private class Match(val segment: Int, val t: Double, val distanceM: Double)

    private fun nearestSegment(lat: Double, lng: Double, from: Int, to: Int): Match {
        // Doi sang toa do phang (met) quanh vi tri xe: du chinh xac trong vai km.
        val mPerDegLat = 110_574.0
        val mPerDegLng = 111_320.0 * cos(Math.toRadians(lat))
        var best = Match(from, 0.0, Double.MAX_VALUE)

        for (i in from..to) {
            val ax = (lngs[i] - lng) * mPerDegLng; val ay = (lats[i] - lat) * mPerDegLat
            val bx = (lngs[i + 1] - lng) * mPerDegLng; val by = (lats[i + 1] - lat) * mPerDegLat
            val dx = bx - ax; val dy = by - ay
            val len2 = dx * dx + dy * dy
            // t = vi tri hinh chieu tren doan (0 = dau doan, 1 = cuoi doan).
            val t = if (len2 == 0.0) 0.0 else (-(ax * dx + ay * dy) / len2).coerceIn(0.0, 1.0)
            val px = ax + t * dx; val py = ay + t * dy
            val d = sqrt(px * px + py * py)
            if (d < best.distanceM) best = Match(i, t, d)
        }
        return best
    }

    private companion object {
        const val SEARCH_AHEAD = 300
        const val PASSED_MARGIN_M = 3.0  // da toi sat cho re (< 3 m) coi nhu da qua
    }
}

/**
 * Phat hien di sai duong. Thuan Kotlin -> unit test duoc.
 *
 * Kho o luc BAT DAU: xe thuong dang trong bai do / ngo nho, cach con duong ma tuyen bat dau
 * vai chuc - vai tram met. Neu coi "cach tuyen > 40 m" la sai duong ngay thi tinh lai duong
 * mai mai (tuyen moi van bat dau tu con duong do). Vi vay:
 *  - Chua "vao tuyen" (chua lan nao sat tuyen): chi coi la sai khi xe DI XA THEM 150 m
 *    so voi luc bat dau (vd di nguoc huong).
 *  - Da vao tuyen: lech > 40 m (hoac hon, neu GPS kem chinh xac) la sai.
 * Can 3 lan GPS lien tiep sai moi tinh lai duong (tranh GPS nhay 1 lan).
 */
class OffRouteDetector {
    private var startOffsetM: Int? = null
    private var joined = false

    /** So lan GPS lien tiep thay xe lech tuyen. */
    var consecutiveOff = 0
        private set

    /** @return true neu lan GPS nay xe dang lech tuyen. */
    fun update(offRouteM: Int, accuracyM: Int?): Boolean {
        val threshold = maxOf(BASE_THRESHOLD_M, minOf((accuracyM ?: 0) * 1.5, 100.0))
        val start = startOffsetM ?: offRouteM.also { startOffsetM = it }
        if (offRouteM <= threshold) joined = true

        val off = if (joined) offRouteM > threshold else offRouteM > start + LEAVE_START_M
        consecutiveOff = if (off) consecutiveOff + 1 else 0
        return off
    }

    /** Doi sang tuyen moi: bat dau lai tu dau. */
    fun reset() {
        startOffsetM = null
        joined = false
        consecutiveOff = 0
    }

    companion object {
        const val BASE_THRESHOLD_M = 40.0
        const val LEAVE_START_M = 150
        const val REROUTE_AFTER = 3
    }
}

/**
 * Quyet dinh LUC NAO doc chi dan. Moi chi dan doc 2 lan:
 *  - bao truoc: "Sau 300 mét, rẽ trái vào Phố Huế" (xe nhanh thi bao som hon: 800 m)
 *  - tai cho re: "Rẽ trái vào Phố Huế"
 */
class NavigationVoice {
    private var farSpokenFor = -1
    private var nearSpokenFor = -1

    fun next(progress: NavProgress, step: RouteStep, speedKmh: Int?): String? {
        val i = progress.nextStepIndex
        val d = progress.distanceToStepM
        val fast = (speedKmh ?: 0) >= 60
        val nearM = when {
            step.type == "arrive" -> ARRIVE_M
            fast -> 120
            else -> 50
        }
        val farM = if (fast) 800 else 300

        if (d <= nearM) {
            if (nearSpokenFor == i) return null
            nearSpokenFor = i
            farSpokenFor = i
            return step.instruction
        }
        // Da qua gan (vd vua tinh lai duong ngay truoc cho re) thi khong bao truoc nua:
        // vai giay sau se doc cau "tai cho re", noi 2 cau sat nhau chi gay roi.
        if (d <= farM && d > nearM + 50 && farSpokenFor != i) {
            farSpokenFor = i
            val distance = NavFormat.spokenDistance(d)
            return if (step.type == "arrive") "Còn $distance nữa là đến nơi"
            else "Sau $distance, ${step.instruction.lowerFirst()}"
        }
        return null
    }

    /** Doi sang tuyen moi (sau khi tinh lai duong). */
    fun reset() {
        farSpokenFor = -1
        nearSpokenFor = -1
    }

    private fun String.lowerFirst() = replaceFirstChar { it.lowercaseChar() }

    companion object {
        /** Con cach dich chung nay (met) la coi nhu da den. */
        const val ARRIVE_M = 30
    }
}

/** Dinh dang khoang cach / thoi gian: cho man hinh va cho giong noi. */
object NavFormat {
    /** Hien thi: "350 m", "1,2 km", "125 km" (tu 100 km bo phan le). */
    fun distance(m: Int): String = when {
        m < 1000 -> "${(m / 10) * 10} m"
        m < 100_000 -> "${oneDecimal(m / 1000.0)} km"
        else -> "${(m + 500) / 1000} km"
    }

    /** Doc: "300 mét", "1,5 ki lô mét", "125 ki lô mét". Lam tron cho de nghe. */
    fun spokenDistance(m: Int): String = when {
        m < 1000 -> "${((m + 25) / 50 * 50).coerceAtLeast(50)} mét"
        m < 100_000 -> "${oneDecimal(m / 1000.0)} ki lô mét"
        else -> "${(m + 500) / 1000} ki lô mét"
    }

    /** "4 phút", "1 giờ 5 phút". */
    fun duration(seconds: Int): String {
        val minutes = ((seconds + 59) / 60).coerceAtLeast(1)
        return if (minutes < 60) "$minutes phút"
        else if (minutes % 60 == 0) "${minutes / 60} giờ"
        else "${minutes / 60} giờ ${minutes % 60} phút"
    }

    // 1.0 -> "1", 1.25 -> "1,3" (dau phay kieu Viet Nam).
    private fun oneDecimal(v: Double): String {
        val r = (v * 10).roundToInt()
        return if (r % 10 == 0) "${r / 10}" else "${r / 10},${r % 10}"
    }
}
