package com.vietroad.app.drive

import com.vietroad.app.data.Alert
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Test

class PassReviewTrackerTest {
    private val carLat = 21.0285
    private val carLng = 105.8542

    private fun alert(id: Int, type: String = "speed_camera", offsetNorthM: Double = 0.0) =
        Alert(id = id, type = type, lat = carLat + offsetNorthM / 110574.0, lng = carLng,
            name = "Điểm $id", source = "user")

    @Test
    fun asksAfterPassing_notWhileApproaching() {
        val t = PassReviewTracker()
        val cam = alert(1, offsetNorthM = 0.0)
        // Dang toi gan (60 m): chua hoi.
        assertNull(t.passed(listOf(cam), carLat - 60 / 110574.0, carLng))
        // Dang o ngay canh (0 m): van chua hoi.
        assertNull(t.passed(listOf(cam), carLat, carLng))
        // Da di qua (150 m): hoi.
        assertEquals(1, t.passed(listOf(cam), carLat + 150 / 110574.0, carLng)?.id)
        // Khong hoi lai diem do.
        assertNull(t.passed(listOf(cam), carLat + 300 / 110574.0, carLng))
    }

    @Test
    fun ignoresPointsNeverApproached() {
        val t = PassReviewTracker()
        // Diem cach 500 m, xe khong he toi gan -> khong hoi du "ra xa".
        val far = alert(2, offsetNorthM = 500.0)
        assertNull(t.passed(listOf(far), carLat, carLng))
    }

    @Test
    fun ignoresNonReviewableTypes() {
        val t = PassReviewTracker()
        val toll = alert(3, type = "toll_booth")
        assertNull(t.passed(listOf(toll), carLat, carLng))                       // toi gan
        assertNull(t.passed(listOf(toll), carLat + 150 / 110574.0, carLng))      // da qua: van khong hoi
    }

    @Test
    fun resetAllowsAskingAgain() {
        val t = PassReviewTracker()
        val cam = alert(1)
        t.passed(listOf(cam), carLat, carLng)
        assertEquals(1, t.passed(listOf(cam), carLat + 150 / 110574.0, carLng)?.id)
        t.reset()
        t.passed(listOf(cam), carLat, carLng)
        assertEquals(1, t.passed(listOf(cam), carLat + 150 / 110574.0, carLng)?.id)
    }
}
