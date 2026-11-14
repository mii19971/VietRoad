package com.vietroad.app.data

import kotlinx.serialization.Serializable

// Cac class nay khop voi JSON cua VietRoad.Api (ten truong giong het).
// @Serializable: plugin kotlinx.serialization tu sinh code doi JSON <-> object.

// ----- Gui len -----

@Serializable
data class LoginRequest(val email: String, val password: String)

@Serializable
data class RegisterRequest(val email: String, val password: String, val displayName: String? = null)

@Serializable
data class RefreshRequest(val refreshToken: String)

@Serializable
data class ReportRequest(
    val type: String,
    val lat: Double,
    val lng: Double,
    val direction: Double? = null,   // huong xe dang di (do), null = khong ro
    val speedLimit: Int? = null,
)

@Serializable
data class VoteRequest(val vote: String) // "confirm" | "deny"

// ----- Nhan ve -----

@Serializable
data class UserProfile(val id: String, val email: String, val displayName: String, val role: String)

@Serializable
data class AuthResponse(
    val accessToken: String,
    val refreshToken: String,
    val user: UserProfile,
)

@Serializable
data class Alert(
    val id: Int,
    val type: String,
    val lat: Double,
    val lng: Double,
    val speedLimit: Int? = null,
    val name: String,
    val direction: Double? = null,   // canh bao ap dung cho xe di theo huong nay (do)
    val source: String,
    val confidence: Int = 0,
    val confirmCount: Int = 0,
    val denyCount: Int = 0,
)

@Serializable
data class NearbyAlert(val alert: Alert, val distanceM: Int)

@Serializable
data class NearbyAlertsResponse(val count: Int, val alerts: List<NearbyAlert>)

@Serializable
data class ReportResponse(val alert: Alert, val created: Boolean)

@Serializable
data class ErrorBody(val error: String? = null)

// ----- Gioi han toc do theo doan duong -----

/** 1 doan duong (OSM) co gioi han toc do. points = [[lat, lng], ...], theo chieu xe chay neu oneway. */
@Serializable
data class RoadSpeedSegment(
    val id: Long,
    val maxSpeed: Int,
    val oneway: Boolean = false,
    val name: String? = null,
    val ref: String? = null,
    val points: List<List<Double>>,
)

@Serializable
data class SpeedLimitsResponse(val count: Int, val segments: List<RoadSpeedSegment>)

// ----- Goi dich vu -----

/** Goi hien tai: plan = "free" | "premium"; features = tinh nang duoc dung (vd "navigation"). */
@Serializable
data class SubscriptionInfo(
    val plan: String,
    val planLabel: String,
    val expiresAt: String? = null,   // ISO 8601, vd "2026-12-09T03:00:00+00:00"
    val features: List<String> = emptyList(),
) {
    val isPremium get() = plan == "premium"
    fun has(feature: String) = feature in features

    companion object {
        const val NAVIGATION = "navigation"
        const val POLICE_ALERTS = "police_alerts"
    }
}

// ----- Chi duong -----

@Serializable
data class Place(val name: String, val address: String, val lat: Double, val lng: Double)

@Serializable
data class PlacesResponse(val places: List<Place>)

/** 1 cho can lam gi do (re, vao vong xuyen, den noi...). pointIndex = vi tri trong RouteResponse.points. */
@Serializable
data class RouteStep(
    val pointIndex: Int,
    val lat: Double,
    val lng: Double,
    val type: String,               // depart, turn, roundabout, arrive...
    val modifier: String? = null,   // left, right, slight left, uturn...
    val name: String? = null,       // ten duong sau khi re
    val instruction: String,        // cau tieng Viet server da ghep san
)

/** Tuyen duong: points = cac diem [lat, lng] noi lien. */
@Serializable
data class RouteResponse(
    val distanceM: Double,
    val durationS: Double,
    val points: List<List<Double>>,
    val steps: List<RouteStep>,
)

/** Cac loai canh bao, giong AlertTypes ben server. */
object AlertTypes {
    const val SPEED_CAMERA = "speed_camera"
    const val RED_LIGHT_CAMERA = "red_light_camera"
    const val AVERAGE_SPEED_CAMERA = "average_speed_camera"
    const val POLICE_CHECKPOINT = "police_checkpoint"
    const val TRAFFIC_CAMERA = "traffic_camera"   // camera giam sat (phat nguoi), tu OSM
    const val TOLL_BOOTH = "toll_booth"           // tram thu phi, tu OSM
    const val RAILWAY_CROSSING = "railway_crossing" // duong ngang duong sat, tu OSM
    const val SPEED_BUMP = "speed_bump"           // go / vach giam toc, tu OSM

    fun label(type: String): String = when (type) {
        SPEED_CAMERA -> "Camera tốc độ"
        RED_LIGHT_CAMERA -> "Camera đèn đỏ"
        AVERAGE_SPEED_CAMERA -> "Camera tốc độ trung bình"
        POLICE_CHECKPOINT -> "Chốt CSGT"
        TRAFFIC_CAMERA -> "Camera giám sát"
        TOLL_BOOTH -> "Trạm thu phí"
        RAILWAY_CROSSING -> "Đường ngang đường sắt"
        SPEED_BUMP -> "Gờ giảm tốc"
        else -> type
    }

    /**
     * Ten hien thi / doc cho 1 canh bao. Diem nguy hiem dung ten server dat (vd "Đường ngang
     * không có rào chắn", "Vạch giảm tốc") vi chi tiet hon ten loai; camera thi dung ten loai.
     */
    fun title(alert: Alert): String =
        if (alert.type == RAILWAY_CROSSING || alert.type == SPEED_BUMP) alert.name.ifBlank { label(alert.type) }
        else label(alert.type)

    /**
     * Diem nguy hiem NAM TREN 1 con duong: direction = huong con duong (truc, 0..180, 2 chieu nhu nhau),
     * khong phai huong camera. App chi bao khi xe chay doc con duong do.
     */
    fun hasRoadAxis(type: String): Boolean = type == RAILWAY_CROSSING || type == SPEED_BUMP

    /** Loai "nho": chi doc 1 lan khi da gan (doc tu xa moi go giam toc thi qua on). */
    fun announceOnlyWhenNear(type: String): Boolean = type == SPEED_BUMP
}
