package com.vietroad.app.ui

import android.content.Context
import android.graphics.Canvas
import android.graphics.Paint
import android.graphics.Path
import android.graphics.RectF
import android.graphics.Typeface
import android.util.AttributeSet
import android.view.View
import com.vietroad.app.data.SignCatalog
import com.vietroad.app.engine.Category
import kotlin.math.min

/** Vẽ biển báo dạng vector (không cần ảnh) theo nhóm và mã biển QCVN 41. */
class SignView @JvmOverloads constructor(context: Context, attrs: AttributeSet? = null) : View(context, attrs) {

    private var code: String? = null
    private var value: Int? = null

    private val fill = Paint(Paint.ANTI_ALIAS_FLAG).apply { style = Paint.Style.FILL }
    private val stroke = Paint(Paint.ANTI_ALIAS_FLAG).apply { style = Paint.Style.STROKE; strokeCap = Paint.Cap.ROUND }
    private val text = Paint(Paint.ANTI_ALIAS_FLAG).apply {
        textAlign = Paint.Align.CENTER
        typeface = Typeface.DEFAULT_BOLD
    }
    private val path = Path()
    private val rect = RectF()

    fun setSign(code: String?, value: Int? = null) {
        if (this.code == code && this.value == value) return
        this.code = code
        this.value = value
        invalidate()
    }

    /** Biển giới hạn tốc độ; null → vòng xám "--" (chưa xác định). */
    fun setLimit(limit: Int?) = setSign(if (limit == null) NONE else "P.127", limit)

    override fun onDraw(canvas: Canvas) {
        val c = code ?: return
        val cx = width / 2f
        val cy = height / 2f
        val r = min(width, height) / 2f * 0.94f
        val category = SignCatalog.byCode[c]?.category

        when {
            c == NONE -> {
                ring(canvas, cx, cy, r, WHITE_DIM, GREY, 0.14f)
                label(canvas, "--", cx, cy, r, BLACK, 0.9f)
            }
            c == "P.127" -> {
                ring(canvas, cx, cy, r, WHITE, RED, 0.17f)
                label(canvas, value?.toString() ?: "", cx, cy, r, BLACK, 0.95f)
            }
            c == "DP.134" -> {
                ring(canvas, cx, cy, r, WHITE, BLACK, 0.05f)
                label(canvas, value?.toString() ?: "", cx, cy, r, GREY, 0.95f)
                slash(canvas, cx, cy, r * 0.8f, BLACK, 0.06f)
            }
            c == "P.102" -> {
                disc(canvas, cx, cy, r, RED)
                fill.color = WHITE
                rect.set(cx - r * 0.65f, cy - r * 0.16f, cx + r * 0.65f, cy + r * 0.16f)
                canvas.drawRect(rect, fill)
            }
            c == "P.130" || c == "P.131a" -> {
                ring(canvas, cx, cy, r, BLUE, RED, 0.17f)
                slash(canvas, cx, cy, r * 0.83f, RED, 0.14f)
                if (c == "P.130") slash(canvas, cx, cy, r * 0.83f, RED, 0.14f, mirror = true)
            }
            category == Category.PROHIBITION || category == Category.SPEED -> {
                ring(canvas, cx, cy, r, WHITE, RED, 0.17f)
                val g = SignCatalog.glyph(c).let { if (c == "P.115" && value != null) "${value}t" else it }
                label(canvas, g, cx, cy, r, BLACK, 0.8f)
                if (c in TURN_BANS) slash(canvas, cx, cy, r * 0.83f, RED, 0.12f)
            }
            category == Category.DANGER || category == Category.HAZARD -> {
                triangle(canvas, cx, cy, r, if (category == Category.HAZARD) WHITE else YELLOW, RED)
                label(canvas, SignCatalog.glyph(c).ifEmpty { "!" }, cx, cy + r * 0.22f, r * 0.62f, BLACK, 0.9f)
            }
            category == Category.MANDATORY -> {
                disc(canvas, cx, cy, r, BLUE)
                label(canvas, SignCatalog.glyph(c), cx, cy, r, WHITE, 1f)
            }
            category == Category.CAMERA -> drawCamera(canvas, c, cx, cy, r)
            else -> { // INFO
                roundRect(canvas, cx, cy, r, BLUE)
                label(canvas, SignCatalog.glyph(c), cx, cy, r, WHITE, 0.85f)
            }
        }
    }

