package com.vietroad.app.ui

import android.content.Context
import android.graphics.Bitmap
import android.graphics.Canvas
import android.graphics.Color
import android.graphics.Paint
import android.graphics.Path
import android.os.Build
import androidx.compose.foundation.layout.Box
import androidx.compose.foundation.layout.fillMaxSize
import androidx.compose.foundation.layout.padding
import androidx.compose.material3.Button
import androidx.compose.material3.Text
import androidx.compose.runtime.Composable
import androidx.compose.runtime.DisposableEffect
import androidx.compose.runtime.LaunchedEffect
import androidx.compose.runtime.getValue
import androidx.compose.runtime.mutableStateOf
import androidx.compose.runtime.remember
import androidx.compose.runtime.setValue
import androidx.compose.ui.Alignment
import androidx.compose.ui.Modifier
import androidx.compose.ui.platform.LocalContext
import androidx.compose.ui.platform.LocalLifecycleOwner
import androidx.compose.ui.unit.dp
import androidx.compose.ui.viewinterop.AndroidView
import androidx.lifecycle.Lifecycle
import androidx.lifecycle.LifecycleEventObserver
import com.vietroad.app.BuildConfig
import com.vietroad.app.data.Alert
import com.vietroad.app.data.AlertTypes
import com.vietroad.app.drive.Position
import kotlinx.coroutines.delay
import org.maplibre.android.camera.CameraPosition
import org.maplibre.android.camera.CameraUpdateFactory
import org.maplibre.android.geometry.LatLng
import org.maplibre.android.geometry.LatLngBounds
import org.maplibre.android.maps.MapLibreMap
import org.maplibre.android.maps.MapLibreMapOptions
import org.maplibre.android.maps.MapView
import org.maplibre.android.maps.Style
import org.maplibre.android.style.expressions.Expression
import org.maplibre.android.style.layers.CircleLayer
import org.maplibre.android.style.layers.LineLayer
import org.maplibre.android.style.layers.Property
import org.maplibre.android.style.layers.PropertyFactory
import org.maplibre.android.style.layers.SymbolLayer
import org.maplibre.android.style.sources.GeoJsonSource
import org.maplibre.geojson.Feature
import org.maplibre.geojson.FeatureCollection
import org.maplibre.geojson.LineString
import org.maplibre.geojson.Point

/**
 * Ban do lai xe: tuyen duong (xanh), canh bao (cham do / cam / xanh), diem den (xanh la), xe (mui ten).
 *
 * - Mac dinh camera BAM THEO XE. Dang chi duong: phong to, nghieng 45 do, xoay theo huong xe chay
 *   (giong Google Maps). Khong chi duong: huong Bac len tren.
 * - Nguoi dung keo / phong to ban do -> ngung bam theo, hien nut "Về vị trí".
 * - Vua bat dau chi duong: xem toan tuyen 4 giay roi moi bam theo xe.
 */
@Composable
fun DriveMap(state: UiState, modifier: Modifier = Modifier) {
    val context = LocalContext.current
    val controller = remember { MapController(context) }
    var follow by remember { mutableStateOf(true) }
    controller.onUserMovedMap = { follow = false }

    // MapView can biet vong doi man hinh (an / hien / huy) de dung ve, giai phong bo nho.
    val lifecycleOwner = LocalLifecycleOwner.current
    DisposableEffect(lifecycleOwner) {
        val observer = LifecycleEventObserver { _, event ->
            when (event) {
                Lifecycle.Event.ON_START -> controller.start()
                Lifecycle.Event.ON_RESUME -> controller.resume()
                Lifecycle.Event.ON_PAUSE -> controller.pause()
                Lifecycle.Event.ON_STOP -> controller.stop()
                else -> {}
            }
        }
        lifecycleOwner.lifecycle.addObserver(observer)
        onDispose {
            lifecycleOwner.lifecycle.removeObserver(observer)
            controller.destroy()
        }
    }

    val nav = state.nav
    val navigating = nav != null
    val route = nav?.routePoints ?: emptyList()

    // Tuyen moi (bat dau chi duong / tinh lai duong) -> ve lai.
    var hadRoute by remember { mutableStateOf(false) }
    LaunchedEffect(route) {
        controller.setRoute(route, nav?.let { it.destLat to it.destLng })
        if (route.size >= 2 && !hadRoute) {
            // Vua bat dau chi duong: cho xem toan tuyen roi quay ve bam theo xe.
            follow = false
            controller.showOverview(route)
            delay(4000)
            follow = true
        }
        hadRoute = route.size >= 2
    }
    LaunchedEffect(state.nearby) { controller.setAlerts(state.nearby) }
    LaunchedEffect(state.position, follow, navigating) {
        val p = state.position ?: return@LaunchedEffect
        controller.setCar(p)
        if (follow) controller.moveCamera(p, navigating)
    }

    Box(modifier) {
        // AndroidView: nhung View cu (MapView) vao giao dien Compose.
        AndroidView(factory = { controller.mapView }, modifier = Modifier.fillMaxSize())
        if (!follow && state.position != null) {
            Button(
                onClick = { follow = true },
                modifier = Modifier.align(Alignment.BottomEnd).padding(12.dp),
            ) { Text("Về vị trí") }
        }
    }
}

