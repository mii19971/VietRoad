package com.vietroad.app.ui

import androidx.compose.foundation.clickable
import androidx.compose.foundation.layout.Arrangement
import androidx.compose.foundation.layout.Column
import androidx.compose.foundation.layout.Row
import androidx.compose.foundation.layout.Spacer
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.fillMaxWidth
import androidx.compose.foundation.layout.height
import androidx.compose.foundation.layout.padding
import androidx.compose.foundation.layout.width
import androidx.compose.foundation.layout.widthIn
import androidx.compose.ui.graphics.Color
import androidx.compose.foundation.lazy.LazyColumn
import androidx.compose.foundation.lazy.items
import androidx.compose.foundation.text.KeyboardActions
import androidx.compose.foundation.text.KeyboardOptions
import androidx.compose.material3.Button
import androidx.compose.material3.Card
import androidx.compose.material3.CardDefaults
import androidx.compose.material3.CircularProgressIndicator
import androidx.compose.material3.HorizontalDivider
import androidx.compose.material3.MaterialTheme
import androidx.compose.material3.OutlinedButton
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
import androidx.compose.ui.text.font.FontWeight
import androidx.compose.ui.text.input.ImeAction
import androidx.compose.ui.text.style.TextOverflow
import androidx.compose.ui.unit.dp
import androidx.compose.ui.unit.sp
import com.vietroad.app.data.Place
import com.vietroad.app.drive.NavFormat
import com.vietroad.app.drive.NavState
import com.vietroad.app.drive.Position
import java.text.SimpleDateFormat
import java.util.Date
import java.util.Locale

