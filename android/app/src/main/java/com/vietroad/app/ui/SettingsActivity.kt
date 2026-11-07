package com.vietroad.app.ui

import android.content.ActivityNotFoundException
import android.content.Intent
import android.net.Uri
import android.os.Build
import android.os.Bundle
import android.provider.Settings
import android.speech.tts.TextToSpeech
import android.widget.Button
import android.widget.RadioGroup
import android.widget.SeekBar
import android.widget.TextView
import android.widget.Toast
import androidx.activity.result.contract.ActivityResultContracts
import androidx.appcompat.app.AlertDialog
import androidx.appcompat.app.AppCompatActivity
import androidx.appcompat.widget.SwitchCompat
import com.vietroad.app.R
import com.vietroad.app.data.DataIO
import com.vietroad.app.data.FeatureDb
import com.vietroad.app.data.Prefs
import com.vietroad.app.service.Announcer
import com.vietroad.app.service.DriveState
import java.io.File

class SettingsActivity : AppCompatActivity() {

    private lateinit var prefs: Prefs
    private lateinit var db: FeatureDb
    private var announcer: Announcer? = null
    private var exportOnlyUser = true

    private val openDoc = registerForActivityResult(ActivityResultContracts.OpenDocument()) { uri ->
        if (uri != null) askImportMode { replace -> importFrom({ readUri(uri) }, replace) }
    }
    private val createDoc = registerForActivityResult(ActivityResultContracts.CreateDocument("application/json")) { uri ->
        if (uri != null) exportTo(uri)
    }

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)
        setContentView(R.layout.activity_settings)
        prefs = Prefs(this)
        db = FeatureDb.get(this)
        announcer = Announcer(this, prefs)

        // ----- loại xe -----
        val group = findViewById<RadioGroup>(R.id.vehicleGroup)
        group.check(
            when (prefs.vehicleType) {
                "truck" -> R.id.vTruck
                "bus" -> R.id.vBus
                else -> R.id.vCar
            },
        )
        group.setOnCheckedChangeListener { _, id ->
            prefs.vehicleType = when (id) {
                R.id.vTruck -> "truck"
                R.id.vBus -> "bus"
                else -> "car"
            }
        }

        // ----- dung sai vượt tốc -----
        val tolLabel = findViewById<TextView>(R.id.toleranceLabel)
        val tolBar = findViewById<SeekBar>(R.id.toleranceBar)
        tolBar.progress = prefs.overspeedTolerance
        tolLabel.text = getString(R.string.tolerance_label, prefs.overspeedTolerance)
        tolBar.setOnSeekBarChangeListener(seek { v ->
            prefs.overspeedTolerance = v
            tolLabel.text = getString(R.string.tolerance_label, v)
        })

        // ----- công tắc -----
        bindSwitch(R.id.swVoice, prefs.voice) { prefs.voice = it }
        bindSwitch(R.id.swBeep, prefs.beep) { prefs.beep = it }
        bindSwitch(R.id.swAutoStart, prefs.autoStart) { prefs.autoStart = it }
        bindSwitch(R.id.swOverlay, prefs.overlay) { on ->
            prefs.overlay = on
            if (on && Build.VERSION.SDK_INT >= Build.VERSION_CODES.M && !Settings.canDrawOverlays(this)) {
                openSystem(Intent(Settings.ACTION_MANAGE_OVERLAY_PERMISSION, Uri.parse("package:$packageName")))
            }
        }

        // ----- giọng đọc -----
        val volLabel = findViewById<TextView>(R.id.volumeLabel)
        val volBar = findViewById<SeekBar>(R.id.volumeBar)
        fun showVolume(pct: Int) {
            volLabel.text = if (pct == 0) getString(R.string.volume_off) else getString(R.string.volume_label, pct)
        }
        volBar.progress = prefs.minVolume / 10
        showVolume(prefs.minVolume)
        volBar.setOnSeekBarChangeListener(seek { v ->
            prefs.minVolume = v * 10
            showVolume(v * 10)
        })
        findViewById<Button>(R.id.btnTestVoice).setOnClickListener {
            announcer?.test()
            refreshVoiceStatus()
        }
        findViewById<Button>(R.id.btnInstallVoice).setOnClickListener {
            openSystem(Intent(TextToSpeech.Engine.ACTION_INSTALL_TTS_DATA))
        }

        // ----- mô phỏng -----
        val simLabel = findViewById<TextView>(R.id.simLabel)
        val simBar = findViewById<SeekBar>(R.id.simBar)
        simBar.progress = prefs.simSpeed - 30
        simLabel.text = getString(R.string.sim_label, prefs.simSpeed)
        simBar.setOnSeekBarChangeListener(seek { v ->
            prefs.simSpeed = v + 30
            simLabel.text = getString(R.string.sim_label, v + 30)
        })

        // ----- dữ liệu -----
        findViewById<Button>(R.id.btnImport).setOnClickListener {
            try {
                openDoc.launch(arrayOf("application/json", "application/geo+json", "text/plain", "application/octet-stream"))
            } catch (e: ActivityNotFoundException) {
                Toast.makeText(this, R.string.no_file_picker, Toast.LENGTH_LONG).show()
            }
        }
        findViewById<Button>(R.id.btnImportFolder).setOnClickListener { importFromFolder() }
        findViewById<Button>(R.id.btnExportUser).setOnClickListener {
            exportOnlyUser = true
            launchExport("vietroad_diem_tu_them.json")
        }
        findViewById<Button>(R.id.btnExportAll).setOnClickListener {
            exportOnlyUser = false
            launchExport("vietroad_toan_bo.json")
        }
        findViewById<Button>(R.id.btnReset).setOnClickListener {
            AlertDialog.Builder(this)
                .setTitle(R.string.reset_data)
                .setMessage(R.string.reset_confirm)
                .setPositiveButton(android.R.string.ok) { _, _ ->
                    runBg({ db.resetToBundled() }) { r -> toastResult(r) }
                }
                .setNegativeButton(android.R.string.cancel, null)
                .show()
        }

        findViewById<TextView>(R.id.importHint).text = getString(R.string.import_hint, importDir().absolutePath)
    }

    override fun onResume() {
        super.onResume()
        refreshStats()
        refreshVoiceStatus()
        // Người dùng có thể vừa từ chối quyền hiển thị đè
        if (prefs.overlay && Build.VERSION.SDK_INT >= Build.VERSION_CODES.M && !Settings.canDrawOverlays(this)) {
            prefs.overlay = false
            findViewById<SwitchCompat>(R.id.swOverlay).isChecked = false
        }
    }

    override fun onDestroy() {
        announcer?.shutdown()
        announcer = null
        super.onDestroy()
    }

    // ---------------- nhập / xuất ----------------

    /** Thư mục riêng của ứng dụng: chép tệp .json vào đây qua USB/adb, không cần quyền bộ nhớ. */
    private fun importDir(): File = File(getExternalFilesDir(null) ?: filesDir, "import").apply { mkdirs() }

    private fun importFromFolder() {
        val files = importDir().listFiles { f -> f.isFile && (f.name.endsWith(".json") || f.name.endsWith(".geojson")) }
            ?.sortedBy { it.name }.orEmpty()
        if (files.isEmpty()) {
            Toast.makeText(this, getString(R.string.import_folder_empty, importDir().absolutePath), Toast.LENGTH_LONG).show()
            return
        }
        askImportMode { replace ->
            runBg({
                var total = DataIO.Result(0, 0, 0)
                files.forEachIndexed { i, f ->
                    val r = db.importJson(f.readText(Charsets.UTF_8), replace && i == 0)
                    total = DataIO.Result(total.features + r.features, total.zones + r.zones, total.skipped + r.skipped)
                    f.renameTo(File(f.parentFile, f.name + ".done"))
                }
                total
            }) { r -> toastResult(r) }
        }
    }

    private fun askImportMode(then: (replace: Boolean) -> Unit) {
        AlertDialog.Builder(this)
            .setTitle(R.string.import_mode_title)
            .setItems(arrayOf(getString(R.string.import_append), getString(R.string.import_replace))) { _, i -> then(i == 1) }
            .setNegativeButton(android.R.string.cancel, null)
            .show()
    }

    private fun readUri(uri: Uri): String =
        contentResolver.openInputStream(uri)?.bufferedReader(Charsets.UTF_8)?.use { it.readText() }
            ?: throw IllegalStateException("Không đọc được tệp")

    private fun importFrom(read: () -> String, replace: Boolean) {
        runBg({ db.importJson(read(), replace) }) { r -> toastResult(r) }
    }

    private fun launchExport(name: String) {
        try {
            createDoc.launch(name)
        } catch (e: ActivityNotFoundException) {
            // Box không có trình chọn tệp: ghi thẳng vào thư mục ứng dụng
            runBg({
                File(getExternalFilesDir(null) ?: filesDir, name).apply { writeText(db.exportJson(exportOnlyUser)) }
            }) { f -> Toast.makeText(this, getString(R.string.exported_to, f.absolutePath), Toast.LENGTH_LONG).show() }
        }
    }

    private fun exportTo(uri: Uri) {
        runBg({
            contentResolver.openOutputStream(uri)?.use { it.write(db.exportJson(exportOnlyUser).toByteArray(Charsets.UTF_8)) }
        }) { Toast.makeText(this, R.string.export_done, Toast.LENGTH_SHORT).show() }
    }

    private fun toastResult(r: DataIO.Result) {
        DriveState.dataChanged()
        refreshStats()
        Toast.makeText(this, getString(R.string.import_result, r.features, r.zones, r.skipped), Toast.LENGTH_LONG).show()
    }

    // ---------------- tiện ích ----------------

    private fun refreshStats() {
        runBg({ db.stats() }) { s ->
            findViewById<TextView>(R.id.dataStats).text =
                getString(R.string.data_stats, s.features, s.userFeatures, s.zones, s.warnings7d, s.overspeed7d)
        }
    }

    private fun refreshVoiceStatus() {
        findViewById<TextView>(R.id.voiceStatus).text = announcer?.voiceStatus.orEmpty()
    }

    private fun <T> runBg(work: () -> T, done: (T) -> Unit) {
        Thread {
            val result = runCatching(work)
            runOnUiThread {
                if (isFinishing) return@runOnUiThread
                result.onSuccess(done).onFailure { e ->
                    Toast.makeText(this, getString(R.string.error_prefix, e.message ?: e.javaClass.simpleName), Toast.LENGTH_LONG).show()
                }
            }
        }.start()
    }

    private fun bindSwitch(id: Int, value: Boolean, onChange: (Boolean) -> Unit) {
        findViewById<SwitchCompat>(id).apply {
            isChecked = value
            setOnCheckedChangeListener { _, checked -> onChange(checked) }
        }
    }

    private fun openSystem(intent: Intent) {
        try {
            startActivity(intent)
        } catch (e: ActivityNotFoundException) {
            Toast.makeText(this, R.string.not_supported, Toast.LENGTH_LONG).show()
        }
    }

    private fun seek(onChange: (Int) -> Unit) = object : SeekBar.OnSeekBarChangeListener {
        override fun onProgressChanged(sb: SeekBar, progress: Int, fromUser: Boolean) {
            if (fromUser) onChange(progress)
        }
        override fun onStartTrackingTouch(sb: SeekBar) = Unit
        override fun onStopTrackingTouch(sb: SeekBar) = Unit
    }
}
