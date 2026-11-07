package com.vietroad.app.service

import android.annotation.SuppressLint
import android.content.Context
import android.content.Intent
import android.graphics.PixelFormat
import android.graphics.drawable.GradientDrawable
import android.os.Build
import android.provider.Settings
import android.view.Gravity
import android.view.MotionEvent
import android.view.View
import android.view.WindowManager
import android.widget.LinearLayout
import android.widget.TextView
import com.vietroad.app.ui.MainActivity
import com.vietroad.app.ui.SignView
import kotlin.math.abs

/**
 * Bong bóng nổi hiển thị tốc độ + biển giới hạn + cảnh báo gần nhất khi tài xế đang mở
 * ứng dụng khác (bản đồ, nhạc...). Kéo để di chuyển, chạm để mở VietRoad.
 */
class OverlayBubble(private val context: Context) {

    private val wm = context.getSystemService(Context.WINDOW_SERVICE) as WindowManager
    private var root: LinearLayout? = null
    private lateinit var speedText: TextView
    private lateinit var limitSign: SignView
    private lateinit var warnSign: SignView
    private lateinit var warnText: TextView
    private lateinit var bg: GradientDrawable

    fun canShow(): Boolean = Build.VERSION.SDK_INT < Build.VERSION_CODES.M || Settings.canDrawOverlays(context)

    @SuppressLint("ClickableViewAccessibility")
    fun show() {
        if (root != null || !canShow()) return
        val dp = context.resources.displayMetrics.density
        bg = GradientDrawable().apply {
            cornerRadius = 18 * dp
            setColor(0xE60F172A.toInt())
        }
        val layout = LinearLayout(context).apply {
            orientation = LinearLayout.HORIZONTAL
            gravity = Gravity.CENTER_VERTICAL
            background = bg
            setPadding((10 * dp).toInt(), (8 * dp).toInt(), (10 * dp).toInt(), (8 * dp).toInt())
        }
        limitSign = SignView(context)
        layout.addView(limitSign, LinearLayout.LayoutParams((52 * dp).toInt(), (52 * dp).toInt()))
        speedText = TextView(context).apply {
            textSize = 30f
            setTextColor(0xFFFFFFFF.toInt())
            setPadding((10 * dp).toInt(), 0, (10 * dp).toInt(), 0)
            paint.isFakeBoldText = true
        }
        layout.addView(speedText)
        warnSign = SignView(context)
        layout.addView(warnSign, LinearLayout.LayoutParams((44 * dp).toInt(), (44 * dp).toInt()))
        warnText = TextView(context).apply {
            textSize = 16f
            setTextColor(0xFFFDE68A.toInt())
            setPadding((6 * dp).toInt(), 0, 0, 0)
        }
        layout.addView(warnText)

        @Suppress("DEPRECATION")
        val type = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            WindowManager.LayoutParams.TYPE_APPLICATION_OVERLAY
        } else {
            WindowManager.LayoutParams.TYPE_PHONE
        }
        val params = WindowManager.LayoutParams(
            WindowManager.LayoutParams.WRAP_CONTENT,
            WindowManager.LayoutParams.WRAP_CONTENT,
            type,
            WindowManager.LayoutParams.FLAG_NOT_FOCUSABLE or WindowManager.LayoutParams.FLAG_LAYOUT_IN_SCREEN,
            PixelFormat.TRANSLUCENT,
        ).apply {
            gravity = Gravity.TOP or Gravity.START
            x = (16 * dp).toInt()
            y = (80 * dp).toInt()
        }

        var downX = 0f
        var downY = 0f
        var startX = 0
        var startY = 0
        layout.setOnTouchListener { v, e ->
            when (e.action) {
                MotionEvent.ACTION_DOWN -> {
                    downX = e.rawX; downY = e.rawY; startX = params.x; startY = params.y
                    true
                }
                MotionEvent.ACTION_MOVE -> {
                    params.x = startX + (e.rawX - downX).toInt()
                    params.y = startY + (e.rawY - downY).toInt()
                    wm.updateViewLayout(v, params)
                    true
                }
                MotionEvent.ACTION_UP -> {
                    if (abs(e.rawX - downX) < 10 * dp && abs(e.rawY - downY) < 10 * dp) {
                        context.startActivity(
                            Intent(context, MainActivity::class.java).addFlags(Intent.FLAG_ACTIVITY_NEW_TASK),
                        )
                    }
                    true
                }
                else -> false
            }
        }
        try {
            wm.addView(layout, params)
            root = layout
        } catch (e: RuntimeException) {
            root = null
        }
    }

    fun update(s: DriveSnapshot) {
        if (root == null) return
        val r = s.result
        speedText.text = s.speed.toString()
        limitSign.setLimit(r?.speedLimit)
        bg.setColor(if (r?.overspeed == true) 0xE6B91C1C.toInt() else 0xE60F172A.toInt())
        val w = r?.warnings?.firstOrNull()
        if (w == null) {
            warnSign.visibility = View.GONE
            warnText.visibility = View.GONE
        } else {
            warnSign.visibility = View.VISIBLE
            warnText.visibility = View.VISIBLE
            warnSign.setSign(w.feature.typeCode, w.feature.speedLimit)
            warnText.text = "${w.distance} m"
        }
    }

    fun hide() {
        root?.let { runCatching { wm.removeView(it) } }
        root = null
    }
}
