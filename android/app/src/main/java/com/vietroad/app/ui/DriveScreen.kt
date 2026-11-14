package com.vietroad.app.ui

import android.Manifest
import android.content.Context
import android.content.pm.PackageManager
import android.os.Build
import androidx.activity.compose.rememberLauncherForActivityResult
import androidx.activity.result.contract.ActivityResultContracts
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.BoxWithConstraints
import androidx.compose.foundation.shape.RoundedCornerShape
import androidx.compose.foundation.shape.CircleShape
import androidx.compose.foundation.background
import androidx.compose.foundation.border
import androidx.compose.foundation.layout.size
import androidx.compose.foundation.layout.width
import androidx.compose.ui.unit.Dp
import androidx.compose.material3.Surface
import androidx.compose.ui.draw.clip
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
import com.vietroad.app.data.Alert
import com.vietroad.app.data.AlertTypes
import com.vietroad.app.data.SubscriptionInfo
import androidx.compose.ui.graphics.Color
import com.vietroad.app.drive.AheadAlert
import com.vietroad.app.drive.Position

/** Tu be ngang nay tro len (tablet, dien thoai xoay ngang): chia 2 cot. */
private val WIDE_LAYOUT_MIN_WIDTH = 600.dp

/** Man hinh lai xe: toc do, canh bao phia truoc, nut bao cao. */
@Composable
fun DriveScreen(
    state: UiState,
    onPermissionReady: () -> Unit,
    onStartTracking: () -> Unit,
    onStopTracking: () -> Unit,
    onOpenSearch: () -> Unit,
    onStopNavigation: () -> Unit,
    onReport: (type: String) -> Unit,
    onVote: (alertId: Int, confirm: Boolean) -> Unit,
    onDismissReview: () -> Unit,
    onLogout: () -> Unit,
) {
    // ----- Xin quyen vi tri (Android 6+ phai hoi luc chay) -----
    val context = LocalContext.current
    var hasPermission by remember { mutableStateOf(hasLocationPermission(context)) }
    val permissionLauncher = rememberLauncherForActivityResult(
        ActivityResultContracts.RequestMultiplePermissions(),
    ) { hasPermission = hasLocationPermission(context) } // tu choi quyen thong bao van chay duoc

    // LaunchedEffect: chay khi hasPermission thay doi (va lan dau hien man hinh).
    LaunchedEffect(hasPermission) {
        if (hasPermission) onPermissionReady() else permissionLauncher.launch(requiredPermissions())
    }

    // BoxWithConstraints: cho biet kich thuoc vung ve -> chon bo cuc theo do rong man hinh.
    // dp = don vi doc lap mat do diem anh: 600dp ~ be ngang cua tablet 7 inch.
    BoxWithConstraints(Modifier.fillMaxSize().padding(16.dp)) {
        val wide = maxWidth >= WIDE_LAYOUT_MIN_WIDTH

        if (wide) {
            // ----- Man hinh rong (tablet, man hinh o to): ben trai thong tin, ben phai ban do lon -----
            Row(Modifier.fillMaxSize(), horizontalArrangement = Arrangement.spacedBy(16.dp)) {
                Column(Modifier.weight(1f).fillMaxHeight(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                    Header(state, onLogout)
                    TrackingToggle(state, hasPermission, onStartTracking, onStopTracking)
                    PermissionWarning(hasPermission) { hasPermission = hasLocationPermission(context) }
                    NavigationBlock(state, hasPermission, onOpenSearch, onStopNavigation)
                    ReviewPrompt(state.reviewAlert, onVote, onDismissReview)
                    SpeedBlock(state, speedSize = 64)
                    NearestBlock(state, onVote)
                    UpcomingList(state.ahead.drop(1))
                    MessageText(state.message)
                    Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                        ReportButton("Báo camera", Modifier.weight(1f)) { onReport(AlertTypes.SPEED_CAMERA) }
                        ReportButton("Báo đèn đỏ", Modifier.weight(1f)) { onReport(AlertTypes.RED_LIGHT_CAMERA) }
                        ReportButton("Báo CSGT", Modifier.weight(1f)) { onReport(AlertTypes.POLICE_CHECKPOINT) }
                    }
                }
                DriveMap(state, Modifier.weight(1.4f).fillMaxHeight().clip(RoundedCornerShape(16.dp)))
            }
        } else {
            // ----- Dien thoai doc: ban do chiem phan giua, toc do noi tren ban do -----
            Column(Modifier.fillMaxSize(), verticalArrangement = Arrangement.spacedBy(12.dp)) {
                Header(state, onLogout)
                TrackingToggle(state, hasPermission, onStartTracking, onStopTracking)
                PermissionWarning(hasPermission) { hasPermission = hasLocationPermission(context) }
                NavigationBlock(state, hasPermission, onOpenSearch, onStopNavigation)
                ReviewPrompt(state.reviewAlert, onVote, onDismissReview)
                Box(Modifier.weight(1f).fillMaxWidth().clip(RoundedCornerShape(16.dp))) {
                    DriveMap(state, Modifier.fillMaxSize())
                    SpeedBadge(state, Modifier.align(Alignment.BottomStart).padding(12.dp))
                }
                NearestBlock(state, onVote)
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
        state.subscription?.let { PlanBadge(it) }
        Spacer(Modifier.weight(1f))
        if (!state.online) Text("Mất mạng", color = MaterialTheme.colorScheme.error)
        TextButton(onClick = onLogout) { Text("Đăng xuất") }
    }
}

/**
 * Dang chay: nhac rang app van canh bao khi tat man hinh + nut Dung.
 * Da dung: nut Bat dau (to, de bam).
 */
@Composable
private fun TrackingToggle(state: UiState, hasPermission: Boolean, onStart: () -> Unit, onStop: () -> Unit) {
    if (!hasPermission) return
    if (state.tracking) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            Text(
                "Đang cảnh báo — vẫn chạy khi tắt màn hình",
                style = MaterialTheme.typography.bodySmall,
                modifier = Modifier.weight(1f),
            )
            OutlinedButton(onClick = onStop) { Text("Dừng") }
        }
    } else {
        Button(onClick = onStart, modifier = Modifier.fillMaxWidth().height(56.dp)) {
            Text("Bắt đầu cảnh báo", fontSize = 18.sp)
        }
    }
}

