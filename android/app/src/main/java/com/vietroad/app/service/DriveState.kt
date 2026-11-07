package com.vietroad.app.service

import android.os.Handler
import android.os.Looper
import com.vietroad.app.engine.EngineResult
import java.util.concurrent.CopyOnWriteArraySet

data class DriveSnapshot(
    val running: Boolean,
    val simulating: Boolean,
    val gpsFix: Boolean,
    val lat: Double,
    val lng: Double,
    val speed: Int,
    val heading: Double?,
    val result: EngineResult?,
    val overspeedCount: Int,
    val maxSpeed: Int,
    val distanceKm: Double,
)

/** Kênh trạng thái giữa DriveService và giao diện (cùng tiến trình). */
object DriveState {
    @Volatile var last: DriveSnapshot? = null
        private set

    /** MainActivity đang hiển thị → ẩn bong bóng nổi */
    @Volatile var uiVisible = false

    /** Tăng mỗi khi dữ liệu trong CSDL thay đổi để service nạp lại bộ đệm */
    @Volatile var dataVersion = 0
        private set

    private val main = Handler(Looper.getMainLooper())
    private val listeners = CopyOnWriteArraySet<(DriveSnapshot) -> Unit>()

    fun publish(s: DriveSnapshot) {
        last = s
        main.post { listeners.forEach { it(s) } }
    }

    fun addListener(l: (DriveSnapshot) -> Unit) = listeners.add(l)
    fun removeListener(l: (DriveSnapshot) -> Unit) = listeners.remove(l)

    fun dataChanged() {
        dataVersion++
    }
}
