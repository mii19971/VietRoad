package com.vietroad.app.service

import android.content.Context
import android.media.AudioAttributes
import android.media.AudioFocusRequest
import android.media.AudioManager
import android.media.ToneGenerator
import android.os.Build
import android.os.Bundle
import android.os.SystemClock
import android.speech.tts.TextToSpeech
import android.speech.tts.UtteranceProgressListener
import com.vietroad.app.data.Prefs
import com.vietroad.app.engine.Category
import com.vietroad.app.engine.EngineResult
import com.vietroad.app.engine.WarningEngine
import java.util.Locale
import kotlin.math.max

/**
 * Phát cảnh báo bằng giọng nói tiếng Việt (TextToSpeech offline của máy) và tiếng bíp.
 * Mỗi điểm được đọc tối đa 2 lần: khi bắt đầu vào tầm cảnh báo ("Còn 500 mét, ...")
 * và khi đã rất gần (chỉ với điểm quan trọng: camera, đường sắt, điểm đen...).
 */
class Announcer(context: Context, private val prefs: Prefs) : TextToSpeech.OnInitListener {

    private val audio = context.getSystemService(Context.AUDIO_SERVICE) as AudioManager
    private val attrs = AudioAttributes.Builder()
        .setUsage(AudioAttributes.USAGE_ASSISTANCE_NAVIGATION_GUIDANCE)
        .setContentType(AudioAttributes.CONTENT_TYPE_SPEECH)
        .build()
    private val focusListener = AudioManager.OnAudioFocusChangeListener { }
    private var focusRequest: AudioFocusRequest? = null
    private var focusHeld = false
    private var speaking = 0

    private val tts = TextToSpeech(context.applicationContext, this)
    private val tone: ToneGenerator? = try {
        ToneGenerator(AudioManager.STREAM_MUSIC, 90)
    } catch (e: RuntimeException) {
        null
    }

    @Volatile var voiceReady = false
        private set
    @Volatile var voiceStatus = "Đang khởi tạo giọng đọc..."
        private set

    /** featureId -> giai đoạn đã đọc (1 = xa, 2 = gần) */
    private val stage = HashMap<Long, Int>()
    private val lastSeen = HashMap<Long, Long>()
    private var lastOverspeedAt = 0L
    private var lastLimit: Int? = null
    private var utterance = 0

    override fun onInit(status: Int) {
        if (status != TextToSpeech.SUCCESS) {
            voiceStatus = "Không khởi tạo được bộ đọc TTS trên máy"
            return
        }
        val r = tts.setLanguage(Locale("vi", "VN"))
        voiceReady = r != TextToSpeech.LANG_MISSING_DATA && r != TextToSpeech.LANG_NOT_SUPPORTED
        voiceStatus = if (voiceReady) {
            "Giọng đọc tiếng Việt sẵn sàng"
        } else {
            "Máy chưa có giọng tiếng Việt offline — chỉ phát tiếng bíp"
        }
        tts.setSpeechRate(1.05f)
        tts.setAudioAttributes(attrs)
        tts.setOnUtteranceProgressListener(object : UtteranceProgressListener() {
            override fun onStart(utteranceId: String?) = Unit
            override fun onDone(utteranceId: String?) = utteranceFinished()
            @Deprecated("Deprecated in Java")
            override fun onError(utteranceId: String?) = utteranceFinished()
        })
    }

    fun onResult(r: EngineResult, speedKmh: Double) {
        val now = SystemClock.elapsedRealtime()
        val nearDistance = max(120.0, speedKmh / 3.6 * 8) // ~8 giây trước khi tới

        for (w in r.warnings) {
            val id = w.feature.id
            lastSeen[id] = now
            val near = w.distance <= nearDistance
            val prev = stage[id] ?: 0
            val want = if (near) 2 else 1
            if (want <= prev) continue
            stage[id] = want
            val important = w.type.priority <= 2 && w.type.category != Category.INFO
            when {
                prev == 0 && !near -> speak("Còn ${roundDistance(w.distance)} mét, ${w.tts}", beep = important)
                prev == 0 -> speak(w.tts, beep = important)
                important -> speak("Chú ý, ${w.type.name.lowercase(VI)}", beep = true)
            }
        }

        // Quên các điểm đã khuất 60 giây để lần sau đi qua lại được cảnh báo
        val it = lastSeen.entries.iterator()
        while (it.hasNext()) {
            val e = it.next()
            if (now - e.value > 60_000) {
                stage.remove(e.key)
                it.remove()
            }
        }

        // Đổi giới hạn tốc độ (không lặp lại nếu vừa đọc biển P.127)
        val limit = r.speedLimit
        if (limit != null && limit != lastLimit) {
            val justRead = r.warnings.any { it.feature.typeCode == WarningEngine.SPEED_SIGN && it.distance < 80 }
            if (lastLimit != null && !justRead) speak("Giới hạn tốc độ $limit ki lô mét trên giờ")
            lastLimit = limit
        }

        if (r.overspeed && limit != null) {
            val interval = if (r.dangerOverspeed) 8_000 else 15_000
            if (now - lastOverspeedAt > interval) {
                lastOverspeedAt = now
                beep(if (r.dangerOverspeed) 3 else 1)
                speak("Bạn đang chạy quá tốc độ cho phép $limit ki lô mét trên giờ")
            }
        }
    }

