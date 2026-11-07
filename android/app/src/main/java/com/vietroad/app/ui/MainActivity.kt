package com.vietroad.app.ui

import android.Manifest
import android.content.Intent
import android.content.pm.PackageManager
import android.os.Build
import android.os.Bundle
import android.text.InputType
import android.view.Gravity
import android.view.View
import android.widget.Button
import android.widget.EditText
import android.widget.LinearLayout
import android.widget.TextView
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.core.content.ContextCompat
import com.vietroad.app.R
import com.vietroad.app.data.FeatureDb
import com.vietroad.app.data.Prefs
import com.vietroad.app.data.SignCatalog
import com.vietroad.app.engine.Category
import com.vietroad.app.service.DriveService
import com.vietroad.app.service.DriveSnapshot
import com.vietroad.app.service.DriveState
import java.util.Locale

class MainActivity : AppCompatActivity() {

    private lateinit var root: View
    private lateinit var limitSign: SignView
    private lateinit var speedText: TextView
    private lateinit var statusText: TextView
    private lateinit var tripText: TextView
    private lateinit var warnCard: View
    private lateinit var warnSign: SignView
    private lateinit var warnDistance: TextView
    private lateinit var warnName: TextView
    private lateinit var warnRoad: TextView
    private lateinit var nextList: LinearLayout
    private lateinit var btnStart: Button
    private lateinit var btnSim: Button
    private lateinit var prefs: Prefs

    private var pendingAction: (() -> Unit)? = null
    private val listener: (DriveSnapshot) -> Unit = { render(it) }

    private val permissionLauncher = registerForActivityResult(ActivityResultContracts.RequestMultiplePermissions()) { res ->
        val granted = res[Manifest.permission.ACCESS_FINE_LOCATION] == true || hasLocation()
        if (granted) pendingAction?.invoke()
        else Toast.makeText(this, R.string.need_location, Toast.LENGTH_LONG).show()
        pendingAction = null
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_main)
        prefs = Prefs(this)

        root = findViewById(R.id.root)
        limitSign = findViewById(R.id.limitSign)
        speedText = findViewById(R.id.speedText)
        statusText = findViewById(R.id.statusText)
        tripText = findViewById(R.id.tripText)
        warnCard = findViewById(R.id.warnCard)
        warnSign = findViewById(R.id.warnSign)
        warnDistance = findViewById(R.id.warnDistance)
        warnName = findViewById(R.id.warnName)
        warnRoad = findViewById(R.id.warnRoad)
        nextList = findViewById(R.id.nextList)
        btnStart = findViewById(R.id.btnStart)
        btnSim = findViewById(R.id.btnSim)

        btnStart.setOnClickListener {
            if (DriveService.isRunning && DriveState.last?.simulating != true) DriveService.stop(this)
            else withPermissions { DriveService.start(this) }
            btnStart.postDelayed({ updateButtons() }, 300)
        }
        btnSim.setOnClickListener {
            if (DriveService.isRunning && DriveState.last?.simulating == true) DriveService.stop(this)
            else withPermissions { DriveService.start(this, simulate = true) }
            btnSim.postDelayed({ updateButtons() }, 300)
        }
        findViewById<Button>(R.id.btnAdd).setOnClickListener { showAddDialog() }
        findViewById<Button>(R.id.btnSettings).setOnClickListener {
            startActivity(Intent(this, SettingsActivity::class.java))
        }

