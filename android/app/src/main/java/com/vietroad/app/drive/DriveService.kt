package com.vietroad.app.drive

import android.app.NotificationChannel
import android.app.NotificationManager
import android.app.PendingIntent
import android.app.Service
import android.content.Context
import android.content.Intent
import android.content.pm.ServiceInfo
import android.os.Build
import android.os.IBinder
import androidx.core.app.NotificationCompat
import androidx.core.app.ServiceCompat
import androidx.core.content.ContextCompat
import com.vietroad.app.MainActivity
import com.vietroad.app.R
import com.vietroad.app.VietRoadApp
import com.vietroad.app.data.AlertTypes
import kotlinx.coroutines.CoroutineScope
import kotlinx.coroutines.Dispatchers
import kotlinx.coroutines.SupervisorJob
import kotlinx.coroutines.cancel
import kotlinx.coroutines.flow.distinctUntilChanged
import kotlinx.coroutines.flow.map
import kotlinx.coroutines.launch

/**
 * Dich vu chay nen (Foreground Service) giu cho VietRoad hoat dong khi:
 *  - tat man hinh, chuyen sang app khac (Google Maps, nghe nhac...), vuot app khoi danh sach gan day.
 *
 * Luat cua Android: app chay nen muon dung GPS lien tuc phai la "foreground service"
 * va PHAI hien 1 thong bao (notification) thuong truc de nguoi dung biet. Thong bao nay
 * dong thoi hien canh bao gan nhat + nut "Dung".
 *
 * Service chi bat/tat DriveEngine; moi logic nam trong engine.
 */
class DriveService : Service() {
    private val scope = CoroutineScope(SupervisorJob() + Dispatchers.Main.immediate)
    private val engine get() = (application as VietRoadApp).engine

    // Service kieu "started" (khong cho Activity "bind" vao) -> tra ve null.
    override fun onBind(intent: Intent?): IBinder? = null

    override fun onCreate() {
        super.onCreate()
        createChannel()

        // Phai goi startForeground som (trong vai giay sau khi start) neu khong he thong bao loi.
        // Kieu LOCATION: bat buoc tu Android 14 cho service dung GPS.
        ServiceCompat.startForeground(
            this, NOTIFICATION_ID, buildNotification(DriveState()),
            ServiceInfo.FOREGROUND_SERVICE_TYPE_LOCATION,
        )

        // Cap nhat thong bao khi noi dung doi. Khoang cach lam tron 50 m de khong cap nhat moi giay.
        scope.launch {
            engine.state
                .map { notificationText(it) }
                .distinctUntilChanged()
                .collect { notificationManager().notify(NOTIFICATION_ID, buildNotification(engine.state.value)) }
        }
    }

    override fun onStartCommand(intent: Intent?, flags: Int, startId: Int): Int {
        if (intent?.action == ACTION_STOP) {
            // Nguoi dung bam "Dung" tren thong bao.
            stopSelf()
            return START_NOT_STICKY
        }
        engine.start()
        // NOT_STICKY: neu he thong buoc phai giet app (het RAM) thi khong tu bat lai ngam.
        return START_NOT_STICKY
    }

    override fun onDestroy() {
        engine.stop()
        scope.cancel()
        super.onDestroy()
    }

    // ---------- Thong bao ----------

    private fun notificationManager() = getSystemService(Context.NOTIFICATION_SERVICE) as NotificationManager

    /** Android 8+: thong bao phai thuoc 1 "kenh". Muc LOW: hien thi nhung khong keu (da co giong noi). */
    private fun createChannel() {
        if (Build.VERSION.SDK_INT < Build.VERSION_CODES.O) return
        val channel = NotificationChannel(CHANNEL_ID, "Cảnh báo khi lái xe", NotificationManager.IMPORTANCE_LOW)
        channel.description = "Hiện khi VietRoad đang theo dõi vị trí để cảnh báo."
        notificationManager().createNotificationChannel(channel)
    }

    private fun buildNotification(state: DriveState): android.app.Notification {
        // Bam vao thong bao -> mo lai app. FLAG_IMMUTABLE: bat buoc tu Android 12.
        val immutable = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.M) PendingIntent.FLAG_IMMUTABLE else 0
        val openApp = PendingIntent.getActivity(
            this, 0,
            Intent(this, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_SINGLE_TOP),
            immutable or PendingIntent.FLAG_UPDATE_CURRENT,
        )
        val stop = PendingIntent.getService(
            this, 1,
            Intent(this, DriveService::class.java).setAction(ACTION_STOP),
            immutable or PendingIntent.FLAG_UPDATE_CURRENT,
        )

        return NotificationCompat.Builder(this, CHANNEL_ID)
            .setSmallIcon(R.drawable.ic_stat_vietroad)
            .setContentTitle("VietRoad đang cảnh báo")
            .setContentText(notificationText(state))
            .setContentIntent(openApp)
            .addAction(0, "Dừng", stop)
            .setOngoing(true)           // khong vuot di duoc
            .setOnlyAlertOnce(true)     // cap nhat noi dung khong rung / keu lai
            .setCategory(NotificationCompat.CATEGORY_NAVIGATION)
            .build()
    }

    companion object {
        private const val CHANNEL_ID = "drive"
        private const val NOTIFICATION_ID = 1
        private const val ACTION_STOP = "com.vietroad.app.STOP_DRIVE"

        /** Bat dich vu. Goi tu man hinh (dang hien) sau khi da co quyen vi tri. */
        fun start(context: Context) =
            ContextCompat.startForegroundService(context, Intent(context, DriveService::class.java))

        fun stop(context: Context) {
            context.stopService(Intent(context, DriveService::class.java))
        }

        /** Ham thuan -> unit test duoc. Vd: "Camera tốc độ (60 km/h) · còn 450 m". */
        fun notificationText(state: DriveState): String {
            if (!state.tracking || state.position == null) return "Đang chờ tín hiệu vị trí…"
            state.nav?.let { nav ->
                // Dang chi duong: hien chi dan sap toi. Vd "Rẽ trái vào Phố Huế · 300 m"
                if (nav.rerouting) return "Đang tính lại đường đi…"
                val instruction = nav.instruction ?: return "Chỉ đường đến ${nav.destinationName}"
                return "$instruction · ${NavFormat.distance(nav.distanceToStepM ?: 0)}"
            }
            val next = state.ahead.firstOrNull() ?: return "Không có cảnh báo phía trước"
            val limit = next.alert.speedLimit?.let { " ($it km/h)" } ?: ""
            val rounded = (next.distanceM + 25) / 50 * 50
            return "${AlertTypes.label(next.alert.type)}$limit · còn $rounded m"
        }
    }
}
