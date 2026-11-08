package com.vietroad.app.drive

import com.vietroad.app.data.RouteResponse
import com.vietroad.app.data.RouteStep
import org.junit.Assert.assertEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test
import kotlin.math.abs

class NavigatorTest {
    // Tuyen mau: di thang len phia Bac ~1,1 km, re phai sang phia Dong ~520 m roi den noi.
    private val route = RouteResponse(
        distanceM = 1625.0,
        durationS = 200.0,
        points = listOf(listOf(21.000, 105.0), listOf(21.005, 105.0), listOf(21.010, 105.0), listOf(21.010, 105.005)),
        steps = listOf(
            RouteStep(0, 21.000, 105.0, "depart", null, "Phố A", "Đi theo Phố A"),
            RouteStep(2, 21.010, 105.0, "turn", "right", "Phố B", "Rẽ phải vào Phố B"),
            RouteStep(3, 21.010, 105.005, "arrive", null, null, "Đã đến nơi"),
        ),
    )

    private fun near(expected: Int, actual: Int, tolerance: Int = 10) =
        assertTrue("mong doi ~$expected, thuc te $actual", abs(expected - actual) <= tolerance)

    @Test
    fun onRoute_nextStepIsTurn_withDistanceAlongRoute() {
        val p = Navigator(route).update(21.002, 105.0)
        assertEquals(1, p.nextStepIndex)
        near(885, p.distanceToStepM)          // con ~885 m toi cho re phai
        near(885 + 519, p.remainingM)
        near(0, p.offRouteM, 1)
    }

    @Test
    fun slightlyBesideRoad_reportsSmallOffset() {
        val p = Navigator(route).update(21.002, 105.0001) // lech ~10 m sang ben
        near(10, p.offRouteM, 2)
        assertEquals(1, p.nextStepIndex)
    }

    @Test
    fun afterTurn_nextStepIsArrival() {
        val nav = Navigator(route)
        nav.update(21.009, 105.0)
        val p = nav.update(21.010, 105.002)
        assertEquals(2, p.nextStepIndex)
        near(311, p.distanceToStepM)
    }

    @Test
    fun farFromRoute_isDetectedAsOffRoute() {
        val p = Navigator(route).update(21.002, 105.01) // cach tuyen ~1 km
        assertTrue(p.offRouteM > 500)
    }

    @Test
    fun voice_speaksEarlyThenAtTurn_onlyOnceEach() {
        val voice = NavigationVoice()
        val turn = route.steps[1]
        fun at(d: Int, speed: Int? = 30) = voice.next(NavProgress(1, d, d + 500, 100, 0), turn, speed)

        assertNull(at(600))                                          // con xa
        assertEquals("Sau 300 mét, rẽ phải vào Phố B", at(290))     // bao truoc
        assertNull(at(200))                                          // khong lap lai
        assertEquals("Rẽ phải vào Phố B", at(40))                    // tai cho re
        assertNull(at(20))
    }

    @Test
    fun voice_fastCar_warnsEarlier() {
        val voice = NavigationVoice()
        val p = NavProgress(1, 780, 1300, 100, 0)
        assertEquals("Sau 800 mét, rẽ phải vào Phố B", voice.next(p, route.steps[1], speedKmh = 80))
    }

    @Test
    fun voice_arrival() {
        val voice = NavigationVoice()
        assertEquals("Còn 300 mét nữa là đến nơi", voice.next(NavProgress(2, 290, 290, 30, 0), route.steps[2], 20))
        assertNull(voice.next(NavProgress(2, 45, 45, 5, 0), route.steps[2], 20))
        assertEquals("Đã đến nơi", voice.next(NavProgress(2, 25, 25, 3, 0), route.steps[2], 20))
    }

    @Test
    fun offRoute_startingInParkingLot_isNotOffRoute() {
        val d = OffRouteDetector()
        // Xe trong bai do cach con duong 90 m, dung yen: KHONG duoc coi la di sai.
        repeat(10) { assertTrue(!d.update(90, 5)) }
        assertEquals(0, d.consecutiveOff)
        // Lai ra duong (sat tuyen) roi di tiep binh thuong.
        assertTrue(!d.update(10, 5))
        // Da vao tuyen, sau do re nham: lech 60 m la sai.
        assertTrue(d.update(60, 5))
    }

    @Test
    fun offRoute_neverJoined_butDrivingAway_isOffRoute() {
        val d = OffRouteDetector()
        d.update(90, 5)                      // bat dau cach tuyen 90 m
        repeat(3) { assertTrue(d.update(300, 5)) } // di nguoc huong, xa them > 150 m
        assertEquals(3, d.consecutiveOff)
    }

    @Test
    fun offRoute_poorGps_widerThreshold() {
        val d = OffRouteDetector()
        d.update(5, 5)                       // vao tuyen
        assertTrue(!d.update(60, 50))        // GPS sai so 50 m -> nguong 75 m: chua coi la sai
        assertTrue(d.update(60, 5))          // GPS tot -> nguong 40 m: sai
    }

    @Test
    fun format() {
        assertEquals("13070 km", NavFormat.distance(13_069_800))
        assertEquals("13070 ki lô mét", NavFormat.spokenDistance(13_069_800))
        assertEquals("350 m", NavFormat.distance(357))
        assertEquals("1,3 km", NavFormat.distance(1250))
        assertEquals("2 km", NavFormat.distance(2000))
        assertEquals("300 mét", NavFormat.spokenDistance(290))
        assertEquals("50 mét", NavFormat.spokenDistance(10))
        assertEquals("1,5 ki lô mét", NavFormat.spokenDistance(1500))
        assertEquals("5 phút", NavFormat.duration(250))
        assertEquals("1 giờ 5 phút", NavFormat.duration(3900))
    }
}
