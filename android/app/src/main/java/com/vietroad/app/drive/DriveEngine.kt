package com.vietroad.app.drive

import android.content.Context
import android.location.Location
import android.location.LocationManager
import android.os.SystemClock
import com.vietroad.app.data.Alert
import com.vietroad.app.data.ApiClient
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.Job
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch

/** Vi tri xe tai 1 thoi diem. */
data class Position(
    val lat: Double,
    val lng: Double,
    val speedKmh: Int?,
    val heading: Double?,
    val fromGps: Boolean = true,   // false = vi tri tu mang (kem chinh xac, khong co huong)
    val accuracyM: Int? = null,    // sai so uoc tinh (met)
)

/** Trang thai "dang lai xe": ca man hinh va thong bao (notification) deu doc tu day. */
data class DriveState(
    val tracking: Boolean = false,
    val position: Position? = null,
    val ahead: List<AheadAlert> = emptyList(),
    val nearbyCount: Int = 0,
    val online: Boolean = true,
)

/**
 * "Bo nao" khi lai xe: nhan GPS -> tai canh bao quanh xe -> chon canh bao phia truoc -> doc giong noi.
 *
 * Truoc day phan nay nam trong MainViewModel, nhung ViewModel gan voi man hinh:
 * tat man hinh / chuyen sang app khac (Google Maps...) la he thong co the dung no.
 * Gio no la 1 object DUY NHAT cho ca app (tao trong VietRoadApp), do DriveService
 * (dich vu chay nen) bat/tat; man hinh chi "xem" state.
 */
class DriveEngine(context: Context, private val api: ApiClient) {
    private val appContext = context.applicationContext
    private val tracker = LocationTracker(appContext)

    // Scope rieng, khong phu thuoc man hinh nao. SupervisorJob: 1 tac vu loi khong keo theo cac tac vu khac.
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)

    private val _state = MutableStateFlow(DriveState())
    val state: StateFlow<DriveState> = _state.asStateFlow()

    private var announcer: Announcer? = null
    private var policy = AnnouncementPolicy()
    private var trackingJob: Job? = null

    // Bo nho dem canh bao quanh xe, tai lai khi xe di xa hoac sau 1 phut.
    private var nearby: List<Alert> = emptyList()
    private var lastFetchAt = 0L
    private var lastFetchLat: Double? = null
    private var lastFetchLng: Double? = null
    private var fetching = false

    /** Canh bao ve nguon vi tri (GPS tat, may khong co GPS...), null = on. */
    fun providerWarning(): String? {
        val providers = tracker.availableProviders()
        return when {
            providers.isEmpty() -> "Không có nguồn vị trí. Hãy bật Vị trí trong Cài đặt."
            LocationManager.GPS_PROVIDER !in providers && !tracker.hasGpsHardware() ->
                "Máy không có GPS: dùng vị trí qua mạng (kém chính xác hơn)."
            LocationManager.GPS_PROVIDER !in providers -> "GPS đang tắt: dùng vị trí qua mạng. Nên bật GPS."
            else -> null
        }
    }

    /** Bat dau theo doi. Chi DriveService goi (sau khi da co quyen vi tri). Goi nhieu lan cung chi chay 1 lan. */
    fun start() {
        if (trackingJob != null) return
        announcer = Announcer(appContext)
        policy = AnnouncementPolicy()
        _state.update { it.copy(tracking = true) }
        trackingJob = scope.launch { tracker.updates().collect { onLocation(it) } }
    }

    /** Dung han: huy GPS (tiet kiem pin), tat giong noi, xoa du lieu cu. */
    fun stop() {
        trackingJob?.cancel()
        trackingJob = null
        announcer?.shutdown()
        announcer = null
        nearby = emptyList()
        lastFetchAt = 0
        lastFetchLat = null
        lastFetchLng = null
        _state.value = DriveState()
    }

    /** Tai lai ngay (sau khi bao cao / xac nhan) de thay ket qua. */
    fun refreshNow() {
        _state.value.position?.let { refreshNearbyIfNeeded(it, force = true) }
    }

    private fun onLocation(location: Location) {
        // Chi tin huong GPS khi xe dang chay (dung yen thi huong nhay lung tung).
        val moving = location.hasSpeed() && location.speed >= AlertLogic.MIN_SPEED_FOR_HEADING_MS
        val position = Position(
            lat = location.latitude,
            lng = location.longitude,
            speedKmh = if (location.hasSpeed()) (location.speed * 3.6f).toInt() else null,
            heading = if (moving && location.hasBearing()) location.bearing.toDouble() else null,
            fromGps = location.provider == LocationManager.GPS_PROVIDER,
            accuracyM = if (location.hasAccuracy()) location.accuracy.toInt() else null,
        )

        refreshNearbyIfNeeded(position)
        updateAhead(position)
    }

    /** Tinh lai canh bao phia truoc, doc neu can, cap nhat state. */
    private fun updateAhead(position: Position) {
        val ahead = AlertLogic.ahead(nearby, position.lat, position.lng, position.heading)
        policy.next(ahead.firstOrNull())?.let { announcer?.speak(it) }

        _state.update { it.copy(position = position, ahead = ahead, nearbyCount = nearby.size) }
    }

    /** Tai lai canh bao khi da di xa >1 km so voi lan tai truoc, hoac sau 60 giay. */
    private fun refreshNearbyIfNeeded(position: Position, force: Boolean = false) {
        val now = SystemClock.elapsedRealtime()
        val movedM = lastFetchLat?.let { Geo.distanceMeters(it, lastFetchLng!!, position.lat, position.lng) }
        val due = force || movedM == null || movedM > REFETCH_DISTANCE_M || now - lastFetchAt > REFETCH_INTERVAL_MS
        if (fetching || !due) return

        fetching = true
        scope.launch {
            try {
                val alerts = api.nearby(position.lat, position.lng, FETCH_RADIUS_M).alerts.map { it.alert }
                if (trackingJob == null) return@launch // da bam Dung trong luc dang tai -> bo ket qua
                nearby = alerts
                lastFetchAt = now
                lastFetchLat = position.lat
                lastFetchLng = position.lng
                _state.update { it.copy(online = true) }
                // Tinh lai ngay voi du lieu moi, khong cho lan GPS tiep theo (xe dung yen thi GPS it gui).
                updateAhead(_state.value.position ?: position)
            } catch (e: kotlinx.coroutines.CancellationException) {
                throw e
            } catch (e: Exception) {
                // Mat mang: giu danh sach cu, van canh bao duoc; thu lai o lan GPS sau.
                _state.update { it.copy(online = false) }
            } finally {
                fetching = false
            }
        }
    }

    private companion object {
        const val FETCH_RADIUS_M = 3000
        const val REFETCH_DISTANCE_M = 1000.0
        const val REFETCH_INTERVAL_MS = 60_000L
    }
}
