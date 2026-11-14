package com.vietroad.app.ui

import android.app.Application
import androidx.lifecycle.AndroidViewModel
import androidx.lifecycle.viewModelScope
import com.vietroad.app.DeviceInfo
import com.vietroad.app.VietRoadApp
import com.vietroad.app.data.Alert
import com.vietroad.app.data.AlertTypes
import com.vietroad.app.data.ApiException
import com.vietroad.app.data.AuthResponse
import com.vietroad.app.data.Place
import com.vietroad.app.data.SubscriptionInfo
import com.vietroad.app.drive.NavState
import com.vietroad.app.data.ReportRequest
import com.vietroad.app.drive.AheadAlert
import com.vietroad.app.drive.DriveService
import com.vietroad.app.drive.Position
import kotlinx.coroutines.flow.MutableStateFlow
import kotlinx.coroutines.flow.StateFlow
import kotlinx.coroutines.flow.asStateFlow
import kotlinx.coroutines.flow.update
import kotlinx.coroutines.launch
import java.io.IOException

/** Toan bo trang thai man hinh. Giao dien chi "ve lai" theo object nay. */
data class UiState(
    val loggedIn: Boolean = false,
    val userName: String? = null,
    val busy: Boolean = false,
    val message: String? = null,
    // 5 truong duoi chep tu DriveEngine (chay trong DriveService).
    val tracking: Boolean = false,
    val position: Position? = null,
    val ahead: List<AheadAlert> = emptyList(),
    val nearbyCount: Int = 0,
    val nearby: List<Alert> = emptyList(),
    val speedLimit: Int? = null,
    val overspeed: Boolean = false,
    val online: Boolean = true,
    val nav: NavState? = null,
    // Man hinh tim diem den (null = dang o man hinh lai xe).
    val search: SearchUi? = null,
    // Goi dich vu (null = chua tai xong). Quyet dinh tinh nang nao duoc dung.
    val subscription: SubscriptionInfo? = null,
) {
    val canNavigate get() = subscription?.has(SubscriptionInfo.NAVIGATION) == true
}

data class SearchUi(
    val originName: String? = null,  // dia chi vi tri hien tai (null = dang tra / khong biet)
    val originLoading: Boolean = false,
    val picked: Place? = null,       // diem den vua chon (dang tinh duong)
    val results: List<Place> = emptyList(),
    val busy: Boolean = false,
    val searched: Boolean = false,   // da tim it nhat 1 lan (de hien "khong tim thay")
)

/**
 * ViewModel cua man hinh: dang nhap, bao cao, xac nhan; bat/tat dich vu nen.
 * Phan GPS + giong noi nam o DriveEngine (song ca khi man hinh tat); o day chi theo doi state cua no.
 */
class MainViewModel(app: Application) : AndroidViewModel(app) {
    private val vietRoad = app as VietRoadApp
    private val session = vietRoad.session
    private val api = vietRoad.api
    private val engine = vietRoad.engine

    // StateFlow: "hop chua" trang thai; giao dien theo doi va tu ve lai khi gia tri doi.
    private val _state = MutableStateFlow(UiState(loggedIn = session.isLoggedIn, userName = session.displayName))
    val state: StateFlow<UiState> = _state.asStateFlow()

    // Chi tu bat dich vu 1 lan khi mo man hinh; neu nguoi dung da bam "Dung" thi ton trong.
    private var autoStarted = false

    init {
        // Chep trang thai lai xe vao trang thai man hinh moi khi engine thay doi.
        viewModelScope.launch {
            engine.state.collect { d ->
                _state.update {
                    it.copy(tracking = d.tracking, position = d.position, ahead = d.ahead,
                        nearbyCount = d.nearbyCount, nearby = d.nearby, online = d.online, nav = d.nav,
                        speedLimit = d.speedLimit, overspeed = d.overspeed)
                }
            }
        }
        if (session.isLoggedIn) refreshSubscription()
    }

    // ---------- Goi dich vu ----------

    /** Tai goi hien tai (sau khi dang nhap / mo app). Loi mang thi giu gia tri cu. */
    fun refreshSubscription() {
        viewModelScope.launch { loadSubscription() }
    }

