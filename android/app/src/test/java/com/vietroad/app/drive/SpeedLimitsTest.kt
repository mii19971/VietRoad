package com.vietroad.app.drive

import com.vietroad.app.data.RoadSpeedSegment
import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class SpeedLimitsTest {
    // Cao toc 1 chieu huong Bac (120) va duong gom 2 chieu chay song song cach ~30 m (60).
    private val highway = RoadSpeedSegment(1, 120, oneway = true, points = listOf(listOf(21.000, 105.8000), listOf(21.010, 105.8000)))
    private val frontage = RoadSpeedSegment(2, 60, oneway = false, points = listOf(listOf(21.000, 105.8003), listOf(21.010, 105.8003)))
    // Duong ngang (Dong - Tay) cat qua o vi do 21.005.
    private val crossRoad = RoadSpeedSegment(3, 50, oneway = false, points = listOf(listOf(21.005, 105.79), listOf(21.005, 105.81)))
    private val roads = listOf(highway, frontage, crossRoad)

    @Test
    fun onHighway_headingNorth_gets120() {
        val m = SpeedLimitMatcher()
        assertEquals(120, m.match(roads, 21.002, 105.80002, heading = 0.0)?.maxSpeed)
    }

    @Test
    fun onewayHighway_wrongDirection_notMatched_fallsToFrontageRoad() {
        // Di huong Nam o sat cao tocc (cao toc 1 chieu Bac) -> khong phai cao toc; duong gom 2 chieu cach ~29 m.
        val m = SpeedLimitMatcher(maxDistanceM = 35.0)
        assertEquals(60, m.match(roads, 21.002, 105.80003, heading = 180.0)?.maxSpeed)
    }

    @Test
    fun atIntersection_headingDecidesRoad() {
        val m = SpeedLimitMatcher()
        // Dung tai giao diem cao toc x duong ngang, di huong Dong -> duong ngang (50).
        assertEquals(50, m.match(roads, 21.005, 105.80000, heading = 90.0)?.maxSpeed)
    }

    @Test
    fun farFromAnyRoad_null() {
        assertNull(SpeedLimitMatcher().match(roads, 21.002, 105.805, heading = 0.0))
    }

    @Test
    fun hysteresis_keepsCurrentRoad_whenAnotherIsOnlySlightlyCloser() {
        val m = SpeedLimitMatcher()
        assertEquals(120, m.match(roads, 21.002, 105.80005, heading = null)?.maxSpeed)   // ~5 m toi cao toc
        // Lech ve phia duong gom: ~19 m toi cao toc, ~13 m toi duong gom -> van giu cao toc (chenh < 10 m).
        assertEquals(120, m.match(roads, 21.003, 105.80018, heading = null)?.maxSpeed)
    }

    @Test
    fun overspeed_warnsOnce_thenRepeatsAfter60s_andResetsWhenSlowedDown() {
        val p = OverspeedPolicy()
        assertNull(p.next(84, 80, 0))                                     // trong dung sai 5 km/h
        assertEquals("Bạn đang chạy 95 ki lô mét giờ, quá tốc độ cho phép 80", p.next(95, 80, 1_000))
        assertNull(p.next(96, 80, 30_000))                                 // chua toi 60 s
        assertTrue(p.next(96, 80, 61_000) != null)                         // nhac lai
        assertNull(p.next(78, 80, 62_000))                                 // da giam toc
        assertTrue(p.next(90, 80, 63_000) != null)                         // vuot lai: noi ngay
    }

    @Test
    fun overspeed_newLimit_reevaluates() {
        val p = OverspeedPolicy()
        assertTrue(p.next(95, 80, 0) != null)
        assertTrue(p.next(95, 60, 5_000) != null)   // vao doan gioi han thap hon: noi ngay
        assertFalse(p.isOver(95, null))
    }
}
