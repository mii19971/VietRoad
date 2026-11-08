package com.vietroad.app.ui

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.ColumnScope
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxHeight
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.material3.Button
import androidx.compose.material3.ButtonDefaults
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
import androidx.compose.material3.Text
import androidx.compose.material3.TextButton
import androidx.compose.runtime.Composable
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import androidx.core.content.ContextCompat
import com.vietroad.app.data.AlertTypes
import com.vietroad.app.drive.AheadAlert

/** Tu be ngang nay tro len (tablet, dien thoai xoay ngang): chia 2 cot. */
private val WIDE_LAYOUT_MIN_WIDTH = 600.dp

/** Man hinh lai xe: toc do, canh bao phia truoc, nut bao cao. */
@Composable
fun DriveScreen(
    state: UiState,
    onStartTracking: () -> Unit,
    onReport: (type: String) -> Unit,
    onVote: (alertId: Int, confirm: Boolean) -> Unit,
    onLogout: () -> Unit,
) {
    // ----- Xin quyen vi tri (Android 6+ phai hoi luc chay) -----
    val context = LocalContext.current
    var hasPermission by remember { mutableStateOf(hasLocationPermission(context)) }
    val permissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions(),
    ) { results -> hasPermission = results.values.any { it } }

    // LaunchedEffect: chay khi hasPermission thay doi (va lan dau hien man hinh).
    LaunchedEffect(hasPermission) {
        if (hasPermission) onStartTracking()
        else permissionLauncher.launch(arrayOf(Manifest.permission.ACCESS_FINE_LOCATION, Manifest.permission.ACCESS_COARSE_LOCATION))
    }

    // BoxWithConstraints: cho biet kich thuoc vung ve -> chon bo cuc theo do rong man hinh.
    // dp = don vi doc lap mat do diem anh: 600dp ~ be ngang cua tablet 7 inch.
    BoxWithConstraints(Modifier.fillMaxSize().padding(16.dp)) {
        val wide = maxWidth >= WIDE_LAYOUT_MIN_WIDTH

        if (wide) {
            // ----- Man hinh rong: 2 cot -----
            Row(Modifier.fillMaxSize(), horizontalArrangement = Arrangement.spacedBy(24.dp)) {
                // Cot trai (rong hon): thu quan trong nhat khi dang lai.
                Column(Modifier.weight(1.2f).fillMaxHeight(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Header(state, onLogout)
                    PermissionWarning(hasPermission) { hasPermission = hasLocationPermission(context) }
                    SpeedBlock(state, speedSize = 88)
                    NearestBlock(state, onVote)
                }
                // Cot phai: danh sach tiep theo + thong bao + nut bao cao.
                Column(Modifier.weight(1f).fillMaxHeight(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Text("Tiếp theo", style = MaterialTheme.typography.titleMedium)
                    UpcomingList(state.ahead.drop(1))
                    MessageText(state.message)
                    // Man hinh rong: nut xep doc, to -> de bam khi dang lai.
                    ReportButton("Báo camera tốc độ", Modifier.fillMaxWidth()) { onReport(AlertTypes.SPEED_CAMERA) }
                    ReportButton("Báo camera đèn đỏ", Modifier.fillMaxWidth()) { onReport(AlertTypes.RED_LIGHT_CAMERA) }
                    ReportButton("Báo chốt CSGT", Modifier.fillMaxWidth()) { onReport(AlertTypes.POLICE_CHECKPOINT) }
                }
            }
        } else {
            // ----- Dien thoai doc: 1 cot -----
            Column(Modifier.fillMaxSize(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Header(state, onLogout)
                PermissionWarning(hasPermission) { hasPermission = hasLocationPermission(context) }
                SpeedBlock(state, speedSize = 56)
                NearestBlock(state, onVote)
                UpcomingList(state.ahead.drop(1))
                MessageText(state.message)
                Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                    ReportButton("Báo camera", Modifier.weight(1f)) { onReport(AlertTypes.SPEED_CAMERA) }
                    ReportButton("Báo đèn đỏ", Modifier.weight(1f)) { onReport(AlertTypes.RED_LIGHT_CAMERA) }
                    ReportButton("Báo CSGT", Modifier.weight(1f)) { onReport(AlertTypes.POLICE_CHECKPOINT) }
                }
            }
        }
    }
}

// ---------- Cac khoi giao dien nho, dung chung cho ca 2 bo cuc ----------

@Composable
private fun Header(state: UiState, onLogout: () -> Unit) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Text(state.userName ?: "", style = MaterialTheme.typography.titleMedium)
        Spacer(Modifier.weight(1f))
        if (!state.online) Text("Mất mạng", color = MaterialTheme.colorScheme.error)
        TextButton(onClick = onLogout) { Text("Đăng xuất") }
    }
}

