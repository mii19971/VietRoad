package com.vietroad.app.ui

import android.app.Application
import android.location.Location
import android.location.LocationManager
import android.os.SystemClock
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.vietroad.app.BuildConfig
import com.vietroad.app.data.Alert
import com.vietroad.app.data.AlertTypes
import com.vietroad.app.data.ApiClient
import com.vietroad.app.data.ApiException
import com.vietroad.app.data.AuthResponse
import com.vietroad.app.data.ReportRequest
import com.vietroad.app.data.SessionStore
import com.vietroad.app.drive.AheadAlert
import com.vietroad.app.drive.AlertLogic
import com.vietroad.app.drive.AnnouncementPolicy
import com.vietroad.app.drive.Announcer
import com.vietroad.app.drive.Geo
import com.vietroad.app.drive.LocationTracker
import kotlinx.coroutines.Job
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.io.IOException

/** Vi tri xe tai 1 thoi diem. */
data class Position(
    val lat: Double,
    val lng: Double,
    val speedKmh: Int?,
    val heading: Double?,
    val fromGps: Boolean = true,   // false = vi tri tu mang (kem chinh xac, khong co huong)
    val accuracyM: Int? = null,    // sai so uoc tinh (met)
)

/** Toan bo trang thai man hinh. Giao dien chi "ve lai" theo object nay. */
data class UiState(
    val loggedIn: Boolean = false,
    val userName: String? = null,
    val busy: Boolean = false,
    val message: String? = null,
    val position: Position? = null,
    val ahead: List<AheadAlert> = emptyList(),
    val nearbyCount: Int = 0,
    val online: Boolean = true,
)

/**
 * ViewModel: song lau hon man hinh (xoay man hinh khong mat du lieu),
 * giu trang thai va dieu phoi: API, GPS, giong noi.
 */
class MainViewModel(app: Application) : AndroidViewModel(app) {
    private val session = SessionStore(app)
    private val api = ApiClient(BuildConfig.API_BASE_URL, session)
    private val tracker = LocationTracker(app)
    private val announcer = Announcer(app)
    private val policy = AnnouncementPolicy()

    // StateFlow: "hop chua" trang thai; giao dien theo doi va tu ve lai khi gia tri doi.
    private val _state = MutableStateFlow(UiState(loggedIn = session.isLoggedIn, userName = session.displayName))
    val state: StateFlow<UiState> = _state.asStateFlow()

    // Bo nho dem canh bao quanh xe, tai lai khi xe di xa hoac sau 1 phut.
    private var nearby: List<Alert> = emptyList()
    private var lastFetchAt = 0L
    private var lastFetchLat: Double? = null
    private var lastFetchLng: Double? = null
    private var fetching = false
    private var trackingJob: Job? = null

    // ---------- Dang nhap ----------

    fun login(email: String, password: String) = authenticate { api.login(email.trim(), password) }

    fun register(email: String, password: String, displayName: String) =
        authenticate { api.register(email.trim(), password, displayName.trim().ifEmpty { null }) }

    private fun authenticate(call: suspend () -> AuthResponse) {
        viewModelScope.launch {
            _state.update { it.copy(busy = true, message = null) }
            try {
                val auth = call()
                session.save(auth)
                _state.update { it.copy(busy = false, loggedIn = true, userName = auth.user.displayName) }
            } catch (e: Exception) {
                _state.update { it.copy(busy = false, message = e.toUserMessage()) }
            }
        }
    }

    fun logout() {
        viewModelScope.launch {
            runCatching { api.logout() } // loi mang cung khong sao, van xoa phien tren may
            session.clear()
            stopTracking()
            _state.value = UiState()
        }
    }

    // ---------- GPS ----------

    /** Goi sau khi da co quyen vi tri. Goi nhieu lan cung chi chay 1 lan. */
    fun startTracking() {
        if (trackingJob != null) return

        val providers = tracker.availableProviders()
        val warning = when {
            providers.isEmpty() -> "Không có nguồn vị trí. Hãy bật Vị trí trong Cài đặt."
            LocationManager.GPS_PROVIDER !in providers && !tracker.hasGpsHardware() ->
                "Máy không có GPS: dùng vị trí qua mạng (kém chính xác hơn)."
            LocationManager.GPS_PROVIDER !in providers -> "GPS đang tắt: dùng vị trí qua mạng. Nên bật GPS."
            else -> null
        }
        if (warning != null) _state.update { it.copy(message = warning) }

        trackingJob = viewModelScope.launch {
            tracker.updates().collect { onLocation(it) }
        }
    }

