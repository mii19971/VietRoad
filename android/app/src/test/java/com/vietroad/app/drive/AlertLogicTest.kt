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

    @Test
    fun speedBump_announcedOnlyWhenNear_withServerName() {
        val bump = Alert(id = 9, type = "speed_bump", lat = carLat, lng = carLng, name = "Vạch giảm tốc", source = "osm")
        val policy = AnnouncementPolicy()
        assertNull(policy.next(AheadAlert(bump, 400)))                       // xa: khong doc
        assertEquals("Sắp tới Vạch giảm tốc", policy.next(AheadAlert(bump, 140)))
        assertNull(policy.next(AheadAlert(bump, 100)))                       // khong doc lai
    }

    @Test
    fun railwayCrossing_announcedFarAndNear() {
        val crossing = Alert(id = 8, type = "railway_crossing", lat = carLat, lng = carLng,
            name = "Đường ngang không có rào chắn", source = "osm")
        val policy = AnnouncementPolicy()
        assertEquals("Phía trước 400 mét có Đường ngang không có rào chắn", policy.next(AheadAlert(crossing, 410)))
        assertEquals("Sắp tới Đường ngang không có rào chắn", policy.next(AheadAlert(crossing, 140)))
    }

    @Test
    fun bumpNearer_doesNotHideCameraFurtherAhead() {
        val bump = AheadAlert(Alert(id = 9, type = "speed_bump", lat = 0.0, lng = 0.0, name = "Gờ giảm tốc", source = "osm"), 300)
        val cam = AheadAlert(alert(1, 0.0, 0.0), 450)
        val policy = AnnouncementPolicy()
        assertEquals("Phía trước 450 mét có Camera tốc độ", policy.next(listOf(bump, cam)))
        assertNull(policy.next(listOf(bump, cam)))
    }

    @Test
    fun railwayCrossing_onSideStreet_ignoredWhenDrivingParallel() {
        // Duong ngang ~200 m phia Bac, nam tren ngo chay Dong - Tay (truc 90).
        val crossing = Alert(id = 7, type = "railway_crossing", lat = carLat + 0.0018, lng = carLng + 0.0002,
            name = "Đường ngang đường sắt", source = "osm", direction = 90.0)
        // Xe di huong Bac doc pho (song song duong sat) -> khong bao, ke ca khi di sat ben canh.
        assertTrue(AlertLogic.ahead(listOf(crossing), carLat, carLng, heading = 0.0).isEmpty())
        assertTrue(AlertLogic.ahead(listOf(crossing), carLat + 0.0018, carLng, heading = 0.0).isEmpty())
        // Xe re vao ngo, di huong Tay (270) ve phia duong ngang -> bao (chieu nao cung duoc).
        assertEquals(1, AlertLogic.ahead(listOf(crossing), carLat + 0.0018, carLng + 0.0012, heading = 270.0).size)
    }

    @Test
    fun railwayCrossing_onParallelAlley_ignored() {
        // Di huong Tay. Duong ngang tren ngo song song (cung truc Dong - Tay) cach ~250 m ve phia Nam, xa ~450 m.
        val alley = Alert(id = 6, type = "railway_crossing", lat = carLat - 0.00225, lng = carLng - 0.0040,
            name = "Đường ngang đường sắt", source = "osm", direction = 92.0)
        val onMyRoad = alley.copy(id = 5, lat = carLat + 0.0001)
        val ahead = AlertLogic.ahead(listOf(alley, onMyRoad), carLat, carLng, heading = 270.0)
        assertEquals(listOf(5), ahead.map { it.alert.id })
    }

    @Test
    fun schoolZone_announcedOnlyWhenNear_generic() {
        val school = Alert(id = 4, type = "school_zone", lat = carLat, lng = carLng,
            name = "Trường Tiểu học Văn Miếu", source = "osm")
        val policy = AnnouncementPolicy()
        assertNull(policy.next(AheadAlert(school, 400)))                 // xa: khong doc
        assertEquals("Sắp tới Khu vực trường học", policy.next(AheadAlert(school, 140)))  // doc chung, khong doc ten truong
    }
}