        limitSign.setLimit(null)
        if (prefs.autoStart && !DriveService.isRunning) withPermissions { DriveService.start(this) }
    }

    override fun onStart() {
        super.onStart()
        DriveState.uiVisible = true
        DriveState.addListener(listener)
        DriveState.last?.let { render(it) } ?: renderIdle()
        updateButtons()
    }

    override fun onStop() {
        DriveState.uiVisible = false
        DriveState.removeListener(listener)
        super.onStop()
    }

    // ---------------- quyền ----------------

    private fun hasLocation() =
        ContextCompat.checkSelfPermission(this, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED

    private fun withPermissions(action: () -> Unit) {
        val needed = mutableListOf<String>()
        if (!hasLocation()) {
            needed += Manifest.permission.ACCESS_FINE_LOCATION
            needed += Manifest.permission.ACCESS_COARSE_LOCATION
        }
        if (Build.VERSION.SDK_INT >= 33 &&
            ContextCompat.checkSelfPermission(this, Manifest.permission.POST_NOTIFICATIONS) != PackageManager.PERMISSION_GRANTED
        ) needed += Manifest.permission.POST_NOTIFICATIONS
        if (needed.isEmpty()) {
            action()
            return
        }
        // Từ chối quyền thông báo không chặn việc chạy; chỉ cần quyền vị trí
        pendingAction = action
        permissionLauncher.launch(needed.toTypedArray())
    }

    // ---------------- hiển thị ----------------

    private fun updateButtons() {
        val running = DriveService.isRunning
        val sim = running && DriveState.last?.simulating == true
        btnStart.setText(if (running && !sim) R.string.stop else R.string.start)
        btnSim.setText(if (sim) R.string.stop_sim else R.string.simulate)
    }

    private fun renderIdle() {
        speedText.text = "0"
        limitSign.setLimit(null)
        statusText.setText(R.string.status_stopped)
        showNoWarning()
    }

    private fun render(s: DriveSnapshot) {
        updateButtons()
        if (!s.running) {
            renderIdle()
            return
        }
        val r = s.result
        speedText.text = s.speed.toString()
        limitSign.setLimit(r?.speedLimit)
        val bgColor = when {
            r?.dangerOverspeed == true -> R.color.overspeed_danger
            r?.overspeed == true -> R.color.overspeed
            else -> R.color.bg
        }
        root.setBackgroundColor(ContextCompat.getColor(this, bgColor))

        statusText.text = when {
            s.simulating -> getString(R.string.status_sim)
            s.gpsFix -> getString(R.string.status_gps_ok)
            else -> getString(R.string.status_gps_wait)
        }
        tripText.text = String.format(
            Locale.US, "%.1f km · tối đa %d km/h · vượt tốc %d lần", s.distanceKm, s.maxSpeed, s.overspeedCount,
        )

        val warnings = r?.warnings.orEmpty()
        val first = warnings.firstOrNull()
        if (first == null) showNoWarning() else {
            warnSign.visibility = View.VISIBLE
            warnSign.setSign(first.feature.typeCode, first.feature.speedLimit)
            warnDistance.text = "${first.distance} m"
            warnName.text = first.type.name + (first.feature.speedLimit?.takeIf { first.type.category == Category.SPEED }?.let { " $it km/h" } ?: "")
            warnRoad.text = first.feature.roadName.orEmpty()
            warnCard.setBackgroundResource(if (first.type.priority <= 1) R.drawable.card_alert else R.drawable.card)
        }

        nextList.removeAllViews()
        val dp = resources.displayMetrics.density
        for (w in warnings.drop(1).take(3)) {
            val row = LinearLayout(this).apply {
                orientation = LinearLayout.HORIZONTAL
                gravity = Gravity.CENTER_VERTICAL
                setPadding(0, (6 * dp).toInt(), 0, 0)
            }
            row.addView(SignView(this).apply { setSign(w.feature.typeCode, w.feature.speedLimit) },
                LinearLayout.LayoutParams((40 * dp).toInt(), (40 * dp).toInt()))
            row.addView(TextView(this).apply {
                text = "${w.distance} m · ${w.type.name}"
                textSize = 17f
                setTextColor(ContextCompat.getColor(this@MainActivity, R.color.text))
                setPadding((12 * dp).toInt(), 0, 0, 0)
            })
            nextList.addView(row)
        }
    }

    private fun showNoWarning() {
        warnSign.visibility = View.INVISIBLE
        warnDistance.text = ""
        warnName.setText(R.string.no_warning)
        warnRoad.text = ""
        warnCard.setBackgroundResource(R.drawable.card)
        nextList.removeAllViews()
    }

    // ---------------- thêm điểm tại vị trí hiện tại ----------------

    private fun showAddDialog() {
        val s = DriveState.last
        if (s == null || !s.running || !s.gpsFix) {
            Toast.makeText(this, R.string.need_gps_fix, Toast.LENGTH_LONG).show()
            return
        }
        val types = SignCatalog.all.sortedWith(compareBy({ it.category.ordinal }, { it.priority }))
        AlertDialog.Builder(this)
            .setTitle(R.string.add_point_title)
            .setItems(types.map { "${it.category.label} · ${it.name}" }.toTypedArray()) { _, i ->
                val t = types[i]
                if (t.code == "P.127" || t.code == "P.115") askValue(t.code, s) else saveFeature(t.code, s, null)
            }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }

    private fun askValue(code: String, s: DriveSnapshot) {
        val input = EditText(this).apply {
            inputType = InputType.TYPE_CLASS_NUMBER
            hint = if (code == "P.127") "Ví dụ: 60" else "Số tấn, ví dụ: 10"
        }
        AlertDialog.Builder(this)
            .setTitle(if (code == "P.127") "Tốc độ tối đa (km/h)" else "Trọng tải tối đa (tấn)")
            .setView(input)
            .setPositiveButton(android.R.string.ok) { _, _ ->
                val v = input.text.toString().toIntOrNull()
                if (v == null || v !in 5..150) Toast.makeText(this, "Giá trị không hợp lệ", Toast.LENGTH_SHORT).show()
                else saveFeature(code, s, v)
            }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }

    private fun saveFeature(code: String, s: DriveSnapshot, value: Int?) {
        // Điểm gắn với hướng xe đang chạy (nếu có) để chỉ cảnh báo đúng chiều
        FeatureDb.get(this).addUserFeature(code, s.lat, s.lng, s.heading, value)
        DriveState.dataChanged()
        Toast.makeText(this, getString(R.string.point_saved, SignCatalog.byCode[code]?.name), Toast.LENGTH_SHORT).show()
    }
}
