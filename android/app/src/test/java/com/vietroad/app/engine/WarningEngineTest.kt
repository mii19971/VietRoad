package com.vietroad.app.engine

import com.vietroad.app.data.SignCatalog
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class WarningEngineTest {

    private val engine = WarningEngine(SignCatalog.byCode)

    // Xe ở gốc, chạy hướng Bắc (0°). 0.001° vĩ độ ≈ 111 m.
    private val car = VehicleState(lat = 21.0, lng = 105.8, heading = 0.0, speed = 60.0)

    private fun feature(id: Long, code: String, dLat: Double, dLng: Double = 0.0, heading: Double? = null, limit: Int? = null, vehicles: String = "all") =
        RoadFeature(id, code, 21.0 + dLat, 105.8 + dLng, heading, 45.0, limit, vehicles)

    @Test
    fun cameraAheadIsWarned() {
        val r = engine.evaluate(car, listOf(feature(1, "CAM_SPEED", 0.004)), emptyList())
        assertEquals(1, r.warnings.size)
        assertEquals(445.0, r.warnings[0].distance.toDouble(), 5.0)
    }

    @Test
    fun cameraBehindIsIgnored() {
        val r = engine.evaluate(car, listOf(feature(1, "CAM_SPEED", -0.004)), emptyList())
        assertTrue(r.warnings.isEmpty())
    }

    @Test
    fun oppositeDirectionSignIsIgnored() {
        val southbound = feature(1, "CAM_SPEED", 0.003, heading = 180.0)
        val northbound = feature(2, "CAM_SPEED", 0.003, heading = 0.0)
        val r = engine.evaluate(car, listOf(southbound, northbound), emptyList())
        assertEquals(listOf(2L), r.warnings.map { it.feature.id })
    }

    @Test
    fun tooFarIsIgnored() {
        val r = engine.evaluate(car, listOf(feature(1, "W.205a", 0.004)), emptyList()) // 200 m tầm cảnh báo
        assertTrue(r.warnings.isEmpty())
    }

    @Test
    fun truckOnlySignIgnoredForCar() {
        val f = feature(1, "P.106a", 0.002, vehicles = "truck")
        assertTrue(engine.evaluate(car, listOf(f), emptyList()).warnings.isEmpty())
        assertEquals(1, engine.evaluate(car.copy(vehicleType = "truck"), listOf(f), emptyList()).warnings.size)
    }

    @Test
    fun zoneGivesLimitAndOverspeed() {
        val zone = SpeedZone(1, "Test", listOf(doubleArrayOf(20.99, 105.8), doubleArrayOf(21.01, 105.8)), 50, 40, false)
        val r = engine.evaluate(car.copy(speed = 62.0), emptyList(), listOf(zone))
        assertEquals(50, r.speedLimit)
        assertTrue(r.overspeed)
        assertTrue(r.dangerOverspeed)
        val truck = engine.evaluate(car.copy(speed = 42.0, vehicleType = "truck"), emptyList(), listOf(zone))
        assertEquals(40, truck.speedLimit)
        assertFalse(truck.overspeed) // 42 ≤ 40 + 3
    }

    @Test
    fun zoneOffRoadNotApplied() {
        val zone = SpeedZone(1, "Xa", listOf(doubleArrayOf(20.99, 105.81), doubleArrayOf(21.01, 105.81)), 50, null, false)
        assertNull(engine.evaluate(car, emptyList(), listOf(zone)).speedLimit) // cách ~1 km
    }

    @Test
    fun passingSpeedSignIsRemembered() {
        val sign = feature(1, "P.127", 0.0002, heading = 0.0, limit = 80)
        val r1 = engine.evaluate(car, listOf(sign), emptyList())
        assertEquals(80, r1.lastSignLimit)
        // Đi tiếp 2 km, không còn biển trong tầm: vẫn giữ giới hạn 80
        val r2 = engine.evaluate(car.copy(lat = 21.02, lastSignLimit = r1.lastSignLimit, speed = 90.0), emptyList(), emptyList())
        assertEquals(80, r2.speedLimit)
        assertTrue(r2.overspeed)
    }

    @Test
    fun endOfLimitSignClearsLimit() {
        val end = feature(1, "DP.134", 0.0002, heading = 0.0)
        val r = engine.evaluate(car.copy(lastSignLimit = 60), listOf(end), emptyList())
        assertNull(r.lastSignLimit)
        assertNull(r.speedLimit)
    }

    @Test
    fun ttsFillsLimit() {
        val r = engine.evaluate(car, listOf(feature(1, "P.127", 0.002, limit = 70)), emptyList())
        assertEquals("Tốc độ tối đa 70 ki lô mét trên giờ", r.warnings[0].tts)
    }
}
