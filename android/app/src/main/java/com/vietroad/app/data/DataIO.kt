package com.vietroad.app.data

import android.content.ContentValues
import android.database.sqlite.SQLiteDatabase
import com.vietroad.app.engine.Geo
import org.json.JSONArray
import org.json.JSONObject
import org.json.JSONTokener

/**
 * Nhập/xuất dữ liệu dạng JSON để cập nhật CSDL hoàn toàn offline (qua USB, thẻ nhớ...).
 *
 * Hỗ trợ 2 định dạng:
 * 1. VietRoad: { "features": [{type_code, lat, lng, heading, speed_limit, vehicles, road_name, province, note}],
 *               "zones": [{name, points: [[lat,lng],...], max_speed_car, max_speed_truck, oneway}] }
 * 2. GeoJSON FeatureCollection: Point → điểm cảnh báo, LineString có max_speed_car → vùng tốc độ.
 *    (GeoJSON dùng thứ tự [lng, lat])
 */
object DataIO {

    data class Result(val features: Int, val zones: Int, val skipped: Int)

    fun importInto(db: SQLiteDatabase, json: String, source: String): Result {
        var features = 0
        var zones = 0
        var skipped = 0

        fun addFeature(o: JSONObject, lat: Double, lng: Double) {
            val cv = featureValues(o, lat, lng, source)
            if (cv == null) skipped++ else {
                db.insertOrThrow("features", null, cv)
                features++
            }
        }

        fun addZone(o: JSONObject, points: List<DoubleArray>) {
            val cv = zoneValues(o, points)
            if (cv == null) skipped++ else {
                db.insertOrThrow("zones", null, cv)
                zones++
            }
        }

        when (val root = JSONTokener(json).nextValue()) {
            is JSONArray -> for (i in 0 until root.length()) {
                val o = root.optJSONObject(i) ?: continue
                addFeature(o, o.optDouble("lat"), o.optDouble("lng"))
            }

            is JSONObject -> if (root.optString("type") == "FeatureCollection") {
                val arr = root.optJSONArray("features") ?: JSONArray()
                for (i in 0 until arr.length()) {
                    val item = arr.optJSONObject(i) ?: continue
                    val props = item.optJSONObject("properties") ?: JSONObject()
                    val geom = item.optJSONObject("geometry") ?: continue
                    val coords = geom.optJSONArray("coordinates") ?: continue
                    when (geom.optString("type")) {
                        "Point" -> addFeature(props, coords.optDouble(1), coords.optDouble(0))
                        "LineString" -> addZone(props, (0 until coords.length()).mapNotNull { k ->
                            coords.optJSONArray(k)?.let { doubleArrayOf(it.optDouble(1), it.optDouble(0)) }
                        })
                        else -> skipped++
                    }
                }
            } else {
                val fs = root.optJSONArray("features") ?: JSONArray()
                for (i in 0 until fs.length()) {
                    val o = fs.optJSONObject(i) ?: continue
                    addFeature(o, o.optDouble("lat"), o.optDouble("lng"))
                }
                val zs = root.optJSONArray("zones") ?: JSONArray()
                for (i in 0 until zs.length()) {
                    val o = zs.optJSONObject(i) ?: continue
                    val pts = o.optJSONArray("points") ?: JSONArray()
                    addZone(o, (0 until pts.length()).mapNotNull { k ->
                        pts.optJSONArray(k)?.let { doubleArrayOf(it.optDouble(0), it.optDouble(1)) }
                    })
                }
            }

            else -> throw IllegalArgumentException("Tệp không phải JSON hợp lệ")
        }
        return Result(features, zones, skipped)
    }

    private fun validCoord(lat: Double, lng: Double) =
        !lat.isNaN() && !lng.isNaN() && lat in -90.0..90.0 && lng in -180.0..180.0

    private fun JSONObject.strOrNull(key: String): String? =
        if (isNull(key)) null else optString(key).trim().takeIf { it.isNotEmpty() }

