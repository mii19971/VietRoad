package com.vietroad.app.data

import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.sync.Mutex
import kotlinx.coroutines.sync.withLock
import kotlinx.coroutines.withContext
import kotlinx.serialization.encodeToString
import kotlinx.serialization.json.Json
import okhttp3.MediaType.Companion.toMediaType
import okhttp3.OkHttpClient
import okhttp3.Request
import okhttp3.RequestBody.Companion.toRequestBody
import okhttp3.Response
import java.util.concurrent.TimeUnit

/** API tra loi loi (4xx/5xx). code = ma HTTP, message = thong bao cua server. */
class ApiException(val code: Int, message: String) : Exception(message)

/**
 * Goi VietRoad.Api. Moi ham deu la "suspend": chay tren luong mang (Dispatchers.IO),
 * khong lam dung giao dien trong luc cho server tra loi.
 */
class ApiClient(private val baseUrl: String, private val session: SessionStore) {

    private val http = OkHttpClient.Builder()
        .connectTimeout(10, TimeUnit.SECONDS)
        .readTimeout(15, TimeUnit.SECONDS)
        .build()

    // ignoreUnknownKeys: server them truong moi thi app cu van doc duoc.
    private val json = Json { ignoreUnknownKeys = true }

    private val jsonType = "application/json; charset=utf-8".toMediaType()

    // Chi 1 coroutine duoc refresh token tai 1 thoi diem (refresh token chi dung duoc 1 lan).
    private val refreshLock = Mutex()

    // ----- Dang nhap -----

    suspend fun login(email: String, password: String): AuthResponse =
        post("api/v1/auth/login", json.encodeToString(LoginRequest(email, password)), auth = false)

    suspend fun register(email: String, password: String, displayName: String?): AuthResponse =
        post("api/v1/auth/register", json.encodeToString(RegisterRequest(email, password, displayName)), auth = false)

    suspend fun logout() {
        val token = session.refreshToken ?: return
        execute("api/v1/auth/logout", "POST", json.encodeToString(RefreshRequest(token)), auth = false)
    }

    // ----- Canh bao -----

    suspend fun nearby(lat: Double, lng: Double, radiusM: Int): NearbyAlertsResponse =
        // Double.toString cua Kotlin luon dung dau ".", khong phu thuoc ngon ngu may.
        get("api/v1/alerts/nearby?lat=$lat&lng=$lng&radius=$radiusM")

    suspend fun report(request: ReportRequest): ReportResponse =
        post("api/v1/reports", json.encodeToString(request), auth = true)

    suspend fun vote(alertId: Int, confirm: Boolean): Alert =
        post("api/v1/alerts/$alertId/votes", json.encodeToString(VoteRequest(if (confirm) "confirm" else "deny")), auth = true)

    // ----- Ham phu -----

    // inline + reified: giu duoc kieu T luc chay de json.decodeFromString<T> biet doi sang kieu nao.
    private suspend inline fun <reified T> get(path: String): T =
        json.decodeFromString(execute(path, "GET", null, auth = false))

    private suspend inline fun <reified T> post(path: String, body: String, auth: Boolean): T =
        json.decodeFromString(execute(path, "POST", body, auth))

    /**
     * Gui request, tra ve noi dung JSON.
     * Neu access token het han (401): dung refresh token lay token moi roi gui lai 1 lan.
     */
    private suspend fun execute(path: String, method: String, body: String?, auth: Boolean): String =
        withContext(Dispatchers.IO) {
            var response = send(path, method, body, if (auth) session.accessToken else null)

            if (response.code == 401 && auth) {
                response.close()
                if (!refreshTokens()) {
                    session.clear() // refresh token cung het han -> phai dang nhap lai
                    throw ApiException(401, "Phiên đăng nhập đã hết hạn, vui lòng đăng nhập lại.")
                }
                response = send(path, method, body, session.accessToken)
            }

            // use { }: tu dong dong response sau khi doc xong (giai phong ket noi).
            response.use { r ->
                val text = r.body?.string().orEmpty()
                if (!r.isSuccessful) {
                    val message = runCatching { json.decodeFromString<ErrorBody>(text).error }.getOrNull()
                    throw ApiException(r.code, message ?: "Lỗi máy chủ (${r.code})")
                }
                text
            }
        }

    private fun send(path: String, method: String, body: String?, token: String?): Response {
        val request = Request.Builder()
            .url(baseUrl + path)
            .method(method, body?.toRequestBody(jsonType))
            .apply { if (token != null) header("Authorization", "Bearer $token") }
            .build()
        return http.newCall(request).execute()
    }

    /** Doi refresh token lay cap token moi. false = khong doi duoc. */
    private suspend fun refreshTokens(): Boolean = refreshLock.withLock {
        val refresh = session.refreshToken ?: return false
        send("api/v1/auth/refresh", "POST", json.encodeToString(RefreshRequest(refresh)), null).use { r ->
            if (!r.isSuccessful) return false
            session.save(json.decodeFromString<AuthResponse>(r.body!!.string()))
            true
        }
    }
}
