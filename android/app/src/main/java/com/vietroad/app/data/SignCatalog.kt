package com.vietroad.app.data

import com.vietroad.app.engine.Category
import com.vietroad.app.engine.SignType

/** Danh mục loại biển/điểm tích hợp sẵn, mã theo QCVN 41:2019/BGTVT. */
object SignCatalog {

    val all: List<SignType> = listOf(
        t("CAM_SPEED", "Camera giám sát tốc độ", Category.CAMERA, "Phía trước có camera giám sát tốc độ", 1, 800),
        t("CAM_REDLIGHT", "Camera giám sát đèn đỏ", Category.CAMERA, "Phía trước có camera giám sát đèn đỏ", 1, 400),
        t("CAM_TRAFFIC", "Camera giám sát giao thông", Category.CAMERA, "Phía trước có camera giám sát giao thông", 2, 500),
        t("CAM_MOBILE", "Điểm CSGT hay kiểm tra tốc độ", Category.CAMERA, "Phía trước là khu vực cảnh sát giao thông thường kiểm tra tốc độ", 1, 800),

        t("P.127", "Tốc độ tối đa cho phép", Category.SPEED, "Tốc độ tối đa {limit} ki lô mét trên giờ", 2, 300),
        t("DP.134", "Hết hạn chế tốc độ tối đa", Category.SPEED, "Hết đoạn đường hạn chế tốc độ", 4, 200),

        t("P.102", "Cấm đi ngược chiều", Category.PROHIBITION, "Phía trước cấm đi ngược chiều", 2, 200),
        t("P.103a", "Cấm xe ô tô", Category.PROHIBITION, "Phía trước cấm xe ô tô", 2, 300),
        t("P.106a", "Cấm xe ô tô tải", Category.PROHIBITION, "Phía trước cấm xe tải", 2, 300),
        t("P.115", "Hạn chế trọng tải toàn bộ xe", Category.PROHIBITION, "Phía trước hạn chế trọng tải xe", 3, 400),
        t("P.117", "Hạn chế chiều cao", Category.PROHIBITION, "Phía trước hạn chế chiều cao xe", 3, 400),
        t("P.123a", "Cấm rẽ trái", Category.PROHIBITION, "Phía trước cấm rẽ trái", 3, 250),
        t("P.123b", "Cấm rẽ phải", Category.PROHIBITION, "Phía trước cấm rẽ phải", 3, 250),
        t("P.124a", "Cấm quay đầu xe", Category.PROHIBITION, "Phía trước cấm quay đầu xe", 3, 250),
        t("P.124c", "Cấm rẽ trái và quay đầu xe", Category.PROHIBITION, "Phía trước cấm rẽ trái và quay đầu xe", 3, 250),
        t("P.130", "Cấm dừng xe và đỗ xe", Category.PROHIBITION, "Khu vực cấm dừng và đỗ xe", 4, 150),
        t("P.131a", "Cấm đỗ xe", Category.PROHIBITION, "Khu vực cấm đỗ xe", 5, 150),

        t("W.201a", "Chỗ ngoặt nguy hiểm vòng bên trái", Category.DANGER, "Chú ý, khúc cua nguy hiểm bên trái", 3, 300),
        t("W.201b", "Chỗ ngoặt nguy hiểm vòng bên phải", Category.DANGER, "Chú ý, khúc cua nguy hiểm bên phải", 3, 300),
        t("W.203b", "Đường bị thu hẹp", Category.DANGER, "Phía trước đường bị thu hẹp", 4, 250),
        t("W.205a", "Đường giao nhau", Category.DANGER, "Phía trước có đường giao nhau", 5, 200),
        t("W.208", "Giao nhau với đường ưu tiên", Category.DANGER, "Chú ý, giao nhau với đường ưu tiên", 4, 250),
        t("W.210", "Giao nhau với đường sắt có rào chắn", Category.DANGER, "Phía trước giao cắt đường sắt có rào chắn", 2, 400),
        t("W.211a", "Giao nhau với đường sắt không có rào chắn", Category.DANGER, "Nguy hiểm, giao cắt đường sắt không có rào chắn", 1, 400),
        t("W.224", "Đường người đi bộ cắt ngang", Category.DANGER, "Chú ý người đi bộ cắt ngang", 4, 200),
        t("W.225", "Trẻ em", Category.DANGER, "Chú ý, khu vực trường học, giảm tốc độ", 3, 300),
        t("W.227", "Công trường", Category.DANGER, "Phía trước có công trường", 3, 300),
        t("W.245a", "Đi chậm", Category.DANGER, "Đề nghị đi chậm", 4, 250),

        t("R.302a", "Hướng phải đi vòng chướng ngại vật", Category.MANDATORY, "Chú ý chướng ngại vật, đi vòng sang phải", 4, 200),

        t("BLACKSPOT", "Điểm đen tai nạn", Category.HAZARD, "Chú ý, phía trước là điểm đen tai nạn giao thông", 1, 500),
        t("FLOOD", "Điểm thường ngập nước", Category.HAZARD, "Phía trước là điểm thường xuyên ngập nước", 4, 400),

        t("TOLL", "Trạm thu phí", Category.INFO, "Phía trước có trạm thu phí", 3, 1000),
        t("IE.452", "Bắt đầu đường cao tốc", Category.INFO, "Bắt đầu vào đường cao tốc", 4, 300),
        t("IE.453", "Kết thúc đường cao tốc", Category.INFO, "Sắp kết thúc đường cao tốc", 4, 500),
        t("REST", "Trạm dừng nghỉ", Category.INFO, "Phía trước có trạm dừng nghỉ", 6, 1500),
    )

    val byCode: Map<String, SignType> = all.associateBy { it.code }

    /** Ký hiệu ngắn vẽ trên biển (SignView). Chỉ dùng ký tự có sẵn trong font Roboto. */
    fun glyph(code: String): String = when (code) {
        "P.103a" -> "Ô TÔ"
        "P.106a" -> "TẢI"
        "P.115" -> "TẤN"
        "P.117" -> "↕"
        "P.123a" -> "←"
        "P.123b" -> "→"
        "P.124a" -> "U"
        "P.124c" -> "←U"
        "W.201a" -> "←"
        "W.201b" -> "→"
        "W.203b" -> ")("
        "W.205a" -> "+"
        "W.208" -> "T"
        "W.210" -> "RÀO"
        "W.211a" -> "TÀU"
        "W.224" -> "ĐI BỘ"
        "W.225" -> "TRẺ EM"
        "W.227" -> "THI CÔNG"
        "W.245a" -> "CHẬM"
        "R.302a" -> "↘"
        "BLACKSPOT" -> "!"
        "FLOOD" -> "NGẬP"
        "TOLL" -> "THU PHÍ"
        "IE.452" -> "CAO TỐC"
        "IE.453" -> "HẾT CT"
        "REST" -> "P"
        "CAM_MOBILE" -> "CSGT"
        else -> ""
    }

    private fun t(code: String, name: String, c: Category, tts: String, priority: Int, warn: Int) =
        SignType(code, name, c, tts, priority, warn)
}
