package com.vietroad.app.service

import android.Manifest
import android.app.Notification
import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.content.pm.ServiceInfo
import android.location.Location
import android.location.LocationListener
import android.location.LocationManager
import android.os.Build
import android.os.Bundle
import android.os.Handler
import android.os.HandlerThread
import android.os.IBinder
import android.os.Looper
import android.os.SystemClock
import android.util.Log
import androidx.core.app.NotificationCompat
import androidx.core.content.ContextCompat
import com.vietroad.app.R
import com.vietroad.app.data.FeatureDb
import com.vietroad.app.data.Prefs
import com.vietroad.app.data.SignCatalog
import com.vietroad.app.engine.EngineResult
import com.vietroad.app.engine.Geo
import com.vietroad.app.engine.RoadFeature
import com.vietroad.app.engine.SpeedZone
import com.vietroad.app.engine.VehicleState
import com.vietroad.app.engine.WarningEngine
import com.vietroad.app.ui.MainActivity
import kotlin.math.max
import kotlin.math.roundToInt

/**
 * Service chạy nền (foreground): nhận GPS mỗi giây, chạy bộ máy cảnh báo trên dữ liệu
 * SQLite cục bộ, phát giọng nói, cập nhật giao diện/bong bóng nổi. Không dùng mạng.
 */
class DriveService : Service(), LocationListener {

    private lateinit var db: FeatureDb
    private lateinit var prefs: Prefs
    private lateinit var engine: WarningEngine
    private lateinit var announcer: Announcer
    private lateinit var overlay: OverlayBubble
    private lateinit var locationManager: LocationManager
    private val handler = Handler(Looper.getMainLooper())

    /** Mọi thao tác SQLite chạy trên luồng riêng để GPS/giao diện không bị giật trên box yếu. */
    private val dbThread = HandlerThread("vietroad-db")
    private lateinit var dbHandler: Handler

    // Bộ đệm dữ liệu quanh xe, nạp lại (trên luồng DB) khi xe đi xa tâm bộ đệm
    private var cacheFeatures: List<RoadFeature> = emptyList()
    private var cacheZones: List<SpeedZone> = emptyList()
    private var cacheLat = Double.NaN
    private var cacheLng = Double.NaN
    private var cacheVersion = -1
    private var cacheLoadedAt = 0L
    private var cacheLoading = false

    /** Tăng mỗi lần đổi chế độ GPS ↔ mô phỏng, để bỏ kết quả nạp tuyến mô phỏng đã lỗi thời */
    private var modeToken = 0

    // Trạng thái hành trình
    private var lastSignLimit: Int? = null
    private var prevLat = Double.NaN
    private var prevLng = Double.NaN
    private var lastFixAt = 0L
    private var gpsFix = false
    private var wasOverspeed = false
    private var overspeedCount = 0
    private var maxSpeed = 0
    private var distanceM = 0.0
    private val loggedWarnings = HashMap<Long, Long>()
    private var lastNotifAt = 0L
    private var lastSnapshot: DriveSnapshot? = null

