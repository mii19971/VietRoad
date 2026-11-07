package com.vietroad.app.data

import org.junit.Assert.assertEquals
import org.junit.Assert.assertFalse
import org.junit.Assert.assertNotEquals
import org.junit.Assert.assertNull
import org.junit.Assert.assertTrue
import org.junit.Test

class DataFieldsTest {

    @Test
    fun featureUidMatchesSampleGenerator() {
        // Giá trị do tools/gen-sample-data.mjs sinh ra — app và script phải cho cùng mã
        assertEquals("9322bba5-d1d2-3972-82b8-3697ec56de75", DataFields.featureUid("P.127", 21.01342, 105.532385, 92.0))
        assertEquals("4c26aad0-e65d-3f6a-846d-3918f5d92b79", DataFields.featureUid("BLACKSPOT", 21.006401, 105.647982, null))
    }

    @Test
    fun zoneUidMatchesSampleGenerator() {
        assertEquals(
            "470a6a14-a7c2-3743-b0eb-ae6e01da4f8b",
            DataFields.zoneUid("Đường Láng", doubleArrayOf(21.0255, 105.803), doubleArrayOf(21.0035, 105.814)),
        )
    }

    @Test
    fun featureUidDependsOnDirection() {
        val north = DataFields.featureUid("CAM_SPEED", 21.0, 105.8, 0.0)
        val south = DataFields.featureUid("CAM_SPEED", 21.0, 105.8, 180.0)
        assertNotEquals(north, south)
        assertEquals(north, DataFields.featureUid("CAM_SPEED", 21.0, 105.8, 0.2)) // làm tròn hướng
    }

    @Test
    fun parseTimeFormats() {
        val day = 1_740_787_200_000L // 2025-03-01T00:00:00Z
        assertEquals(day, DataFields.parseTime("2025-03-01"))
        assertEquals(day, DataFields.parseTime("2025-03-01T00:00:00Z"))
        assertEquals(day, DataFields.parseTime("2025-03-01T00:00:00.000Z"))
        assertEquals(day + 8 * 3_600_000L, DataFields.parseTime("2025-03-01T08:00:00"))
        assertEquals(day, DataFields.parseTime(1_740_787_200)) // giây
        assertEquals(day, DataFields.parseTime(day)) // mili giây
        assertEquals(day, DataFields.parseTime("1740787200"))
    }

    @Test
    fun parseTimeRejectsGarbage() {
        assertNull(DataFields.parseTime("01/03/2025"))
        assertNull(DataFields.parseTime("2025-13-01")) // tháng 13
        assertNull(DataFields.parseTime("2025-03-01 rác"))
        assertNull(DataFields.parseTime(-5))
        assertNull(DataFields.parseTime(true))
    }

    @Test
    fun formatTimeRoundTrips() {
        val t = 1_740_820_000_000L - 1_740_820_000_000L % 1000
        assertEquals(t, DataFields.parseTime(DataFields.formatTime(t)))
        assertEquals("2025-03-01T00:00:00Z", DataFields.formatTime(1_740_787_200_000L))
    }

    @Test
    fun confidenceRange() {
        assertTrue(DataFields.validConfidence(0.0))
        assertTrue(DataFields.validConfidence(1.0))
        assertFalse(DataFields.validConfidence(1.5))
        assertFalse(DataFields.validConfidence(Double.NaN))
    }
}
