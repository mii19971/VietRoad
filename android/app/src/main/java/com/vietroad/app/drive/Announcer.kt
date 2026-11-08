package com.vietroad.app.drive

import android.content.Context
import android.speech.tts.TextToSpeech
import android.os.SystemClock
import android.util.Log
import java.util.Locale

/**
 * Doc canh bao bang giong noi (Text-To-Speech co san trong Android).
 * Can cai goi giong "Tieng Viet" cua Google TTS; chua co thi doc bang giong mac dinh.
 */
class Announcer(context: Context) : TextToSpeech.OnInitListener {
    private val tts = TextToSpeech(context.applicationContext, this)
    private var ready = false

    // Cau goi speak() truoc khi TTS khoi dong xong (vd "Bắt đầu chỉ đường...") -> giu lai, doc sau.
    private val pending = mutableListOf<String>()

    /** true = may co giong tieng Viet. */
    var hasVietnamese = false
        private set

    // TTS khoi dong bat dong bo; xong thi he thong goi ham nay (tren luong chinh).
    override fun onInit(status: Int) {
        if (status != TextToSpeech.SUCCESS) return
        val result = tts.setLanguage(Locale("vi", "VN"))
        hasVietnamese = result != TextToSpeech.LANG_MISSING_DATA && result != TextToSpeech.LANG_NOT_SUPPORTED
        ready = true
        pending.forEach(::speak)
        pending.clear()
    }

    // Cau vua doc + luc doc: bo cau giong het trong 10 giay
    // (vd 2 camera trung nhau trong du lieu OSM, nam sat nhau -> "Sắp tới Camera đèn đỏ" 2 lan).
    private var lastText: String? = null
    private var lastAt = 0L

    fun speak(text: String) {
        val now = SystemClock.elapsedRealtime()
        if (text == lastText && now - lastAt < REPEAT_WINDOW_MS) return
        lastText = text
        lastAt = now

        // Ghi log de kiem tra tren may that / may ao: adb logcat -s VietRoad
        Log.i("VietRoad", "Đọc: $text")
        if (!ready) {
            if (pending.size < 5) pending += text
            return
        }
        // QUEUE_ADD: doc noi tiep, khong cat ngang cau dang doc.
        tts.speak(text, TextToSpeech.QUEUE_ADD, null, text.hashCode().toString())
    }

    fun shutdown() = tts.shutdown()

    private companion object {
        const val REPEAT_WINDOW_MS = 10_000L
    }
}