/** Man hinh tim diem den: o nhap + danh sach ket qua; bam 1 ket qua = bat dau chi duong. */
@Composable
fun SearchScreen(
    search: SearchUi,
    position: Position?,
    message: String?,
    onSearch: (String) -> Unit,
    onPick: (Place) -> Unit,
    onClose: () -> Unit,
) {
    var query by rememberSaveable { mutableStateOf("") }

    Column(
        Modifier.fillMaxSize().padding(16.dp).widthIn(max = 720.dp),
        verticalArrangement = Arrangement.spacedBy(12.dp),
    ) {
        Row(verticalAlignment = Alignment.CenterVertically) {
            TextButton(onClick = onClose) { Text("← Quay lại") }
            Text("Chỉ đường", style = MaterialTheme.typography.titleLarge)
        }

        // Ghi chu "đi từ đâu đến đâu".
        Card(Modifier.fillMaxWidth()) {
            Column(Modifier.padding(12.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
                RoutePoint(
                    dot = "●", dotColor = MaterialTheme.colorScheme.primary, label = "Từ",
                    text = when {
                        position == null -> "Chưa có vị trí GPS"
                        search.originName != null -> "Vị trí hiện tại · ${search.originName}"
                        search.originLoading -> "Vị trí hiện tại · đang xác định địa chỉ…"
                        // Khong tra duoc dia chi (mat mang, API cu...): hien toa do de van biet la cho nao.
                        else -> "Vị trí hiện tại · ${"%.5f, %.5f".format(Locale.US, position.lat, position.lng)}"
                    },
                )
                RoutePoint(
                    dot = "●", dotColor = Color(0xFF2E7D32), label = "Đến",
                    text = search.picked?.let { "${it.name} (đang tìm đường…)" } ?: "Chọn điểm đến bên dưới",
                    dim = search.picked == null,
                )
            }
        }

        OutlinedTextField(
            value = query,
            onValueChange = { query = it },
            label = { Text("Đi đến đâu? (vd: Hồ Gươm, 1 Tràng Tiền)") },
            singleLine = true,
            // Nut "Tim" tren ban phim cung tim luon.
            keyboardOptions = KeyboardOptions(imeAction = ImeAction.Search),
            keyboardActions = KeyboardActions(onSearch = { onSearch(query) }),
            modifier = Modifier.fillMaxWidth(),
        )
        Button(
            onClick = { onSearch(query) },
            enabled = !search.busy && query.isNotBlank(),
            modifier = Modifier.fillMaxWidth().height(52.dp),
        ) { Text("Tìm") }

        if (search.busy) CircularProgressIndicator(Modifier.align(Alignment.CenterHorizontally))
        message?.let { Text(it, color = MaterialTheme.colorScheme.error) }
        if (search.searched && !search.busy && search.results.isEmpty()) {
            Text("Không tìm thấy địa điểm nào. Thử gõ cụ thể hơn (thêm quận, thành phố).")
        }

        LazyColumn(Modifier.fillMaxWidth()) {
            items(search.results) { place ->
                Column(
                    Modifier.fillMaxWidth()
                        .clickable(enabled = !search.busy) { onPick(place) }
                        .padding(vertical = 10.dp),
                ) {
                    Text(place.name, style = MaterialTheme.typography.titleMedium)
                    Text(
                        place.address,
                        style = MaterialTheme.typography.bodySmall,
                        maxLines = 2, overflow = TextOverflow.Ellipsis,
                    )
                }
                HorizontalDivider()
            }
        }
        Text("Dữ liệu © OpenStreetMap contributors", style = MaterialTheme.typography.labelSmall)
    }
}

/** 1 dong "● Từ: ..." / "● Đến: ...". Cham mau giong ban do: xanh = xe, xanh la = diem den. */
@Composable
private fun RoutePoint(dot: String, dotColor: Color, label: String, text: String, dim: Boolean = false) {
    Row(verticalAlignment = Alignment.CenterVertically) {
        Text(dot, color = dotColor)
        Spacer(Modifier.width(8.dp))
        Text("$label: ", style = MaterialTheme.typography.labelLarge)
        Text(
            text,
            style = MaterialTheme.typography.bodyMedium,
            color = if (dim) MaterialTheme.colorScheme.onSurfaceVariant else MaterialTheme.colorScheme.onSurface,
            maxLines = 1, overflow = TextOverflow.Ellipsis,
        )
    }
}

/** The chi dan khi dang chi duong: mui ten + khoang cach toi cho re + thoi gian con lai. */
@Composable
fun NavCard(nav: NavState, onStop: () -> Unit) {
    Card(
        colors = CardDefaults.cardColors(containerColor = MaterialTheme.colorScheme.primaryContainer),
        modifier = Modifier.fillMaxWidth(),
    ) {
        Column(Modifier.padding(16.dp), verticalArrangement = Arrangement.spacedBy(6.dp)) {
            if (nav.rerouting) {
                Text("Đang tính lại đường đi…", style = MaterialTheme.typography.headlineSmall)
            } else {
                Row(verticalAlignment = Alignment.CenterVertically) {
                    Text(arrowFor(nav.type, nav.modifier), fontSize = 48.sp, fontWeight = FontWeight.Bold)
                    Spacer(Modifier.padding(horizontal = 8.dp))
                    Column {
                        nav.distanceToStepM?.let {
                            Text(NavFormat.distance(it), fontSize = 32.sp, fontWeight = FontWeight.Bold)
                        }
                        Text(nav.instruction ?: "Đang xác định vị trí trên tuyến…", style = MaterialTheme.typography.titleMedium)
                    }
                }
            }

            Row(verticalAlignment = Alignment.CenterVertically) {
                Column(Modifier.weight(1f)) {
                    nav.originName?.let {
                        Text("Từ $it", style = MaterialTheme.typography.bodySmall, maxLines = 1, overflow = TextOverflow.Ellipsis)
                    }
                    Text("Đến ${nav.destinationName}", maxLines = 1, overflow = TextOverflow.Ellipsis)
                    Text(
                        "Còn ${NavFormat.distance(nav.remainingM)} · ${NavFormat.duration(nav.remainingS)} · đến lúc ${arrivalClock(nav.remainingS)}",
                        style = MaterialTheme.typography.bodySmall,
                    )
                }
                OutlinedButton(onClick = onStop) { Text("Kết thúc") }
            }
        }
    }
}

/** Mui ten theo kieu re (ky tu Unicode, khong can file hinh). */
private fun arrowFor(type: String?, modifier: String?): String = when {
    type == "arrive" -> "◉"
    type == "roundabout" || type == "rotary" -> "⟳"
    else -> when (modifier) {
        "left" -> "←"
        "right" -> "→"
        "slight left" -> "↖"
        "slight right" -> "↗"
        "sharp left" -> "↙"
        "sharp right" -> "↘"
        "uturn" -> "↶"
        else -> "↑"
    }
}

/** Gio du kien toi noi, vd "17:45". */
private fun arrivalClock(remainingS: Int): String =
    SimpleDateFormat("HH:mm", Locale.getDefault()).format(Date(System.currentTimeMillis() + remainingS * 1000L))
