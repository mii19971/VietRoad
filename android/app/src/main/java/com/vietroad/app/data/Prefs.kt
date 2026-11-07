package com.vietroad.app.data

import android.content.Context

class Prefs(context: Context) {
    private val sp = context.applicationContext.getSharedPreferences("vietroad", Context.MODE_PRIVATE)

    /** car | truck | bus */
    var vehicleType: String
        get() = sp.getString("vehicle_type", "car") ?: "car"
        set(v) = sp.edit().putString("vehicle_type", v).apply()

    var overspeedTolerance: Int
        get() = sp.getInt("overspeed_tolerance", 3)
        set(v) = sp.edit().putInt("overspeed_tolerance", v).apply()

    var voice: Boolean
        get() = sp.getBoolean("voice", true)
        set(v) = sp.edit().putBoolean("voice", v).apply()

    /** Khi cảnh báo, nâng âm lượng media lên ít nhất mức này (% so với tối đa). 0 = không can thiệp. */
    var minVolume: Int
        get() = sp.getInt("min_volume", 80)
        set(v) = sp.edit().putInt("min_volume", v).apply()

    var beep: Boolean
        get() = sp.getBoolean("beep", true)
        set(v) = sp.edit().putBoolean("beep", v).apply()

    /** Bong bóng tốc độ nổi trên ứng dụng khác (bản đồ, nhạc...) */
    var overlay: Boolean
        get() = sp.getBoolean("overlay", false)
        set(v) = sp.edit().putBoolean("overlay", v).apply()

    /** Tự chạy khi Android box khởi động (bật chìa khoá) */
    var autoStart: Boolean
        get() = sp.getBoolean("auto_start", true)
        set(v) = sp.edit().putBoolean("auto_start", v).apply()

    var simSpeed: Int
        get() = sp.getInt("sim_speed", 80)
        set(v) = sp.edit().putInt("sim_speed", v).apply()
}
