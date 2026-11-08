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

    fun label(type: String): String = when (type) {
        SPEED_CAMERA -> "Camera tốc độ"
        RED_LIGHT_CAMERA -> "Camera đèn đỏ"
        AVERAGE_SPEED_CAMERA -> "Camera tốc độ trung bình"
        POLICE_CHECKPOINT -> "Chốt CSGT"
        else -> type
    }
}