    private fun drawCamera(canvas: Canvas, c: String, cx: Float, cy: Float, r: Float) {
        roundRect(canvas, cx, cy, r, if (c == "CAM_MOBILE") ORANGE else BLUE)
        fill.color = WHITE
        rect.set(cx - r * 0.55f, cy - r * 0.32f, cx + r * 0.38f, cy + r * 0.22f)
        canvas.drawRoundRect(rect, r * 0.08f, r * 0.08f, fill)
        path.reset() // ống kính hướng sang phải
        path.moveTo(cx + r * 0.38f, cy - r * 0.12f)
        path.lineTo(cx + r * 0.62f, cy - r * 0.28f)
        path.lineTo(cx + r * 0.62f, cy + r * 0.18f)
        path.lineTo(cx + r * 0.38f, cy + r * 0.02f)
        path.close()
        canvas.drawPath(path, fill)
        fill.color = if (c == "CAM_REDLIGHT") RED else BLUE
        canvas.drawCircle(cx - r * 0.1f, cy - r * 0.05f, r * 0.13f, fill)
        val caption = when (c) {
            "CAM_SPEED" -> "TỐC ĐỘ"
            "CAM_REDLIGHT" -> "ĐÈN ĐỎ"
            "CAM_MOBILE" -> "CSGT"
            else -> "CAMERA"
        }
        text.color = WHITE
        text.textSize = r * 0.26f
        fitText(caption, r * 1.5f)
        canvas.drawText(caption, cx, cy + r * 0.62f, text)
    }

    // ---------------- primitives ----------------

    private fun disc(canvas: Canvas, cx: Float, cy: Float, r: Float, color: Int) {
        fill.color = color
        canvas.drawCircle(cx, cy, r, fill)
    }

    private fun ring(canvas: Canvas, cx: Float, cy: Float, r: Float, inner: Int, border: Int, width: Float) {
        disc(canvas, cx, cy, r, inner)
        stroke.color = border
        stroke.strokeWidth = r * width
        canvas.drawCircle(cx, cy, r - stroke.strokeWidth / 2, stroke)
    }

    private fun slash(canvas: Canvas, cx: Float, cy: Float, r: Float, color: Int, width: Float, mirror: Boolean = false) {
        stroke.color = color
        stroke.strokeWidth = r * width / 0.83f
        val d = r * 0.707f
        if (mirror) canvas.drawLine(cx + d, cy - d, cx - d, cy + d, stroke)
        else canvas.drawLine(cx - d, cy - d, cx + d, cy + d, stroke)
    }

    private fun triangle(canvas: Canvas, cx: Float, cy: Float, r: Float, inner: Int, border: Int) {
        path.reset()
        path.moveTo(cx, cy - r)
        path.lineTo(cx + r, cy + r * 0.78f)
        path.lineTo(cx - r, cy + r * 0.78f)
        path.close()
        fill.color = inner
        canvas.drawPath(path, fill)
        stroke.color = border
        stroke.strokeWidth = r * 0.14f
        stroke.strokeJoin = Paint.Join.ROUND
        canvas.drawPath(path, stroke)
    }

    private fun roundRect(canvas: Canvas, cx: Float, cy: Float, r: Float, color: Int) {
        fill.color = color
        rect.set(cx - r, cy - r, cx + r, cy + r)
        canvas.drawRoundRect(rect, r * 0.22f, r * 0.22f, fill)
    }

    private fun label(canvas: Canvas, s: String, cx: Float, cy: Float, r: Float, color: Int, scale: Float) {
        if (s.isEmpty()) return
        text.color = color
        text.textSize = r * scale * (if (s.length >= 3) 0.62f else 0.9f)
        fitText(s, r * 1.45f)
        val fm = text.fontMetrics
        canvas.drawText(s, cx, cy - (fm.ascent + fm.descent) / 2, text)
    }

    private fun fitText(s: String, maxWidth: Float) {
        val w = text.measureText(s)
        if (w > maxWidth) text.textSize = text.textSize * maxWidth / w
    }

    companion object {
        private const val NONE = "__none__"
        private val TURN_BANS = setOf("P.123a", "P.123b", "P.124a", "P.124c")
        private const val WHITE = 0xFFFFFFFF.toInt()
        private const val WHITE_DIM = 0xFFE2E8F0.toInt()
        private const val BLACK = 0xFF111111.toInt()
        private const val GREY = 0xFF64748B.toInt()
        private const val RED = 0xFFDC2626.toInt()
        private const val BLUE = 0xFF1D4ED8.toInt()
        private const val YELLOW = 0xFFFACC15.toInt()
        private const val ORANGE = 0xFFEA580C.toInt()
    }
}