@Composable
private fun PermissionWarning(hasPermission: Boolean, onRetry: () -> Unit) {
    if (hasPermission) return
    Text("Cần quyền vị trí để cảnh báo.", color = MaterialTheme.colorScheme.error)
    Button(onClick = onRetry) { Text("Thử lại") }
}

@Composable
private fun SpeedBlock(state: UiState, speedSize: Int) {
    val position = state.position
    Column {
        Text(
            text = position?.speedKmh?.let { "$it km/h" } ?: "-- km/h",
            fontSize = speedSize.sp, fontWeight = FontWeight.Bold,
        )
        Text(
            text = when {
                position == null -> "Đang chờ vị trí…"
                else -> "${locationSource(position)} · ${state.nearbyCount} cảnh báo trong 3 km"
            },
            style = MaterialTheme.typography.bodySmall,
            color = if (position?.fromGps == false) MaterialTheme.colorScheme.tertiary else MaterialTheme.colorScheme.onSurface,
        )
    }
}

@Composable
private fun NearestBlock(state: UiState, onVote: (Int, Boolean) -> Unit) {
    val nearest = state.ahead.firstOrNull()
    when {
        nearest != null -> NearestAlertCard(nearest, onVote)
        state.position != null -> Text("Không có cảnh báo phía trước.", style = MaterialTheme.typography.titleMedium)
    }
}

/** Danh sach canh bao tiep theo; chiem het phan con trong cua cot. */
@Composable
private fun ColumnScope.UpcomingList(items: List<AheadAlert>) {
    LazyColumn(Modifier.weight(1f).fillMaxWidth()) {
        items(items, key = { it.alert.id }) { item ->
            Text("${AlertTypes.label(item.alert.type)} · ${formatDistance(item.distanceM)}", Modifier.padding(vertical = 6.dp))
            HorizontalDivider()
        }
    }
}

@Composable
private fun MessageText(message: String?) {
    message?.let { Text(it, color = MaterialTheme.colorScheme.primary) }
}

@Composable
private fun NearestAlertCard(item: AheadAlert, onVote: (Int, Boolean) -> Unit) {
    Card(
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
        modifier = Modifier.fillMaxWidth(),
    ) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Text(AlertTypes.label(item.alert.type), style = MaterialTheme.typography.headlineSmall)
            Text(formatDistance(item.distanceM), fontSize = 40.sp, fontWeight = FontWeight.Bold)
            item.alert.speedLimit?.let { Text("Giới hạn $it km/h", style = MaterialTheme.typography.titleMedium) }
            Text(
                "${item.alert.confirmCount} xác nhận · ${item.alert.denyCount} phủ nhận · nguồn ${item.alert.source}",
                style = MaterialTheme.typography.bodySmall,
            )
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                OutlinedButton(onClick = { onVote(item.alert.id, true) }) { Text("Vẫn còn") }
                OutlinedButton(onClick = { onVote(item.alert.id, false) }) { Text("Không còn") }
            }
        }
    }
}

@Composable
private fun ReportButton(text: String, modifier: Modifier, onClick: () -> Unit) {
    Button(
        onClick = onClick,
        modifier = modifier.height(56.dp), // nut cao, de bam khi dang lai
        colors = ButtonDefaults.buttonColors(containerColor = MaterialTheme.colorScheme.tertiary),
    ) { Text(text) }
}

private fun locationSource(p: Position): String {
    val accuracy = p.accuracyM?.let { " ±$it m" } ?: ""
    return if (p.fromGps) "GPS$accuracy" else "Vị trí qua mạng$accuracy"
}

private fun formatDistance(meters: Int): String =
    if (meters < 1000) "$meters m" else "%.1f km".format(meters / 1000.0)

private fun hasLocationPermission(context: Context): Boolean =
    ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED ||
        ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_COARSE_LOCATION) == PackageManager.PERMISSION_GRANTED
