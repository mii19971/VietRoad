package com.vietroad.app.drive

import com.vietroad.app.data.Alert
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

/** Unit test chay tren may tinh (khong can dien thoai): ./gradlew testDebugUnitTest */
class AlertLogicTest {

    // Xe o Ho Guom. 0.0045 do vi do ~ 500 m.
    private val carLat = 21.0285
    private val carLng = 105.8542

    private fun alert(id: Int, lat: Double, lng: Double, direction: Double? = null, speedLimit: Int? = null) =
        Alert(id = id, type = "speed_camera", lat = lat, lng = lng, name = "Cam $id",
            source = "osm", direction = direction, speedLimit = speedLimit)

    private val north = alert(1, carLat + 0.0045, carLng)   // ~500 m phia Bac
    private val south = alert(2, carLat - 0.0027, carLng)   // ~300 m phia Nam

    @Test
    fun distance_HaNoiToHcm_isAbout1140Km() {
        val d = Geo.distanceMeters(21.0285, 105.8542, 10.7769, 106.7009)
        assertTrue(d in 1_130_000.0..1_150_000.0)
    }

    @Test
    fun angleDifference_wrapsAround() {
        assertEquals(20.0, Geo.angleDifference(350.0, 10.0), 0.001)
    }

    @Test
    fun drivingNorth_onlyNorthAlertIsAhead() {
        val result = AlertLogic.ahead(listOf(north, south), carLat, carLng, heading = 0.0)
        assertEquals(listOf(1), result.map { it.alert.id })
        assertTrue(result[0].distanceM in 480..520)
    }

    @Test
    fun standingStill_allAlertsSortedByDistance() {
        val result = AlertLogic.ahead(listOf(north, south), carLat, carLng, heading = null)
        assertEquals(listOf(2, 1), result.map { it.alert.id }) // phia Nam gan hon
    }

    @Test
    fun cameraForOppositeDirection_isIgnored() {
        // Camera phia truoc nhung danh cho xe chay huong Nam (180) -> xe chay huong Bac khong can canh bao.
        val opposite = alert(3, carLat + 0.0045, carLng, direction = 180.0)
        val sameWay = alert(4, carLat + 0.0045, carLng + 0.0001, direction = 10.0)

        val result = AlertLogic.ahead(listOf(opposite, sameWay), carLat, carLng, heading = 0.0)
        assertEquals(listOf(4), result.map { it.alert.id })
    }

    @Test
    fun policy_speaksAt500mThen150m_onlyOnceEach() {
        val policy = AnnouncementPolicy()
        val cam = alert(5, 0.0, 0.0, speedLimit = 60)

        assertNull(policy.next(AheadAlert(cam, 800)))                       // con xa: chua doc
        assertEquals("Phía trước 450 mét có Camera tốc độ, giới hạn 60 ki lô mét giờ",
            policy.next(AheadAlert(cam, 460)))                                // lan 1 (lam tron 50 m)
        assertNull(policy.next(AheadAlert(cam, 400)))                       // khong doc lap
        assertEquals("Sắp tới Camera tốc độ, giới hạn 60 ki lô mét giờ",
            policy.next(AheadAlert(cam, 140)))                                // lan 2
        assertNull(policy.next(AheadAlert(cam, 50)))
    }

    @Test
    fun policy_speaksAgain_afterPassingAndComingBack() {
        val policy = AnnouncementPolicy()
        val cam = alert(5, 0.0, 0.0, speedLimit = 60)
        fun step(distanceM: Int?): String? {
            val ahead = distanceM?.let { listOf(AheadAlert(cam, it)) } ?: emptyList()
            policy.forgetPassed(ahead)
            return policy.next(ahead.firstOrNull())
        }

        assertTrue(step(450) != null)   // doc lan 1
        assertTrue(step(140) != null)   // doc lan 2
        assertNull(step(100))           // dang toi gan: khong doc lap
        assertNull(step(null))          // vuot qua: camera o phia sau, khong con trong danh sach
        assertTrue(step(450) != null)   // quay lai doan duong nay: doc lai
    }

    @Test
    fun policy_keepsMemory_whileStillClose() {
        val policy = AnnouncementPolicy()
        val cam = alert(5, 0.0, 0.0)
        val near = listOf(AheadAlert(cam, 450))
        policy.forgetPassed(near); assertTrue(policy.next(near.first()) != null)
        // GPS rung, khoang cach nhay len 600 m (van < 800): khong quen -> khong doc lai
        val jitter = listOf(AheadAlert(cam, 600))
        policy.forgetPassed(jitter); assertNull(policy.next(jitter.first()))
        val back = listOf(AheadAlert(cam, 480))
        policy.forgetPassed(back); assertNull(policy.next(back.first()))
    }
}
