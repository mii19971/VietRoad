package com.vietroad.app.drive

import android.content.Context
import android.speech.tts.TextToSpeech
import java.util.Locale

/**
 * Doc canh bao bang giong noi (Text-To-Speech co san trong Android).
 * Can cai goi giong "Tieng Viet" cua Google TTS; chua co thi doc bang giong mac dinh.
 */
class Announcer(context: Context) : TextToSpeech.OnInitListener {
    private val tts = TextToSpeech(context.applicationContext, this)
    private var ready = false

    /** true = may co giong tieng Viet. */
    var hasVietnamese = false
        private set

    // TTS khoi dong bat dong bo; xong thi he thong goi ham nay.
    override fun onInit(status: Int) {
        if (status != TextToSpeech.SUCCESS) return
        val result = tts.setLanguage(Locale("vi", "VN"))
        hasVietnamese = result != TextToSpeech.LANG_MISSING_DATA && result != TextToSpeech.LANG_NOT_SUPPORTED
        ready = true
    }

    fun speak(text: String) {
        if (!ready) return
        // QUEUE_ADD: doc noi tiep, khong cat ngang cau dang doc.
        tts.speak(text, TextToSpeech.QUEUE_ADD, null, text.hashCode().toString())
    }

    fun shutdown() = tts.shutdown()
}
