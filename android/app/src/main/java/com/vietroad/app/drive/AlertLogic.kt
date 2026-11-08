package com.vietroad.app.drive

import com.vietroad.app.data.Alert
import com.vietroad.app.data.AlertTypes
import kotlin.math.roundToInt

/** 1 canh bao kem khoang cach tinh tu vi tri HIEN TAI cua xe. */
data class AheadAlert(val alert: Alert, val distanceM: Int)

/**
 * Chon canh bao lien quan toi xe. Danh sach tu server tinh khoang cach tu luc tai
 * (co the da cach day vai tram met), nen app tu tinh lai moi lan GPS cap nhat.
 */
object AlertLogic {
    /** Diem nam trong +-60 do quanh huong xe dang di -> coi la "phia truoc". */
    const val AHEAD_CONE_DEG = 60.0

    /** Canh bao co huong: chi ap dung khi huong xe lech <= 60 do (dung chieu duong). */
    const val SAME_DIRECTION_DEG = 60.0

    /** Duoi toc do nay (~7 km/h) huong GPS nhay lung tung -> khong dung huong de loc. */
    const val MIN_SPEED_FOR_HEADING_MS = 2.0f

    /** Rat gan (dang di qua) thi luon tinh, du goc lech bao nhieu. */
    private const val PASSING_DISTANCE_M = 30.0

    /**
     * @param heading huong xe (do); null = dung yen / chua ro huong -> chi sap xep theo khoang cach.
     * @return canh bao lien quan, gan nhat truoc.
     */
    fun ahead(alerts: List<Alert>, lat: Double, lng: Double, heading: Double?): List<AheadAlert> =
        alerts
            .map { it to Geo.distanceMeters(lat, lng, it.lat, it.lng) }
            .filter { (alert, distance) ->
                if (heading == null || distance < PASSING_DISTANCE_M) return@filter true

                // 1. Diem phai o PHIA TRUOC: huong tu xe toi diem gan voi huong xe dang di.
                val bearingToAlert = Geo.bearingDegrees(lat, lng, alert.lat, alert.lng)
                val isAhead = Geo.angleDifference(bearingToAlert, heading) <= AHEAD_CONE_DEG

                // 2. Dung CHIEU: camera chieu nguoc lai (dung ben kia duong) thi bo qua.
                val sameDirection = alert.direction == null ||
                    Geo.angleDifference(alert.direction, heading) <= SAME_DIRECTION_DEG

                isAhead && sameDirection
            }
            .sortedBy { it.second }
            .map { (alert, distance) -> AheadAlert(alert, distance.roundToInt()) }

    /** Cau doc bang giong noi. Khoang cach lam tron 50 m cho de nghe. */
    fun speech(item: AheadAlert, near: Boolean): String {
        val label = AlertTypes.label(item.alert.type)
        val limit = item.alert.speedLimit?.let { ", giới hạn $it ki lô mét giờ" } ?: ""
        if (near) return "Sắp tới $label$limit"
        val meters = ((item.distanceM + 25) / 50) * 50
        return "Phía trước $meters mét có $label$limit"
    }
}

/**
 * Quyet dinh LUC NAO doc. Moi canh bao doc toi da 2 lan:
 *   - lan 1 khi con <= 500 m ("Phia truoc 450 met co camera...")
 *   - lan 2 khi con <= 150 m ("Sap toi camera...")
 * Nho id da doc de khong doc lap lai moi giay.
 */
class AnnouncementPolicy(
    private val farDistanceM: Int = 500,
    private val nearDistanceM: Int = 150,
) {
    private val stage = mutableMapOf<Int, Int>() // alertId -> 0 chua doc, 1 da doc xa, 2 da doc gan

    /** @return cau can doc, hoac null neu khong can doc gi. */
    fun next(nearest: AheadAlert?): String? {
        nearest ?: return null
        val id = nearest.alert.id
        val current = stage[id] ?: 0

        return when {
            nearest.distanceM <= nearDistanceM && current < 2 -> {
                stage[id] = 2
                AlertLogic.speech(nearest, near = true)
            }
            nearest.distanceM <= farDistanceM && current < 1 -> {
                stage[id] = 1
                AlertLogic.speech(nearest, near = false)
            }
            else -> null
        }
    }
}