/**
 * Lam viec voi MapLibre (View cu, khong phai Compose).
 * Style (kieu ban do) tai qua mang, mat vai giay: du lieu den truoc thi giu lai, tai xong moi ve.
 */
class MapController(context: Context) {
    // May ao: bo gia lap OpenGL (libGLESv2_enc) hay crash voi SurfaceView (SIGSEGV trong glDrawElements)
    // -> dung TextureView. May that giu SurfaceView (nhanh, tiet kiem pin hon).
    val mapView = MapView(context, MapLibreMapOptions.createFromAttributes(context).textureMode(isEmulator()))
        .also { it.onCreate(null) }
    var onUserMovedMap: (() -> Unit)? = null

    private var map: MapLibreMap? = null
    private var style: Style? = null

    // Du lieu moi nhat (de ve lai khi style tai xong).
    private var route: List<List<Double>> = emptyList()
    private var destination: Pair<Double, Double>? = null
    private var alerts: List<Alert> = emptyList()
    private var car: Position? = null

    private var started = false
    private var resumed = false
    private var destroyed = false

    init {
        mapView.getMapAsync { m ->
            map = m
            // Mo dau: da co vi tri xe thi nhay toi do luon; chua co thi xem ca nuoc Viet Nam.
            val pending = pendingCamera
            if (pending != null) moveCamera(pending.first, pending.second, animate = false)
            else m.cameraPosition = CameraPosition.Builder().target(LatLng(16.0, 106.0)).zoom(4.8).build()
            pendingCamera = null
            // Vua bat dau chi duong khi ban do chua san sang -> xem toan tuyen bay gio.
            pendingOverview?.let { showOverview(it) }
            pendingOverview = null
            // Nguoi dung tu keo / chum ngon tay (khong phai do code dieu khien camera).
            m.addOnCameraMoveStartedListener { reason ->
                if (reason == MapLibreMap.OnCameraMoveStartedListener.REASON_API_GESTURE) onUserMovedMap?.invoke()
            }
            m.setStyle(Style.Builder().fromUri(BuildConfig.MAP_STYLE_URL)) { s ->
                style = s
                useVietnameseNames(s)
                addLayers(s)
                drawRoute(); drawAlerts(); drawCar()
            }
        }
    }

    // ---------- Cap nhat du lieu ----------

    fun setRoute(points: List<List<Double>>, dest: Pair<Double, Double>?) {
        route = points
        destination = dest
        drawRoute()
    }

    fun setAlerts(list: List<Alert>) {
        alerts = list
        drawAlerts()
    }

    fun setCar(p: Position) {
        car = p
        drawCar()
    }

    // Yeu cau camera den truoc khi ban do san sang -> giu lai, ban do san sang thi thuc hien.
    private var pendingCamera: Pair<Position, Boolean>? = null
    private var pendingOverview: List<List<Double>>? = null