/** Nhan goi canh ten: "Premium · đến 09/12/2026" hoac "Miễn phí". */
@Composable
private fun PlanBadge(sub: SubscriptionInfo) {
    val text = if (sub.isPremium) "Premium" + (sub.expiresAt?.let { " · đến ${shortDate(it)}" } ?: "") else sub.planLabel
    Surface(
        modifier = Modifier.padding(start = 8.dp),
        shape = RoundedCornerShape(8.dp),
        color = if (sub.isPremium) Color(0xFFFFC107) else MaterialTheme.colorScheme.surfaceVariant,
        contentColor = if (sub.isPremium) Color.Black else MaterialTheme.colorScheme.onSurfaceVariant,
    ) {
        Text(text, style = MaterialTheme.typography.labelMedium, modifier = Modifier.padding(horizontal = 8.dp, vertical = 2.dp))
    }
}

/** "2026-12-09T03:00:00+00:00" -> "09/12/2026" (chi lay phan ngay; minSdk 21 chua co java.time). */
private fun shortDate(iso: String): String {
    val d = iso.take(10).split("-")
    return if (d.size == 3) "${d[2]}/${d[1]}/${d[0]}" else iso
}

/** Dang chi duong: the chi dan. Chua: nut "Chi duong". */
@Composable
private fun NavigationBlock(state: UiState, hasPermission: Boolean, onOpenSearch: () -> Unit, onStop: () -> Unit) {
    if (!hasPermission) return
    val nav = state.nav
    if (nav != null) {
        NavCard(nav, onStop)
    } else {
        OutlinedButton(onClick = onOpenSearch, modifier = Modifier.fillMaxWidth().height(52.dp)) {
            // Goi Mien phi: van bam duoc (app hoi lai server, co the vua duoc nang cap), kem nhan Premium.
            Text(if (state.canNavigate) "Chỉ đường" else "Chỉ đường · 🔒 Premium", fontSize = 18.sp)
        }
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
        Row(verticalAlignment = Alignment.CenterVertically, horizontalArrangement = Arrangement.spacedBy(16.dp)) {
            Text(
                text = position?.speedKmh?.let { "$it km/h" } ?: "-- km/h",
                fontSize = speedSize.sp, fontWeight = FontWeight.Bold,
                color = speedColor(state),
            )
            state.speedLimit?.let { SpeedLimitSign(it, size = (speedSize * 1.1f).dp) }
        }
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

/** O toc do nho noi tren ban do (dien thoai). */
@Composable
private fun SpeedBadge(state: UiState, modifier: Modifier) {
    Surface(
        modifier = modifier,
        shape = RoundedCornerShape(12.dp),
        color = MaterialTheme.colorScheme.surface.copy(alpha = 0.9f),
        shadowElevation = 4.dp,
    ) {
        Row(Modifier.padding(horizontal = 12.dp, vertical = 6.dp), verticalAlignment = Alignment.CenterVertically) {
            Column {
                Text(state.position?.speedKmh?.let { "$it km/h" } ?: "-- km/h", fontSize = 28.sp,
                    fontWeight = FontWeight.Bold, color = speedColor(state))
                Text(
                    state.position?.let(::locationSource) ?: "Đang chờ vị trí…",
                    style = MaterialTheme.typography.labelSmall,
                )
            }
            state.speedLimit?.let { Spacer(Modifier.width(10.dp)); SpeedLimitSign(it, size = 48.dp) }
        }
    }
}

/** Toc do mau do khi qua gioi han. */
@Composable
private fun speedColor(state: UiState) =
    if (state.overspeed) Color(0xFFE53935) else MaterialTheme.colorScheme.onSurface

/** Bien bao gioi han toc do (tron, nen trang, vien do, so den) - giong bien P.127 ngoai duong. */
@Composable
private fun SpeedLimitSign(limit: Int, size: Dp) {
    Box(
        modifier = Modifier
            .size(size)
            .background(Color.White, CircleShape)
            .border(width = size * 0.11f, color = Color(0xFFD32F2F), shape = CircleShape),
        contentAlignment = Alignment.Center,
    ) {
        Text(
            "$limit",
            color = Color.Black,
            fontWeight = FontWeight.Bold,
            fontSize = (size.value * if (limit >= 100) 0.34f else 0.42f).sp,
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
            Text("${AlertTypes.title(item.alert)} · ${formatDistance(item.distanceM)}", Modifier.padding(vertical = 6.dp))
            HorizontalDivider()
        }
    }
}

@Composable
private fun MessageText(message: String?) {
    message?.let { Text(it, color = MaterialTheme.colorScheme.primary) }
}

/**
 * Hoi nhanh "con / khong con" sau khi vua di qua 1 diem (camera, chot CSGT). Giup du lieu luon moi:
 * diem da go bi bao "khong con" roi tu mo di; diem con that duoc xac nhan nen khong bi giam diem.
 */
@Composable
private fun ReviewPrompt(alert: Alert?, onVote: (Int, Boolean) -> Unit, onDismiss: () -> Unit) {
    if (alert == null) return
    Card(
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.secondaryContainer),
        modifier = Modifier.fillMaxWidth(),
    ) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(8.dp)) {
            Text("Bạn vừa qua ${AlertTypes.label(alert.type)}. Còn không?",
                style = MaterialTheme.typography.titleMedium)
            Row(horizontalArrangement = Arrangement.spacedBy(8.dp)) {
                Button(onClick = { onVote(alert.id, true) }, modifier = Modifier.weight(1f)) { Text("Còn") }
                Button(onClick = { onVote(alert.id, false) }, modifier = Modifier.weight(1f)) { Text("Không còn") }
                TextButton(onClick = onDismiss) { Text("Bỏ qua") }
            }
        }
    }
}

@Composable
private fun NearestAlertCard(item: AheadAlert, onVote: (Int, Boolean) -> Unit) {
    Card(
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.errorContainer),
        modifier = Modifier.fillMaxWidth(),
    ) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            Text(AlertTypes.title(item.alert), style = MaterialTheme.typography.headlineSmall)
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

/** Vi tri (bat buoc) + thong bao (Android 13+, de hien thong bao "dang canh bao" cua dich vu nen). */
private fun requiredPermissions(): Array<String> = buildList {
    add(Manifest.permission.ACCESS_FINE_LOCATION)
    add(Manifest.permission.ACCESS_COARSE_LOCATION)
    if (Build.VERSION.SDK_INT >= Build.VERSION_CODES.TIRAMISU) add(Manifest.permission.POST_NOTIFICATIONS)
}.toTypedArray()

private fun hasLocationPermission(context: Context): Boolean =
    ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_FINE_LOCATION) == PackageManager.PERMISSION_GRANTED ||
        ContextCompat.checkSelfPermission(context, Manifest.permission.ACCESS_COARSE_LOCATION) == PackageManager.PERMISSION_GRANTED
