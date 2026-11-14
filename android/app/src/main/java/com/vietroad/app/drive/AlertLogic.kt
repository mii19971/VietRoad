package com.vietroad.app.drive

import com.vietroad.app.data.Alert
import com.vietroad.app.data.AlertTypes
import kotlin.math.roundToInt
import kotlin.math.sin

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

    /** Diem co truc duong (duong ngang, go giam toc): xe phai chay lech truc <= 45 do (chieu nao cung duoc). */
    const val ROAD_AXIS_DEG = 45.0

    /**
     * Diem co truc duong phai nam gan "duong thang phia truoc mui xe": lech ngang <= 60 m.
     * Chan duong ngang tren ngo SONG SONG cach vai tram met (cung huong, van nam trong hinh non 60 do).
     */
    const val ROAD_AXIS_MAX_OFFSET_M = 60.0

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
                // 0. Diem tren 1 con duong khac (vd duong ngang tren ngo cat ngang pho dang di):
                //    bo qua, KE CA khi dang di sat ben canh (khong dung ngoai le "dang di qua").
                if (heading != null && alert.direction != null && AlertTypes.hasRoadAxis(alert.type) &&
                    !Geo.sameAxis(alert.direction, heading, ROAD_AXIS_DEG)) return@filter false

                if (heading == null || distance < PASSING_DISTANCE_M) return@filter true

                // 1. Diem phai o PHIA TRUOC: huong tu xe toi diem gan voi huong xe dang di.
                val bearingToAlert = Geo.bearingDegrees(lat, lng, alert.lat, alert.lng)
                val isAhead = Geo.angleDifference(bearingToAlert, heading) <= AHEAD_CONE_DEG

                // 2. Dung CHIEU: camera chieu nguoc lai (dung ben kia duong) thi bo qua.
                val sameDirection = alert.direction == null || AlertTypes.hasRoadAxis(alert.type) ||
                    Geo.angleDifference(alert.direction, heading) <= SAME_DIRECTION_DEG

                // 3. Diem tren duong: phai nam ngay tren huong di, khong phai o duong song song ben canh.
                val onMyLine = !AlertTypes.hasRoadAxis(alert.type) ||
                    distance * sin(Math.toRadians(Geo.angleDifference(bearingToAlert, heading))) <= ROAD_AXIS_MAX_OFFSET_M

                isAhead && sameDirection && onMyLine
            }
            .sortedBy { it.second }
            .map { (alert, distance) -> AheadAlert(alert, distance.roundToInt()) }

    /** Cau doc bang giong noi. Khoang cach lam tron 50 m cho de nghe. */
    fun speech(item: AheadAlert, near: Boolean): String {
        val label = AlertTypes.title(item.alert)
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
 * Go giam toc chi doc lan 2 (xem AlertTypes.announceOnlyWhenNear).
 * Nho id da doc de khong doc lap lai moi giay.
 * Khi da vuot qua (canh bao khong con phia truoc) hoac da xa > 800 m thi QUEN id do,
 * de lan sau di lai doan duong nay (vd: di roi quay ve) van duoc doc.
 */
class AnnouncementPolicy(
    private val farDistanceM: Int = 500,
    private val nearDistanceM: Int = 150,
    private val forgetDistanceM: Int = 800,
) {
    private val stage = mutableMapOf<Int, Int>() // alertId -> 0 chua doc, 1 da doc xa, 2 da doc gan

    /** Goi moi lan co danh sach "phia truoc" moi: quen cac canh bao da vuot qua / da o xa. */
    fun forgetPassed(ahead: List<AheadAlert>) {
        val stillClose = ahead.filter { it.distanceM <= forgetDistanceM }.mapTo(HashSet()) { it.alert.id }
        stage.keys.retainAll(stillClose)
    }

    /**
     * Xet LAN LUOT cac canh bao phia truoc (gan truoc), doc cai dau tien can doc.
     * Khong chi xet cai gan nhat: vd go giam toc o 300 m (chua doc vi con xa) khong duoc
     * "che" camera o 450 m. Moi lan GPS chi doc 1 cau; cau tiep theo o lan GPS sau.
     */
    fun next(ahead: List<AheadAlert>): String? =
        ahead.asSequence().takeWhile { it.distanceM <= farDistanceM }.firstNotNullOfOrNull { next(it) }

    /** @return cau can doc cho 1 canh bao, hoac null neu khong can doc gi. */
    fun next(nearest: AheadAlert?): String? {
        nearest ?: return null
        val id = nearest.alert.id
        val current = stage[id] ?: 0

        return when {
            nearest.distanceM <= nearDistanceM && current < 2 -> {
                stage[id] = 2
                AlertLogic.speech(nearest, near = true)
            }
            // Go giam toc: bo lan doc tu xa, chi doc "Sắp tới..." khi con <= 150 m.
            nearest.distanceM <= farDistanceM && current < 1 && !AlertTypes.announceOnlyWhenNear(nearest.alert.type) -> {
                stage[id] = 1
                AlertLogic.speech(nearest, near = false)
            }
            else -> null
        }
    }
}
