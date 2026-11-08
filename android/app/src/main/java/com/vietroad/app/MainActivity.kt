package com.vietroad.app

import android.os.Bundle
import android.view.WindowManager
import androidx.activity.ComponentActivity
import androidx.activity.compose.setContent
import androidx.activity.viewModels
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.Surface
import androidx.compose.material3.darkColorScheme
import androidx.compose.runtime.getValue
import androidx.lifecycle.compose.collectAsStateWithLifecycle
import com.vietroad.app.ui.DriveScreen
import com.vietroad.app.ui.LoginScreen
import com.vietroad.app.ui.MainViewModel

/** Diem vao cua app. Chi lam 1 viec: hien giao dien Compose theo trang thai cua ViewModel. */
class MainActivity : ComponentActivity() {

    // by viewModels(): lay ViewModel gan voi Activity; xoay man hinh van giu nguyen object cu.
    private val viewModel: MainViewModel by viewModels()

    override fun onCreate(savedInstanceState: Bundle?) {
        super.onCreate(savedInstanceState)

        // App lai xe: giu man hinh luon sang.
        window.addFlags(WindowManager.LayoutParams.FLAG_KEEP_SCREEN_ON)

        setContent {
            // Nen toi: do choi mat khi lai xe ban dem.
            MaterialTheme(colorScheme = darkColorScheme()) {
                Surface {
                    // collectAsStateWithLifecycle: theo doi state, tu dung khi app chay nen.
                    val state by viewModel.state.collectAsStateWithLifecycle()

                    if (!state.loggedIn) {
                        LoginScreen(
                            busy = state.busy,
                            message = state.message,
                            onLogin = viewModel::login,
                            onRegister = viewModel::register,
                        )
                    } else {
                        DriveScreen(
                            state = state,
                            onStartTracking = viewModel::startTracking,
                            onReport = viewModel::report,
                            onVote = viewModel::vote,
                            onLogout = viewModel::logout,
                        )
                    }
                }
            }
        }
    }
}
