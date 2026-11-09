package com.vietroad.app

import android.os.Build

/** Thong tin may dang chay app. */
object DeviceInfo {
    /** Dang chay tren may ao Android (emulator) cua Android Studio? */
    val isEmulator: Boolean by lazy {
        Build.HARDWARE.contains("ranchu") || Build.HARDWARE.contains("goldfish") ||
            Build.FINGERPRINT.contains("generic") || Build.PRODUCT.startsWith("sdk_")
    }

    /**
     * Dia chi VietRoad.Api:
     *  - May ao: 10.0.2.2 = "localhost" cua may tinh (gradle.properties).
     *  - Dien thoai that: IP LAN cua may tinh (local.properties), vd http://192.168.1.6:5000/
     * Nho vay 1 file APK chay duoc ca 2 noi.
     */
    val apiBaseUrl: String
        get() = if (isEmulator) BuildConfig.API_BASE_URL else BuildConfig.DEVICE_API_BASE_URL
}