    /** Camera bam theo xe. Chi duong: gan hon, nghieng, xoay theo huong chay. */
    fun moveCamera(p: Position, navigating: Boolean, animate: Boolean = true) {
        val m = map ?: run { pendingCamera = p to navigating; return }
        val camera = CameraPosition.Builder()
            .target(LatLng(p.lat, p.lng))
            .zoom(if (navigating) 16.5 else 15.0)
            .tilt(if (navigating) 45.0 else 0.0)
            // Dung yen (khong co huong) -> giu huong cu, khong xoay lung tung.
            .bearing(if (navigating) p.heading ?: m.cameraPosition.bearing else 0.0)
            .build()
        // 900 ms ~ khoang cach 2 lan GPS (1 giay) -> ban do troi muot.
        if (animate) m.animateCamera(CameraUpdateFactory.newCameraPosition(camera), 900)
        else m.cameraPosition = camera
    }

    /** Thu phong vua ca tuyen duong. */
    fun showOverview(points: List<List<Double>>) {
        if (points.size < 2) return
        val m = map ?: run { pendingOverview = points; return }
        val bounds = LatLngBounds.Builder().includes(points.map { LatLng(it[0], it[1]) }).build()
        m.animateCamera(CameraUpdateFactory.newLatLngBounds(bounds, 120), 1000)
    }

    // ---------- Ve ----------

    /**
     * Style mac dinh hien ten tieng Anh ("Hang Bac Street"). Doi cac lop chu dang dung ten
     * sang ten tieng Viet / ten goc trong OpenStreetMap ("Phố Hàng Bạc").
     * Bo qua lop hien so hieu duong (ref, vd "QL1A") vi khong dung "name".
     */
    private fun useVietnameseNames(s: Style) {
        // Uu tien: ten tieng Viet (nuoc ngoai: "Thái Lan") -> ten chu Latin (duong pho VN) -> ten goc.
        val localName = Expression.coalesce(
            Expression.get("name:vi"), Expression.get("name:latin"), Expression.get("name"),
        )
        for (layer in s.layers) {
            if (layer is SymbolLayer && layer.textField.toString().contains("name")) {
                layer.setProperties(PropertyFactory.textField(localName))
            }
        }
    }

    private fun addLayers(s: Style) {
        s.addSource(GeoJsonSource(SRC_ROUTE))
        s.addSource(GeoJsonSource(SRC_ALERTS))
        s.addSource(GeoJsonSource(SRC_DEST))
        s.addSource(GeoJsonSource(SRC_CAR))
        s.addImage(IMG_ARROW, arrowBitmap())

        // Tuyen: vien trang ben duoi + duong xanh ben tren -> noi bat tren nen ban do.
        s.addLayer(LineLayer("route-casing", SRC_ROUTE).withProperties(
            PropertyFactory.lineColor("#FFFFFF"), PropertyFactory.lineWidth(10f),
            PropertyFactory.lineCap(Property.LINE_CAP_ROUND), PropertyFactory.lineJoin(Property.LINE_JOIN_ROUND),
        ))
        s.addLayer(LineLayer("route-line", SRC_ROUTE).withProperties(
            PropertyFactory.lineColor("#1A73E8"), PropertyFactory.lineWidth(6f),
            PropertyFactory.lineCap(Property.LINE_CAP_ROUND), PropertyFactory.lineJoin(Property.LINE_JOIN_ROUND),
        ))

        // Canh bao: mau theo loai (Expression = "cong thuc" ban do tu tinh cho tung diem).
        s.addLayer(CircleLayer("alerts", SRC_ALERTS).withProperties(
            PropertyFactory.circleRadius(8f),
            PropertyFactory.circleColor(Expression.match(
                Expression.get("type"), Expression.color(Color.parseColor("#D32F2F")),
                Expression.stop(AlertTypes.RED_LIGHT_CAMERA, Expression.color(Color.parseColor("#F57C00"))),
                Expression.stop(AlertTypes.POLICE_CHECKPOINT, Expression.color(Color.parseColor("#1565C0"))),
            )),
            PropertyFactory.circleStrokeColor("#FFFFFF"), PropertyFactory.circleStrokeWidth(2f),
        ))

        s.addLayer(CircleLayer("destination", SRC_DEST).withProperties(
            PropertyFactory.circleRadius(10f), PropertyFactory.circleColor("#2E7D32"),
            PropertyFactory.circleStrokeColor("#FFFFFF"), PropertyFactory.circleStrokeWidth(3f),
        ))

        // Xe: co huong -> mui ten xoay theo huong; chua co huong (dung yen) -> cham tron.
        s.addLayer(CircleLayer("car-dot", SRC_CAR).withProperties(
            PropertyFactory.circleRadius(9f), PropertyFactory.circleColor("#1A73E8"),
            PropertyFactory.circleStrokeColor("#FFFFFF"), PropertyFactory.circleStrokeWidth(3f),
        ).withFilter(Expression.not(Expression.has("bearing"))))
        s.addLayer(SymbolLayer("car-arrow", SRC_CAR).withProperties(
            PropertyFactory.iconImage(IMG_ARROW),
            PropertyFactory.iconRotate(Expression.get("bearing")),
            PropertyFactory.iconRotationAlignment(Property.ICON_ROTATION_ALIGNMENT_MAP),
            PropertyFactory.iconAllowOverlap(true), PropertyFactory.iconIgnorePlacement(true),
        ).withFilter(Expression.has("bearing")))
    }

