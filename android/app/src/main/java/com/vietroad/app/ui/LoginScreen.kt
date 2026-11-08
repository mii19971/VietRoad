package com.vietroad.app.ui

import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.widthIn
import androidx.compose.foundation.rememberScrollState
import androidx.compose.foundation.verticalScroll
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedTextField
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.saveable.rememberSaveable
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.text.input.KeyboardType
import androidx.compose.ui.text.input.PasswordVisualTransformation
import androidx.compose.ui.unit.dp
import com.vietroad.app.BuildConfig

/**
 * Man hinh dang nhap / dang ky.
 * Composable = ham ve giao dien. Khi state doi, Compose tu goi lai ham de ve lai.
 */
@Composable
fun LoginScreen(
    busy: Boolean,
    message: String?,
    onLogin: (email: String, password: String) -> Unit,
    onRegister: (email: String, password: String, name: String) -> Unit,
) {
    // rememberSaveable: giu gia tri o nhap ke ca khi xoay man hinh.
    var isRegister by rememberSaveable { mutableStateOf(false) }
    var email by rememberSaveable { mutableStateOf("") }
    var password by rememberSaveable { mutableStateOf("") }
    var name by rememberSaveable { mutableStateOf("") }

    // Box can giua + widthIn(max): tren tablet / man hinh ngang, form khong bi keo dai het be ngang.
    // verticalScroll: man hinh thap (dien thoai xoay ngang + ban phim) van cuon toi duoc moi nut.
    Box(Modifier.fillMaxSize(), contentAlignment = Alignment.Center) {
    Column(
        modifier = Modifier
            .widthIn(max = 480.dp)
            .fillMaxWidth()
            .verticalScroll(rememberScrollState())
            .padding(24.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Text("VietRoad", style = MaterialTheme.typography.headlineLarge)
        Text(if (isRegister) "Tạo tài khoản" else "Đăng nhập", style = MaterialTheme.typography.titleMedium)

        OutlinedTextField(
            value = email, onValueChange = { email = it },
            label = { Text("Email") }, singleLine = true,
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Email),
            modifier = Modifier.fillMaxWidth(),
        )
        if (isRegister) {
            OutlinedTextField(
                value = name, onValueChange = { name = it },
                label = { Text("Tên hiển thị (không bắt buộc)") }, singleLine = true,
                modifier = Modifier.fillMaxWidth(),
            )
        }
        OutlinedTextField(
            value = password, onValueChange = { password = it },
            label = { Text(if (isRegister) "Mật khẩu (ít nhất 8 ký tự)" else "Mật khẩu") }, singleLine = true,
            visualTransformation = PasswordVisualTransformation(), // hien dau cham thay vi chu
            keyboardOptions = KeyboardOptions(keyboardType = KeyboardType.Password),
            modifier = Modifier.fillMaxWidth(),
        )

        if (message != null) {
            Text(message, color = MaterialTheme.colorScheme.error)
        }

        Button(
            onClick = { if (isRegister) onRegister(email, password, name) else onLogin(email, password) },
            enabled = !busy && email.isNotBlank() && password.isNotEmpty(),
            modifier = Modifier.fillMaxWidth(),
        ) {
            if (busy) CircularProgressIndicator(Modifier.padding(2.dp)) else Text(if (isRegister) "Đăng ký" else "Đăng nhập")
        }

        TextButton(onClick = { isRegister = !isRegister }, modifier = Modifier.fillMaxWidth()) {
            Text(if (isRegister) "Đã có tài khoản? Đăng nhập" else "Chưa có tài khoản? Đăng ký")
        }

        Text("Máy chủ: ${BuildConfig.API_BASE_URL}", style = MaterialTheme.typography.bodySmall)
    }
    }
}
