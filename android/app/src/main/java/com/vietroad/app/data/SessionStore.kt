package com.vietroad.app.data

import android.content.Context

/**
 * Luu phien dang nhap (token) tren may, de mo lai app khong phai dang nhap lai.
 * SharedPreferences = file key-value rieng cua app, app khac khong doc duoc.
 * (Ban phat hanh nen ma hoa them, vd bang Android Keystore.)
 */
class SessionStore(context: Context) {
    private val prefs = context.getSharedPreferences("session", Context.MODE_PRIVATE)

    val accessToken: String? get() = prefs.getString(KEY_ACCESS, null)
    val refreshToken: String? get() = prefs.getString(KEY_REFRESH, null)
    val displayName: String? get() = prefs.getString(KEY_NAME, null)
    val isLoggedIn: Boolean get() = refreshToken != null

    fun save(auth: AuthResponse) {
        prefs.edit()
            .putString(KEY_ACCESS, auth.accessToken)
            .putString(KEY_REFRESH, auth.refreshToken)
            .putString(KEY_NAME, auth.user.displayName)
            .apply() // ghi xuong file o nen, khong chan giao dien
    }

    fun clear() = prefs.edit().clear().apply()

    private companion object {
        const val KEY_ACCESS = "access_token"
        const val KEY_REFRESH = "refresh_token"
        const val KEY_NAME = "display_name"
    }
}