    fun speak(text: String, beep: Boolean = false) {
        if (beep) beep(1)
        if (prefs.voice && voiceReady) {
            boostVolume()
            requestFocus()
            synchronized(this) { speaking++ }
            val params = Bundle().apply { putFloat(TextToSpeech.Engine.KEY_PARAM_VOLUME, 1f) }
            tts.speak(text, TextToSpeech.QUEUE_ADD, params, "vr${utterance++}")
        } else if (!beep) {
            beep(1)
        }
    }

    fun beep(times: Int) {
        if (!prefs.beep) return
        val g = tone ?: return
        boostVolume()
        g.startTone(if (times > 1) ToneGenerator.TONE_CDMA_HIGH_PBX_L else ToneGenerator.TONE_PROP_BEEP2, 250 * times)
    }

    fun test() = speak("Xin chào, đây là VietRoad. Phía trước có camera giám sát tốc độ", beep = true)

    fun reset() {
        stage.clear()
        lastSeen.clear()
        lastLimit = null
    }

    fun shutdown() {
        tts.stop()
        tts.shutdown()
        tone?.release()
        abandonFocus()
    }

    // ---------------- âm lượng & audio focus ----------------

    /** Nâng âm lượng media lên mức tối thiểu đã cài (không bao giờ giảm âm lượng người dùng đặt). */
    private fun boostVolume() {
        val pct = prefs.minVolume
        if (pct <= 0) return
        val max = audio.getStreamMaxVolume(AudioManager.STREAM_MUSIC)
        val target = (max * pct + 99) / 100
        if (audio.getStreamVolume(AudioManager.STREAM_MUSIC) < target) {
            try {
                audio.setStreamVolume(AudioManager.STREAM_MUSIC, target, 0)
            } catch (e: SecurityException) {
                // Chế độ Không làm phiền có thể chặn đổi âm lượng
            }
        }
    }

    /** Xin "focus" tạm thời để nhạc/radio tự nhỏ lại trong lúc đọc cảnh báo. */
    private fun requestFocus() {
        if (focusHeld) return
        focusHeld = if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            val req = focusRequest ?: AudioFocusRequest.Builder(AudioManager.AUDIOFOCUS_GAIN_TRANSIENT_MAY_DUCK)
                .setAudioAttributes(attrs)
                .setOnAudioFocusChangeListener(focusListener)
                .build()
                .also { focusRequest = it }
            audio.requestAudioFocus(req) == AudioManager.AUDIOFOCUS_REQUEST_GRANTED
        } else {
            @Suppress("DEPRECATION")
            audio.requestAudioFocus(focusListener, AudioManager.STREAM_MUSIC, AudioManager.AUDIOFOCUS_GAIN_TRANSIENT_MAY_DUCK) ==
                AudioManager.AUDIOFOCUS_REQUEST_GRANTED
        }
    }

    private fun abandonFocus() {
        if (!focusHeld) return
        focusHeld = false
        if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.O) {
            focusRequest?.let { audio.abandonAudioFocusRequest(it) }
        } else {
            @Suppress("DEPRECATION")
            audio.abandonAudioFocus(focusListener)
        }
    }

    /** Gọi từ luồng của TTS khi đọc xong một câu; trả focus khi hàng đợi trống. */
    private fun utteranceFinished() {
        synchronized(this) {
            speaking = max(0, speaking - 1)
            if (speaking == 0) abandonFocus()
        }
    }

    private fun roundDistance(d: Int): Int = if (d < 100) d else ((d + 25) / 50) * 50

    companion object {
        private val VI = Locale("vi", "VN")
    }
}
