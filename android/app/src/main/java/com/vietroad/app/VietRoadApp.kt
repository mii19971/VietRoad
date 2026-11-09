package com.vietroad.app

import android.app.Application
import com.vietroad.app.data.ApiClient
import com.vietroad.app.data.SessionStore
import com.vietroad.app.drive.DriveEngine
import org.maplibre.android.MapLibre

/**
 * Application: object song suot vong doi cua tien trinh app (lau hon moi Activity / Service).
 * Noi dat cac thanh phan DUNG CHUNG: man hinh (MainViewModel) va dich vu nen (DriveService)
 * cung dung 1 ApiClient, 1 DriveEngine -> cung 1 du lieu, khong bi 2 GPS / 2 giong noi.
 * Khai bao trong AndroidManifest: <application android:name=".VietRoadApp">.
 */
class VietRoadApp : Application() {
    lateinit var session: SessionStore
        private set
    lateinit var api: ApiClient
        private set
    lateinit var engine: DriveEngine
        private set

    override fun onCreate() {
        super.onCreate()
        session = SessionStore(this)
        api = ApiClient(DeviceInfo.apiBaseUrl, session)
        engine = DriveEngine(this, api)
        // MapLibre phai khoi tao 1 lan truoc khi tao MapView dau tien.
        MapLibre.getInstance(this)
    }
}