    // Mô phỏng
    private var simulating = false
    private var simPoints: List<DoubleArray> = emptyList()
    private var simTotal = 0.0
    private var simPos = 0.0
    private var simTick = 0

    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        db = FeatureDb.get(this)
        prefs = Prefs(this)
        engine = WarningEngine(SignCatalog.byCode)
        announcer = Announcer(this, prefs)
        overlay = OverlayBubble(this)
        locationManager = getSystemService(Context.LOCATION_SERVICE) as LocationManager
        createChannel()
        dbThread.start()
        dbHandler = Handler(dbThread.looper)
        onDb("dọn nhật ký cũ") { db.pruneEvents() }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == ACTION_STOP) {
            stopSelf()
            return START_NOT_STICKY
        }
        if (!hasLocationPermission()) {
            Log.w(TAG, "Thiếu quyền vị trí, dừng service")
            stopSelf()
            return START_NOT_STICKY
        }
        startInForeground()
        isRunning = true
        if (intent?.action == ACTION_SIMULATE) startSimulation() else startGps()
        handler.removeCallbacks(watchdog)
        handler.postDelayed(watchdog, 2000)
        return START_STICKY
    }

    override fun onDestroy() {
        isRunning = false
        stopSimulation()
        locationManager.removeUpdates(this)
        handler.removeCallbacksAndMessages(null)
        dbThread.quitSafely() // ghi nốt nhật ký đang chờ rồi dừng
        overlay.hide()
        announcer.shutdown()
        lastSnapshot?.let { DriveState.publish(it.copy(running = false, simulating = false)) }
        super.onDestroy()
    }

    // ---------------- GPS ----------------

    private fun hasLocationPermission() =
        ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED

    private fun startGps() {
        modeToken++
        stopSimulation()
        resetTrip()
        try {
            locationManager.requestLocationUpdates(LocationManager.GPS_PROVIDER, 1000L, 0f, this, Looper.getMainLooper())
        } catch (e: SecurityException) {
            Log.e(TAG, "Không có quyền GPS", e)
        } catch (e: IllegalArgumentException) {
            Log.e(TAG, "Thiết bị không có GPS", e)
        }
        publishIdle()
    }

    override fun onLocationChanged(loc: Location) {
        if (simulating) return
        val speed = if (loc.hasSpeed()) loc.speed * 3.6 else 0.0
        var heading: Double? = if (loc.hasBearing() && speed >= 5) loc.bearing.toDouble() else null
        if (!prevLat.isNaN()) {
            val moved = Geo.distance(prevLat, prevLng, loc.latitude, loc.longitude)
            if (moved > 8) {
                if (heading == null) heading = Geo.bearing(prevLat, prevLng, loc.latitude, loc.longitude)
                distanceM += moved
                prevLat = loc.latitude
                prevLng = loc.longitude
            }
        } else {
            prevLat = loc.latitude
            prevLng = loc.longitude
        }
        process(loc.latitude, loc.longitude, speed, heading)
    }

    // Các phương thức này là abstract trên Android < 11 — phải khai báo để tránh AbstractMethodError
    @Deprecated("Deprecated in Java")
    override fun onStatusChanged(provider: String?, status: Int, extras: Bundle?) = Unit
    override fun onProviderEnabled(provider: String) = Unit
    override fun onProviderDisabled(provider: String) {
        gpsFix = false
        publishIdle()
    }

    /** Mất tín hiệu GPS > 5 giây → báo cho giao diện */
    private val watchdog = object : Runnable {
        override fun run() {
            if (!simulating && gpsFix && SystemClock.elapsedRealtime() - lastFixAt > 5000) {
                gpsFix = false
                publishIdle()
            }
            handler.postDelayed(this, 2000)
        }
    }

    // ---------------- Mô phỏng (chạy thử trên box không có GPS) ----------------

    private fun startSimulation() {
        locationManager.removeUpdates(this)
        stopSimulation()
        val token = ++modeToken
        dbHandler.post {
            // Lỗi đọc CSDL → zone = null → quay về chạy GPS
            val zone = runCatching { db.longestZone() }.onFailure { Log.e(TAG, "Không nạp được tuyến mô phỏng", it) }.getOrNull()
            handler.post { if (token == modeToken && isRunning) beginSimulation(zone) }
        }
    }

    private fun beginSimulation(zone: SpeedZone?) {
        if (zone == null) {
            announcer.speak("Không có dữ liệu tuyến đường để mô phỏng")
            startGps()
            return
        }
        resetTrip()
        simPoints = zone.points
        simTotal = Geo.polylineLength(simPoints)
        simPos = 0.0
        simTick = 0
        simulating = true
        announcer.speak("Bắt đầu chế độ mô phỏng trên ${zone.name ?: "tuyến mẫu"}")
        handler.removeCallbacks(simRunnable)
        handler.post(simRunnable)
    }

    private fun stopSimulation() {
        simulating = false
        handler.removeCallbacks(simRunnable)
    }

    private val simRunnable = object : Runnable {
        override fun run() {
            if (!simulating) return
            simTick++
            // Cứ mỗi chu kỳ 2 phút có 40 giây chạy nhanh hơn 25 km/h để thử cảnh báo vượt tốc độ
            val boost = if ((simTick / 40) % 3 == 2) 25 else 0
            val speed = (prefs.simSpeed + boost).toDouble()
            simPos += speed / 3.6
            if (simPos > simTotal) {
                simPos = 0.0
                lastSignLimit = null
            }
            val p = Geo.pointAt(simPoints, simPos)
            if (p != null) {
                distanceM += speed / 3.6
                process(p.lat, p.lng, speed, p.bearing)
            }
            handler.postDelayed(this, 1000)
        }
    }

    // ---------------- Xử lý chính ----------------

    private fun resetTrip() {
        lastSignLimit = null
        prevLat = Double.NaN
        prevLng = Double.NaN
        overspeedCount = 0
        maxSpeed = 0
        distanceM = 0.0
        wasOverspeed = false
        loggedWarnings.clear()
        announcer.reset()
    }

    /**
     * Nạp lại bộ đệm trên luồng DB khi: chưa có, dữ liệu vừa đổi, xe đi xa tâm bộ đệm,
     * hoặc đã quá CACHE_MAX_AGE (để điểm vừa hết hạn không còn bị cảnh báo).
     * Trong lúc nạp vẫn dùng bộ đệm cũ.
     */
    private fun refreshCache(lat: Double, lng: Double) {
        if (cacheLoading) return
        val now = SystemClock.elapsedRealtime()
        val version = DriveState.dataVersion
        val stale = cacheLat.isNaN() ||
            cacheVersion != version ||
            now - cacheLoadedAt > CACHE_MAX_AGE ||
            Geo.distance(cacheLat, cacheLng, lat, lng) > CACHE_RADIUS - CACHE_MARGIN
        if (!stale) return
        cacheLoading = true
        dbHandler.post {
            val loaded = runCatching { db.featuresNear(lat, lng, CACHE_RADIUS) to db.zonesNear(lat, lng, CACHE_RADIUS) }
            handler.post {
                cacheLoading = false
                loaded.onSuccess { (features, zones) ->
                    cacheFeatures = features
                    cacheZones = zones
                    cacheLat = lat
                    cacheLng = lng
                    cacheVersion = version
                    cacheLoadedAt = now
                }.onFailure { Log.e(TAG, "Không nạp được dữ liệu quanh xe", it) }
            }
        }
    }

    /** Chạy [work] trên luồng DB; lỗi chỉ ghi log, không làm sập service. */
    private fun onDb(what: String, work: () -> Unit) {
        dbHandler.post {
            try {
                work()
            } catch (e: RuntimeException) {
                Log.e(TAG, "Lỗi CSDL khi $what", e)
            }
        }
    }

    private fun process(lat: Double, lng: Double, speed: Double, heading: Double?) {
        lastFixAt = SystemClock.elapsedRealtime()
        gpsFix = true
        refreshCache(lat, lng)

        engine.config = engine.config.copy(overspeedTolerance = prefs.overspeedTolerance)
        val result = engine.evaluate(
            VehicleState(lat, lng, heading, speed, prefs.vehicleType, lastSignLimit),
            cacheFeatures,
            cacheZones,
        )
        lastSignLimit = result.lastSignLimit
        announcer.onResult(result, speed)
        logEvents(result, lat, lng, speed)

        val speedInt = speed.roundToInt()
        maxSpeed = max(maxSpeed, speedInt)
        val snap = DriveSnapshot(
            running = true,
            simulating = simulating,
            gpsFix = true,
            lat = lat,
            lng = lng,
            speed = speedInt,
            heading = heading,
            result = result,
            overspeedCount = overspeedCount,
            maxSpeed = maxSpeed,
            distanceKm = distanceM / 1000.0,
        )
        lastSnapshot = snap
        DriveState.publish(snap)
        updateOverlay(snap)
        updateNotification(snap)
    }

    private fun logEvents(r: EngineResult, lat: Double, lng: Double, speed: Double) {
        if (r.overspeed && !wasOverspeed) {
            overspeedCount++
            val limit = r.speedLimit
            onDb("ghi nhật ký") { db.logEvent("overspeed", null, null, lat, lng, speed, limit) }
        }
        wasOverspeed = r.overspeed
        val now = SystemClock.elapsedRealtime()
        for (w in r.warnings) {
            val seen = loggedWarnings[w.feature.id]
            if (seen == null || now - seen > 10 * 60_000) {
                val f = w.feature
                onDb("ghi nhật ký") { db.logEvent("warning", f.typeCode, f.id, lat, lng, speed, f.speedLimit) }
            }
            loggedWarnings[w.feature.id] = now
        }
    }

    private fun publishIdle() {
        val snap = (lastSnapshot ?: DriveSnapshot(true, simulating, false, 0.0, 0.0, 0, null, null, 0, 0, 0.0))
            .copy(running = true, simulating = simulating, gpsFix = gpsFix)
        lastSnapshot = snap
        DriveState.publish(snap)
    }

    private fun updateOverlay(s: DriveSnapshot) {
        if (prefs.overlay && !DriveState.uiVisible && overlay.canShow()) {
            overlay.show()
            overlay.update(s)
        } else {
            overlay.hide()
        }
    }

    // ---------------- Thông báo foreground ----------------

    private fun createChannel() {
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val ch = NotificationChannel(CHANNEL_ID, getString(R.string.channel_drive), NotificationManager.IMPORTANCE_LOW)
            ch.setShowBadge(false)
            (getSystemService(NOTIFICATION_SERVICE) as NotificationManager).createNotificationChannel(ch)
        }
    }

    private fun buildNotification(text: String): Notification {
        val open = PendingIntent.getActivity(
            this, 0, Intent(this, MainActivity::class.java),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        val stop = PendingIntent.getService(
            this, 1, Intent(this, DriveService::class.java).setAction(ACTION_STOP),
            PendingIntent.FLAG_UPDATE_CURRENT or PendingIntent.FLAG_IMMUTABLE,
        )
        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_stat_drive)
            .setContentTitle(getString(R.string.app_name))
            .setContentText(text)
            .setOngoing(true)
            .setOnlyAlertOnce(true)
            .setContentIntent(open)
            .addAction(0, getString(R.string.stop), stop)
            .build()
    }

    private fun startInForeground() {
        val n = buildNotification(getString(R.string.notif_starting))
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.Q) {
            startForeground(NOTIF_ID, n, ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION)
        } else {
            startForeground(NOTIF_ID, n)
        }
    }

    private fun updateNotification(s: DriveSnapshot) {
        val now = SystemClock.elapsedRealtime()
        if (now - lastNotifAt < 5000) return
        lastNotifAt = now
        val limit = s.result?.speedLimit?.let { " · Giới hạn $it" } ?: ""
        val prefix = if (s.simulating) "[Mô phỏng] " else ""
        (getSystemService(NOTIFICATION_SERVICE) as NotificationManager)
            .notify(NOTIF_ID, buildNotification("$prefix${s.speed} km/h$limit"))
    }

    companion object {
        private const val TAG = "VietRoad"
        const val ACTION_START = "com.vietroad.app.START"
        const val ACTION_SIMULATE = "com.vietroad.app.SIMULATE"
        const val ACTION_STOP = "com.vietroad.app.STOP"
        private const val CHANNEL_ID = "drive"
        private const val NOTIF_ID = 1
        private const val CACHE_RADIUS = 3000.0
        private const val CACHE_MARGIN = 1500.0
        private const val CACHE_MAX_AGE = 10 * 60_000L

        @Volatile var isRunning = false
            private set

        fun start(context: Context, simulate: Boolean = false) {
            val i = Intent(context, DriveService::class.java).setAction(if (simulate) ACTION_SIMULATE else ACTION_START)
            ContextCompat.startForegroundService(context, i)
        }

        fun stop(context: Context) {
            context.stopService(Intent(context, DriveService::class.java))
        }
    }
}
