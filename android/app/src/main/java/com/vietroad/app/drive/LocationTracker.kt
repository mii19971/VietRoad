package com.vietroad.app.drive

import android.annotation.SuppressLint
import android.content.Context
import android.location.Location
import android.location.LocationListener
import android.location.LocationManager
import android.os.Bundle
import android.os.Looper
import android.os.SystemClock
import kotlinx.coroutines.channels.awaitClose
import kotlinx.coroutines.flow.Flow
import kotlinx.coroutines.flow.callbackFlow

/**
 * Lay vi tri. Dung LocationManager co san trong Android (khong can Google Play Services,
 * chay duoc tren ca dau Android o to khong co dich vu Google).
 *
 * 2 nguon vi tri:
 *  - GPS: chinh xac (vai met), co toc do va huong. Tablet chi co Wi-Fi thuong KHONG co GPS.
 *  - Mang (Wi-Fi / tram phat song): kem chinh xac hon (vai chuc - vai tram met), khong co huong.
 * Dung ca 2 neu co; khi GPS dang hoat dong thi bo qua vi tri tu mang.
 */
class LocationTracker(context: Context) {
    private val manager = context.getSystemService(Context.LOCATION_SERVICE) as LocationManager

    /** Cac nguon vi tri may NAY co va dang bat (GPS truoc). */
    fun availableProviders(): List<String> =
        listOf(LocationManager.GPS_PROVIDER, LocationManager.NETWORK_PROVIDER)
            .filter { it in manager.allProviders && manager.isProviderEnabled(it) }

    /** May co chip GPS khong (khac voi "GPS dang tat"). */
    fun hasGpsHardware(): Boolean = LocationManager.GPS_PROVIDER in manager.allProviders

    /**
     * Flow phat ra vi tri moi moi ~1 giay.
     * callbackFlow: bien API kieu "goi lai" (listener) thanh Flow cua Kotlin.
     * Khi nguoi nghe ngung collect -> awaitClose chay -> huy dang ky (tiet kiem pin).
     * Phai xin quyen vi tri TRUOC khi goi (xem DriveScreen).
     */
    @SuppressLint("MissingPermission")
    fun updates(): Flow<Location> = callbackFlow {
        var lastGpsFixAt = Long.MIN_VALUE / 2

        // Viet du 4 ham: tren Android cu (< 10) cac ham nay chua co ban mac dinh,
        // thieu se bi loi AbstractMethodError khi he thong goi.
        val listener = object : LocationListener {
            override fun onLocationChanged(location: Location) {
                val now = SystemClock.elapsedRealtime()
                if (location.provider == LocationManager.GPS_PROVIDER) lastGpsFixAt = now
                if (shouldAccept(location.provider, now, lastGpsFixAt)) trySend(location)
            }
            override fun onProviderEnabled(provider: String) {}
            override fun onProviderDisabled(provider: String) {}
            @Deprecated("Deprecated in Java")
            override fun onStatusChanged(provider: String?, status: Int, extras: Bundle?) {}
        }

        val providers = availableProviders()

        // Vi tri cu (neu co) de hien ngay. Uu tien ban GPS.
        providers.firstNotNullOfOrNull { manager.getLastKnownLocation(it) }?.let { trySend(it) }

        for (provider in providers) {
            manager.requestLocationUpdates(provider, 1_000L, 0f, listener, Looper.getMainLooper())
        }

        awaitClose { manager.removeUpdates(listener) }
    }

    companion object {
        /** GPS im lang lau hon thoi gian nay (vd vao ham, mat ve tinh) -> chap nhan vi tri tu mang. */
        const val GPS_STALE_MS = 10_000L

        /**
         * Ham thuan (khong dung Android) -> viet unit test duoc.
         * Vi tri tu GPS luon nhan; vi tri tu mang chi nhan khi GPS da im lang > 10 giay.
         */
        fun shouldAccept(provider: String?, nowMs: Long, lastGpsFixAtMs: Long): Boolean =
            provider == LocationManager.GPS_PROVIDER || nowMs - lastGpsFixAtMs > GPS_STALE_MS
    }
}