    private fun stopTracking() {
        trackingJob?.cancel()
        trackingJob = null
        nearby = emptyList()
        lastFetchAt = 0
        lastFetchLat = null
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

    /** Tinh lai canh bao phia truoc, doc neu can, cap nhat giao dien. */
    private fun updateAhead(position: Position) {
        val ahead = AlertLogic.ahead(nearby, position.lat, position.lng, position.heading)
        policy.next(ahead.firstOrNull())?.let(announcer::speak)

        _state.update { it.copy(position = position, ahead = ahead, nearbyCount = nearby.size) }
    }

    /** Tai lai canh bao khi da di xa >1 km so voi lan tai truoc, hoac sau 60 giay. */
    private fun refreshNearbyIfNeeded(position: Position, force: Boolean = false) {
        val now = SystemClock.elapsedRealtime()
        val movedM = lastFetchLat?.let { Geo.distanceMeters(it, lastFetchLng!!, position.lat, position.lng) }
        val due = force || movedM == null || movedM > REFETCH_DISTANCE_M || now - lastFetchAt > REFETCH_INTERVAL_MS
        if (fetching || !due) return

        fetching = true
        viewModelScope.launch {
            try {
                nearby = api.nearby(position.lat, position.lng, FETCH_RADIUS_M).alerts.map { it.alert }
                lastFetchAt = now
                lastFetchLat = position.lat
                lastFetchLng = position.lng
                _state.update { it.copy(online = true) }
                // Ve lai ngay voi du lieu moi, khong cho lan GPS tiep theo (xe dung yen thi GPS it gui).
                updateAhead(_state.value.position ?: position)
            } catch (e: Exception) {
                // Mat mang: giu danh sach cu, van canh bao duoc; thu lai o lan GPS sau.
                _state.update { it.copy(online = false) }
            } finally {
                fetching = false
            }
        }
    }

    // ---------- Bao cao ----------

    /** Bao canh bao moi tai vi tri hien tai, theo huong xe dang di. */
    fun report(type: String) {
        val position = _state.value.position
            ?: return _state.update { it.copy(message = "Chưa có vị trí GPS.") }

        viewModelScope.launch {
            try {
                val result = api.report(ReportRequest(type, position.lat, position.lng, position.heading))
                val label = AlertTypes.label(type)
                showMessage(if (result.created) "Đã báo $label. Cảm ơn bạn!" else "$label đã có, đã tính 1 lượt xác nhận.")
                refreshNearbyIfNeeded(position, force = true)
            } catch (e: Exception) {
                handleError(e)
            }
        }
    }

    /** Xac nhan canh bao "van con" (confirm = true) hoac "khong con". */
    fun vote(alertId: Int, confirm: Boolean) {
        viewModelScope.launch {
            try {
                api.vote(alertId, confirm)
                showMessage(if (confirm) "Đã xác nhận còn." else "Đã báo không còn.")
                _state.value.position?.let { refreshNearbyIfNeeded(it, force = true) }
            } catch (e: Exception) {
                handleError(e)
            }
        }
    }

    fun clearMessage() = _state.update { it.copy(message = null) }

    private fun showMessage(text: String) = _state.update { it.copy(message = text) }

    private fun handleError(e: Exception) {
        if (e is ApiException && e.code == 401) {
            // Het phien -> quay ve man hinh dang nhap.
            stopTracking()
            _state.value = UiState(message = e.message)
        } else {
            showMessage(e.toUserMessage())
        }
    }

    private fun Exception.toUserMessage(): String = when (this) {
        is ApiException -> message ?: "Lỗi máy chủ"
        is IOException -> "Không kết nối được máy chủ ${BuildConfig.API_BASE_URL}"
        else -> "Lỗi: $message"
    }

    // ViewModel bi huy (thoat han app) -> tat giong noi.
    override fun onCleared() = announcer.shutdown()

    private companion object {
        const val FETCH_RADIUS_M = 3000
        const val REFETCH_DISTANCE_M = 1000.0
        const val REFETCH_INTERVAL_MS = 60_000L
    }
}