    private suspend fun loadSubscription(): SubscriptionInfo? {
        val sub = runCatching { api.subscription() }.getOrNull() ?: return _state.value.subscription
        _state.update { it.copy(subscription = sub) }
        return sub
    }

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
                loadSubscription()
            } catch (e: Exception) {
                _state.update { it.copy(busy = false, message = e.toUserMessage()) }
            }
        }
    }

    fun logout() {
        viewModelScope.launch {
            runCatching { api.logout() } // loi mang cung khong sao, van xoa phien tren may
            session.clear()
            signedOut(message = null)
        }
    }

    private fun signedOut(message: String?) {
        stopTracking()
        autoStarted = false
        _state.update { UiState(message = message) }
    }

    // ---------- Bat / tat dich vu nen ----------

    /** Man hinh goi khi da co quyen vi tri: chi tu bat lan dau. */
    fun onLocationPermissionReady() {
        if (autoStarted) return
        autoStarted = true
        startTracking()
    }

    /** Bat dich vu nen (GPS + canh bao). */
    fun startTracking() {
        engine.providerWarning()?.let(::showMessage)
        DriveService.start(getApplication())
    }

    fun stopTracking() = DriveService.stop(getApplication())

    // ---------- Chi duong ----------

    /**
     * Bam "Chi duong". Chi duong la tinh nang Premium: chua co thi hoi lai server 1 lan
     * (co the admin vua kich hoat), van chua co thi bao cho tai xe.
     */
    fun openSearch() {
        if (_state.value.canNavigate) return showSearch()
        viewModelScope.launch {
            if (loadSubscription()?.has(SubscriptionInfo.NAVIGATION) == true) showSearch()
            else showMessage(PREMIUM_NAVIGATION_MESSAGE)
        }
    }

    private fun showSearch() {
        val position = _state.value.position
        _state.update { it.copy(search = SearchUi(originLoading = position != null), message = null) }

        // Tra dia chi vi tri hien tai de hien "Từ: ...". Loi thi man hinh hien toa do thay the.
        position ?: return
        viewModelScope.launch {
            val name = runCatching { api.reversePlace(position.lat, position.lng).name }.getOrNull()
            _state.update { it.copy(search = it.search?.copy(originName = name, originLoading = false)) }
        }
    }

    fun closeSearch() = _state.update { it.copy(search = null) }

    /** Tim dia diem, uu tien gan vi tri hien tai. */
    fun searchPlaces(query: String) {
        if (query.isBlank()) return
        val position = _state.value.position
        _state.update { it.copy(search = it.search?.copy(busy = true), message = null) }

        viewModelScope.launch {
            try {
                val places = api.searchPlaces(query.trim(), position?.lat, position?.lng).places
                _state.update { it.copy(search = it.search?.copy(results = places, busy = false, searched = true)) }
            } catch (e: Exception) {
                _state.update { it.copy(search = it.search?.copy(busy = false)) }
                handleError(e)
            }
        }
    }

    /** Tinh duong tu vi tri hien tai toi diem da chon roi bat dau chi duong. */
    fun navigateTo(place: Place) {
        val position = _state.value.position
            ?: return showMessage("Chưa có vị trí GPS, chưa tìm đường được.")
        _state.update { it.copy(search = it.search?.copy(busy = true, picked = place), message = null) }

        viewModelScope.launch {
            try {
                val route = api.route(position.lat, position.lng, place.lat, place.lng)
                DriveService.start(getApplication()) // chi duong tiep ca khi tat man hinh
                // Khong co dia chi -> dung toa do, the chi duong van ghi ro "Từ ...".
                val origin = _state.value.search?.originName
                    ?: "%.5f, %.5f".format(java.util.Locale.US, position.lat, position.lng)
                engine.startNavigation(route, place.name, place.lat, place.lng, origin)
                _state.update { it.copy(search = null) }
            } catch (e: Exception) {
                _state.update { it.copy(search = it.search?.copy(busy = false, picked = null)) }
                handleError(e)
            }
        }
    }

    fun stopNavigation() = engine.stopNavigation()

    // ---------- Bao cao ----------

    /** Bao canh bao moi tai vi tri hien tai, theo huong xe dang di. */
    fun report(type: String) {
        val position = _state.value.position
            ?: return showMessage("Chưa có vị trí GPS.")

        viewModelScope.launch {
            try {
                val result = api.report(ReportRequest(type, position.lat, position.lng, position.heading))
                val label = AlertTypes.label(type)
                showMessage(if (result.created) "Đã báo $label. Cảm ơn bạn!" else "$label đã có, đã tính 1 lượt xác nhận.")
                engine.refreshNow()
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
                engine.refreshNow()
            } catch (e: Exception) {
                handleError(e)
            }
        }
    }

    fun clearMessage() = _state.update { it.copy(message = null) }

    private fun showMessage(text: String) = _state.update { it.copy(message = text) }

    private fun handleError(e: Exception) {
        if (e is ApiException && e.code == 401) {
            // Het phien -> tat dich vu, quay ve man hinh dang nhap.
            signedOut(message = e.message)
        } else {
            showMessage(e.toUserMessage())
        }
    }

    private fun Exception.toUserMessage(): String = when (this) {
        is ApiException -> message ?: "Lỗi máy chủ"
        is IOException -> "Không kết nối được máy chủ ${DeviceInfo.apiBaseUrl}"
        else -> "Lỗi: $message"
    }

    private companion object {
        const val PREMIUM_NAVIGATION_MESSAGE =
            "Chỉ đường bằng giọng nói dành cho gói Premium. Liên hệ quản trị viên để nâng cấp."
    }
}
