package com.vietroad.app.drive

import org.junit.Assert.assertFalse
import org.junit.Assert.assertTrue
import org.junit.Test

class LocationTrackerTest {
    private val now = 100_000L

    @Test
    fun gpsFix_isAlwaysAccepted() =
        assertTrue(LocationTracker.shouldAccept("gps", now, lastGpsFixAtMs = now))

    @Test
    fun networkFix_isIgnoredWhileGpsIsActive() =
        assertFalse(LocationTracker.shouldAccept("network", now, lastGpsFixAtMs = now - 2_000))

    @Test
    fun networkFix_isUsedWhenGpsIsSilentOrMissing() {
        assertTrue(LocationTracker.shouldAccept("network", now, lastGpsFixAtMs = now - 15_000)) // mat ve tinh
        assertTrue(LocationTracker.shouldAccept("network", now, lastGpsFixAtMs = Long.MIN_VALUE / 2)) // khong co GPS
    }
}
