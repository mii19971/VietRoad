package com.vietroad.app.service

import android.Manifest
import android.content.BroadcastReceiver
import android.content.Context
import android.content.Intent
import android.content.pm.PackageManager
import android.util.Log
import androidx.core.content.ContextCompat
import com.vietroad.app.data.Prefs

/** Android box thường bật cùng chìa khoá xe → tự chạy cảnh báo khi khởi động. */
class BootReceiver : BroadcastReceiver() {
    override fun onReceive(context: Context, intent: Intent) {
        val actions = setOf(
            Intent.ACTION_BOOT_COMPLETED,
            "android.intent.action.QUICKBOOT_POWERON",
            Intent.ACTION_MY_PACKAGE_REPLACED,
        )
        if (intent.action !in actions || !Prefs(context).autoStart) return
        if (ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) != PackageManager.PERMISSION_GRANTED) return
        try {
            DriveService.start(context)
        } catch (e: RuntimeException) {
            Log.w("VietRoad", "Không tự khởi động được service", e)
        }
    }
}