    private fun featureValues(o: JSONObject, lat: Double, lng: Double, source: String): ContentValues? {
        val code = o.optString("type_code")
        if (SignCatalog.byCode[code] == null || !validCoord(lat, lng)) return null
        val heading = if (o.isNull("heading")) null else o.optDouble("heading").takeIf { !it.isNaN() && it in 0.0..360.0 }
        val limit = if (o.isNull("speed_limit")) null else o.optInt("speed_limit", -1).takeIf { it in 5..150 }
        if (code == "P.127" && limit == null) return null
        return ContentValues().apply {
            put("type_code", code)
            put("lat", lat)
            put("lng", lng)
            put("tile_y", Geo.tileY(lat))
            put("tile_x", Geo.tileX(lng))
            if (heading == null) putNull("heading") else put("heading", heading)
            put("heading_tolerance", o.optDouble("heading_tolerance", 45.0).takeIf { it in 5.0..180.0 } ?: 45.0)
            if (limit == null) putNull("speed_limit") else put("speed_limit", limit)
            put("vehicles", o.strOrNull("vehicles") ?: "all")
            put("road_name", o.strOrNull("road_name"))
            put("province", o.strOrNull("province"))
            put("note", o.strOrNull("note"))
            put("source", source)
        }
    }

    private fun zoneValues(o: JSONObject, points: List<DoubleArray>): ContentValues? {
        if (points.size < 2 || points.any { !validCoord(it[0], it[1]) }) return null
        val car = o.optInt("max_speed_car", -1).takeIf { it in 5..150 } ?: return null
        val truck = o.optInt("max_speed_truck", -1).takeIf { it in 5..150 }
        val arr = JSONArray()
        points.forEach { arr.put(JSONArray().put(it[0]).put(it[1])) }
        return ContentValues().apply {
            put("name", o.strOrNull("name"))
            put("points", arr.toString())
            put("min_lat", points.minOf { it[0] })
            put("max_lat", points.maxOf { it[0] })
            put("min_lng", points.minOf { it[1] })
            put("max_lng", points.maxOf { it[1] })
            put("max_speed_car", car)
            if (truck == null) putNull("max_speed_truck") else put("max_speed_truck", truck)
            put("oneway", if (o.optBoolean("oneway", false) || o.optInt("oneway", 0) == 1) 1 else 0)
        }
    }

    /** Xuất ra định dạng VietRoad. onlyUser = chỉ các điểm tài xế tự thêm trên box. */
    fun export(db: SQLiteDatabase, onlyUser: Boolean): String {
        val root = JSONObject()
        root.put("format", "vietroad")
        root.put("exported_at", System.currentTimeMillis())
        val fs = JSONArray()
        val where = if (onlyUser) "WHERE source = 'user'" else ""
        db.rawQuery(
            "SELECT type_code, lat, lng, heading, heading_tolerance, speed_limit, vehicles, road_name, province, note FROM features $where",
            null,
        ).use { c ->
            while (c.moveToNext()) {
                fs.put(JSONObject().apply {
                    put("type_code", c.getString(0))
                    put("lat", c.getDouble(1))
                    put("lng", c.getDouble(2))
                    put("heading", if (c.isNull(3)) JSONObject.NULL else c.getDouble(3))
                    put("heading_tolerance", c.getDouble(4))
                    put("speed_limit", if (c.isNull(5)) JSONObject.NULL else c.getInt(5))
                    put("vehicles", c.getString(6))
                    put("road_name", c.getString(7) ?: JSONObject.NULL)
                    put("province", c.getString(8) ?: JSONObject.NULL)
                    put("note", c.getString(9) ?: JSONObject.NULL)
                })
            }
        }
        root.put("features", fs)
        val zs = JSONArray()
        if (!onlyUser) {
            db.rawQuery("SELECT name, points, max_speed_car, max_speed_truck, oneway FROM zones", null).use { c ->
                while (c.moveToNext()) {
                    zs.put(JSONObject().apply {
                        put("name", c.getString(0) ?: JSONObject.NULL)
                        put("points", JSONArray(c.getString(1)))
                        put("max_speed_car", c.getInt(2))
                        put("max_speed_truck", if (c.isNull(3)) JSONObject.NULL else c.getInt(3))
                        put("oneway", c.getInt(4) == 1)
                    })
                }
            }
        }
        root.put("zones", zs)
        return root.toString()
    }
}
