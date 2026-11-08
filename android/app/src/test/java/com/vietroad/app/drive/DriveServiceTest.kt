package com.vietroad.app.drive

import com.vietroad.app.data.Alert
import org.junit.Assert.assertEquals
import org.junit.Test

/** Kiem tra noi dung dong thong bao cua dich vu nen. */
class DriveServiceTest {
    private val position = Position(lat = 21.0, lng = 105.8, speedKmh = 40, heading = 0.0)
    private val cam = Alert(id = 1, type = "speed_camera", lat = 21.004, lng = 105.8, name = "Cam",
        source = "osm", speedLimit = 60)

    @Test
    fun `chua co vi tri thi bao dang cho`() {
        assertEquals("Đang chờ tín hiệu vị trí…", DriveService.notificationText(DriveState(tracking = true)))
    }

    @Test
    fun `khong co canh bao phia truoc`() {
        assertEquals("Không có cảnh báo phía trước",
            DriveService.notificationText(DriveState(tracking = true, position = position)))
    }

    @Test
    fun `canh bao gan nhat, khoang cach lam tron 50 m`() {
        val state = DriveState(tracking = true, position = position, ahead = listOf(AheadAlert(cam, 437)))
        assertEquals("Camera tốc độ (60 km/h) · còn 450 m", DriveService.notificationText(state))
    }

    @Test
    fun `dang chi duong thi hien chi dan sap toi`() {
        val nav = NavState("Hồ Gươm", instruction = "Rẽ trái vào Phố Huế", distanceToStepM = 304, remainingM = 1000, remainingS = 100)
        val state = DriveState(tracking = true, position = position, ahead = listOf(AheadAlert(cam, 437)), nav = nav)
        assertEquals("Rẽ trái vào Phố Huế · 300 m", DriveService.notificationText(state))
        assertEquals("Đang tính lại đường đi…",
            DriveService.notificationText(state.copy(nav = nav.copy(rerouting = true))))
    }
}
