package com.vietroad.app.drive

import com.vietroad.app.data.Alert
import com.vietroad.app.data.AlertTypes

/**
 * Sau khi xe ĐI QUA một cảnh báo mà người dùng có thể xác minh (camera, chốt CSGT), hỏi nhanh
 * "còn / không còn". Nhờ vậy dữ liệu luôn mới: điểm đã gỡ sẽ bị báo "không còn" rồi tự mờ đi,
 * điểm còn thật được xác nhận lại nên không bị giảm điểm theo thời gian.
 *
 * Chỉ hỏi SAU KHI đã qua (điểm lùi ra sau), KHÔNG hỏi lúc đang tới gần — để tài xế không phải
 * bấm điện thoại khi đang cần nhìn đường. Mỗi điểm chỉ hỏi 1 lần cho tới khi bấm Dừng.
 */
class PassReviewTracker(
    private val approachM: Double = 70.0,  // coi la "da toi noi" khi vao trong ban kinh nay
    private val leaveM: Double = 110.0,     // "da di qua" khi ra xa hon nguong nay
) {
    private val approached = HashSet<Int>() // id cac diem xe da toi gan
    private val reviewed = HashSet<Int>()   // id cac diem da hoi (khong hoi lai)

    /**
     * Gọi mỗi lần có vị trí mới.
     * @return cảnh báo vừa đi qua cần hỏi, hoặc null nếu chưa có.
     */
    fun passed(alerts: List<Alert>, lat: Double, lng: Double): Alert? {
        var result: Alert? = null
        for (alert in alerts) {
            if (!AlertTypes.canReview(alert.type)) continue
            val d = Geo.distanceMeters(lat, lng, alert.lat, alert.lng)
            when {
                d <= approachM -> approached.add(alert.id)
                d >= leaveM && alert.id in approached && reviewed.add(alert.id) ->
                    if (result == null) result = alert // da di qua, chua hoi -> hoi (1 diem / lan)
            }
        }
        return result
    }

    /** Bấm Dừng / bắt đầu lại chuyến: quên hết để chuyến sau lại hỏi. */
    fun reset() {
        approached.clear()
        reviewed.clear()
    }
}