    private fun drawRoute() {
        val s = style ?: return
        val line = if (route.size >= 2)
            listOf(Feature.fromGeometry(LineString.fromLngLats(route.map { Point.fromLngLat(it[1], it[0]) })))
        else emptyList()
        s.getSourceAs<GeoJsonSource>(SRC_ROUTE)?.setGeoJson(FeatureCollection.fromFeatures(line))

        val dest = destination?.takeIf { route.isNotEmpty() }
            ?.let { listOf(Feature.fromGeometry(Point.fromLngLat(it.second, it.first))) } ?: emptyList()
        s.getSourceAs<GeoJsonSource>(SRC_DEST)?.setGeoJson(FeatureCollection.fromFeatures(dest))
    }

    private fun drawAlerts() {
        val s = style ?: return
        val features = alerts.map { a ->
            Feature.fromGeometry(Point.fromLngLat(a.lng, a.lat)).apply { addStringProperty("type", a.type) }
        }
        s.getSourceAs<GeoJsonSource>(SRC_ALERTS)?.setGeoJson(FeatureCollection.fromFeatures(features))
    }

    private fun drawCar() {
        val s = style ?: return
        val p = car ?: return
        val f = Feature.fromGeometry(Point.fromLngLat(p.lng, p.lat))
        p.heading?.let { f.addNumberProperty("bearing", it) }
        s.getSourceAs<GeoJsonSource>(SRC_CAR)?.setGeoJson(FeatureCollection.fromFeatures(listOf(f)))
    }

    /** Ve mui ten dieu huong (tam giac xanh vien trang) bang Canvas, khong can file anh. */
    private fun arrowBitmap(): Bitmap {
        val size = 72
        val bmp = Bitmap.createBitmap(size, size, Bitmap.Config.ARGB_8888)
        val path = Path().apply {
            moveTo(size / 2f, 6f)               // mui (huong Bac = huong xe chay)
            lineTo(size - 10f, size - 8f)
            lineTo(size / 2f, size * 0.72f)
            lineTo(10f, size - 8f)
            close()
        }
        val canvas = Canvas(bmp)
        canvas.drawPath(path, Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.WHITE; style = Paint.Style.STROKE; strokeWidth = 8f })
        canvas.drawPath(path, Paint(Paint.ANTI_ALIAS_FLAG).apply { color = Color.parseColor("#1A73E8") })
        return bmp
    }

    // ---------- Vong doi MapView ----------

    fun start() { if (!started && !destroyed) { mapView.onStart(); started = true } }
    fun resume() { if (!resumed && !destroyed) { mapView.onResume(); resumed = true } }
    fun pause() { if (resumed) { mapView.onPause(); resumed = false } }
    fun stop() { if (started) { mapView.onStop(); started = false } }

    fun destroy() {
        if (destroyed) return
        pause(); stop()
        mapView.onDestroy()
        destroyed = true
        map = null
        style = null
    }

    private companion object {
        fun isEmulator(): Boolean =
            Build.HARDWARE.contains("ranchu") || Build.HARDWARE.contains("goldfish") ||
                Build.FINGERPRINT.contains("generic") || Build.PRODUCT.startsWith("sdk_")

        const val SRC_ROUTE = "vr-route"
        const val SRC_ALERTS = "vr-alerts"
        const val SRC_DEST = "vr-destination"
        const val SRC_CAR = "vr-car"
        const val IMG_ARROW = "vr-car-arrow"
    }
}
