package com.vietroad.app.data

import java.text.ParsePosition
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale
import java.util.TimeZone
import java.util.UUID
import kotlin.math.roundToInt

/**
 * Các trường quản lý dữ liệu cảnh báo: mã định danh, nguồn, độ tin cậy, ngày xác minh,
 * hạn hiệu lực, trạng thái. Không phụ thuộc Android → kiểm thử bằng JUnit thuần.
 */
object DataFields {

    /** Đang hiệu lực, được dùng để cảnh báo */
    const val STATUS_ACTIVE = "active"
    /** Chờ xác minh (vd. báo cáo của người dùng), chưa cảnh báo */
    const val STATUS_PENDING = "pending"
    /** Đã gỡ / không còn hiệu lực, giữ lại để đồng bộ việc xoá */
    const val STATUS_INACTIVE = "inactive"
    val STATUSES = setOf(STATUS_ACTIVE, STATUS_PENDING, STATUS_INACTIVE)

    /** Bản ghi không ghi độ tin cậy → coi là "chưa rõ" */
    const val DEFAULT_CONFIDENCE = 0.5
    /** Điểm tài xế tự thêm trên box, chưa được ai xác minh */
    const val USER_CONFIDENCE = 0.5

    fun validConfidence(c: Double) = !c.isNaN() && c in 0.0..1.0

    // ---------------- mã định danh ----------------

    fun randomUid(): String = UUID.randomUUID().toString()

    /**
     * Mã ổn định cho điểm không có sẵn uid: cùng loại + toạ độ + hướng → cùng mã trên mọi thiết bị,
     * nhờ đó nhập lại cùng một tệp sẽ cập nhật thay vì nhân đôi bản ghi.
     * Phải khớp với hàm stableUid trong tools/gen-sample-data.mjs.
     */
    fun featureUid(code: String, lat: Double, lng: Double, heading: Double?): String =
        nameUid("f|$code|${fmt(lat)}|${fmt(lng)}|${heading?.roundToInt() ?: ""}")

    fun zoneUid(name: String?, first: DoubleArray, last: DoubleArray): String =
        nameUid("z|${name.orEmpty()}|${fmt(first[0])}|${fmt(first[1])}|${fmt(last[0])}|${fmt(last[1])}")

    private fun nameUid(key: String) = UUID.nameUUIDFromBytes(key.toByteArray(Charsets.UTF_8)).toString()

    private fun fmt(d: Double) = String.format(Locale.US, "%.6f", d)

    // ---------------- thời gian ----------------

    private val PATTERNS = listOf(
        "yyyy-MM-dd'T'HH:mm:ss.SSS'Z'",
        "yyyy-MM-dd'T'HH:mm:ss'Z'",
        "yyyy-MM-dd'T'HH:mm:ss",
        "yyyy-MM-dd",
    )

    /**
     * Đọc thời điểm từ JSON, trả về epoch millis (UTC).
     * Nhận: số giây hoặc mili giây kể từ 1970, chuỗi số, hoặc chuỗi ISO
     * "2025-03-01", "2025-03-01T08:00:00", "2025-03-01T08:00:00Z", "2025-03-01T08:00:00.000Z".
     * Trả null nếu không đọc được.
     */
    fun parseTime(v: Any?): Long? = when (v) {
        is Number -> {
            val d = v.toDouble()
            when {
                d.isNaN() || d <= 0 -> null
                d < 1e11 -> (d * 1000).toLong() // giây
                else -> d.toLong()
            }
        }
        is String -> {
            val s = v.trim()
            s.toDoubleOrNull()?.let { parseTime(it) } ?: PATTERNS.firstNotNullOfOrNull { parseWith(it, s) }
        }
        else -> null
    }

    private fun parseWith(pattern: String, s: String): Long? {
        val f = utcFormat(pattern)
        val pos = ParsePosition(0)
        val d = f.parse(s, pos) ?: return null
        return if (pos.index == s.length) d.time else null
    }

    /** Ghi thời điểm ra JSON dạng ISO 8601 UTC, vd. "2025-03-01T00:00:00Z". */
    fun formatTime(ms: Long): String = utcFormat("yyyy-MM-dd'T'HH:mm:ss'Z'").format(Date(ms))

    // SimpleDateFormat không an toàn đa luồng → tạo mới mỗi lần
    private fun utcFormat(pattern: String) = SimpleDateFormat(pattern, Locale.US).apply {
        timeZone = TimeZone.getTimeZone("UTC")
        isLenient = false
    }
}
