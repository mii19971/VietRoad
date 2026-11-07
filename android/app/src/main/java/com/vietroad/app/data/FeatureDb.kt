package com.vietroad.app.data

import android.content.ContentValues
import android.content.Context
import android.database.Cursor
import android.database.sqlite.SQLiteDatabase
import android.database.sqlite.SQLiteOpenHelper
import com.vietroad.app.engine.Geo
import com.vietroad.app.engine.RoadFeature
import com.vietroad.app.engine.SpeedZone
import org.json.JSONArray

/**
 * CSDL SQLite nằm ngay trên thiết bị — toàn bộ cảnh báo chạy offline.
 * Lần đầu mở, dữ liệu gốc được nạp từ assets/vietroad_data.json.
 */
class FeatureDb private constructor(private val context: Context) :
    SQLiteOpenHelper(context, "vietroad.db", null, 1) {

    override fun onCreate(db: SQLiteDatabase) {
        db.execSQL(
            """CREATE TABLE features (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                type_code TEXT NOT NULL,
                lat REAL NOT NULL, lng REAL NOT NULL,
                tile_y INTEGER NOT NULL, tile_x INTEGER NOT NULL,
                heading REAL, heading_tolerance REAL NOT NULL DEFAULT 45,
                speed_limit INTEGER,
                vehicles TEXT NOT NULL DEFAULT 'all',
                road_name TEXT, province TEXT, note TEXT,
                source TEXT NOT NULL DEFAULT 'bundled',
                created_at INTEGER NOT NULL DEFAULT (strftime('%s','now') * 1000))""",
        )
        db.execSQL("CREATE INDEX idx_features_tile ON features(tile_y, tile_x)")
        db.execSQL(
            """CREATE TABLE zones (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                name TEXT,
                points TEXT NOT NULL,
                min_lat REAL NOT NULL, min_lng REAL NOT NULL, max_lat REAL NOT NULL, max_lng REAL NOT NULL,
                max_speed_car INTEGER NOT NULL, max_speed_truck INTEGER,
                oneway INTEGER NOT NULL DEFAULT 0)""",
        )
        db.execSQL("CREATE INDEX idx_zones_bbox ON zones(min_lat, max_lat)")
        db.execSQL(
            """CREATE TABLE events (
                id INTEGER PRIMARY KEY AUTOINCREMENT,
                type TEXT NOT NULL,
                type_code TEXT, feature_id INTEGER,
                lat REAL, lng REAL, speed REAL, speed_limit INTEGER,
                ts INTEGER NOT NULL)""",
        )
        db.execSQL("CREATE INDEX idx_events_ts ON events(ts)")
        loadBundled(db)
    }

    override fun onUpgrade(db: SQLiteDatabase, oldVersion: Int, newVersion: Int) = Unit

    private fun loadBundled(db: SQLiteDatabase): DataIO.Result {
        val json = context.assets.open(BUNDLED_FILE).bufferedReader(Charsets.UTF_8).use { it.readText() }
        return DataIO.importInto(db, json, "bundled")
    }

    // ---------------- truy vấn không gian ----------------

    fun featuresNear(lat: Double, lng: Double, radius: Double): List<RoadFeature> {
        val (dLat, dLng) = Geo.degreeSpan(lat, radius)
        val args = arrayOf(
            Geo.tileY(lat - dLat), Geo.tileY(lat + dLat), Geo.tileX(lng - dLng), Geo.tileX(lng + dLng),
            lat - dLat, lat + dLat, lng - dLng, lng + dLng,
        ).map { it.toString() }.toTypedArray()
        return readableDatabase.rawQuery(
            """SELECT $FEATURE_COLS FROM features
               WHERE tile_y BETWEEN ? AND ? AND tile_x BETWEEN ? AND ?
                 AND lat BETWEEN ? AND ? AND lng BETWEEN ? AND ?""",
            args,
        ).use { c -> buildList { while (c.moveToNext()) add(readFeature(c)) } }
    }

    fun zonesNear(lat: Double, lng: Double, radius: Double): List<SpeedZone> {
        val (dLat, dLng) = Geo.degreeSpan(lat, radius)
        val args = arrayOf(lat - dLat, lat + dLat, lng - dLng, lng + dLng).map { it.toString() }.toTypedArray()
        return readableDatabase.rawQuery(
            "SELECT * FROM zones WHERE max_lat >= ? AND min_lat <= ? AND max_lng >= ? AND min_lng <= ?",
            args,
        ).use { c -> buildList { while (c.moveToNext()) add(readZone(c)) } }
    }

    /** Vùng tốc độ dài nhất — dùng làm lộ trình cho chế độ mô phỏng. */
    fun longestZone(): SpeedZone? =
        readableDatabase.rawQuery("SELECT * FROM zones", null).use { c ->
            buildList { while (c.moveToNext()) add(readZone(c)) }
        }.maxByOrNull { Geo.polylineLength(it.points) }

    // ---------------- ghi dữ liệu ----------------

    /** Tài xế tự thêm điểm ngay trên đường (lưu offline, có thể xuất ra chia sẻ). */
    fun addUserFeature(typeCode: String, lat: Double, lng: Double, heading: Double?, speedLimit: Int?): Long {
        val cv = ContentValues().apply {
            put("type_code", typeCode)
            put("lat", lat)
            put("lng", lng)
            put("tile_y", Geo.tileY(lat))
            put("tile_x", Geo.tileX(lng))
            if (heading == null) putNull("heading") else put("heading", heading)
            if (speedLimit == null) putNull("speed_limit") else put("speed_limit", speedLimit)
            put("source", "user")
            put("note", "Tài xế thêm trên thiết bị")
        }
        return writableDatabase.insertOrThrow("features", null, cv)
    }

    fun deleteFeature(id: Long) {
        writableDatabase.delete("features", "id = ?", arrayOf(id.toString()))
    }

    fun importJson(json: String, replaceAll: Boolean): DataIO.Result {
        val db = writableDatabase
        db.beginTransaction()
        try {
            if (replaceAll) {
                db.delete("features", "source != 'user'", null)
                db.delete("zones", null, null)
            }
            val r = DataIO.importInto(db, json, "import")
            db.setTransactionSuccessful()
            return r
        } finally {
            db.endTransaction()
        }
    }

    fun exportJson(onlyUser: Boolean): String = DataIO.export(readableDatabase, onlyUser)

    /** Xoá dữ liệu nhập thêm, nạp lại dữ liệu gốc đi kèm ứng dụng (giữ điểm tài xế tự thêm). */
    fun resetToBundled(): DataIO.Result {
        val db = writableDatabase
        db.beginTransaction()
        try {
            db.delete("features", "source != 'user'", null)
            db.delete("zones", null, null)
            val r = loadBundled(db)
            db.setTransactionSuccessful()
            return r
        } finally {
            db.endTransaction()
        }
    }

    fun logEvent(type: String, typeCode: String?, featureId: Long?, lat: Double, lng: Double, speed: Double, limit: Int?) {
        val cv = ContentValues().apply {
            put("type", type)
            put("type_code", typeCode)
            if (featureId == null) putNull("feature_id") else put("feature_id", featureId)
            put("lat", lat)
            put("lng", lng)
            put("speed", speed)
            if (limit == null) putNull("speed_limit") else put("speed_limit", limit)
            put("ts", System.currentTimeMillis())
        }
        writableDatabase.insert("events", null, cv)
    }

    data class Stats(val features: Int, val userFeatures: Int, val zones: Int, val warnings7d: Int, val overspeed7d: Int)

    fun stats(): Stats {
        val db = readableDatabase
        fun count(sql: String, vararg a: String) =
            db.rawQuery(sql, arrayOf(*a)).use { if (it.moveToFirst()) it.getInt(0) else 0 }
        val since = (System.currentTimeMillis() - 7 * 86_400_000L).toString()
        return Stats(
            features = count("SELECT COUNT(*) FROM features"),
            userFeatures = count("SELECT COUNT(*) FROM features WHERE source = 'user'"),
            zones = count("SELECT COUNT(*) FROM zones"),
            warnings7d = count("SELECT COUNT(*) FROM events WHERE type = 'warning' AND ts >= ?", since),
            overspeed7d = count("SELECT COUNT(*) FROM events WHERE type = 'overspeed' AND ts >= ?", since),
        )
    }

    fun pruneEvents(olderThanDays: Int = 90) {
        val cutoff = System.currentTimeMillis() - olderThanDays * 86_400_000L
        writableDatabase.delete("events", "ts < ?", arrayOf(cutoff.toString()))
    }

    // ---------------- đọc cursor ----------------

    private fun readFeature(c: Cursor) = RoadFeature(
        id = c.getLong(0),
        typeCode = c.getString(1),
        lat = c.getDouble(2),
        lng = c.getDouble(3),
        heading = if (c.isNull(4)) null else c.getDouble(4),
        headingTolerance = c.getDouble(5),
        speedLimit = if (c.isNull(6)) null else c.getInt(6),
        vehicles = c.getString(7) ?: "all",
        roadName = c.getString(8),
        note = c.getString(9),
    )

    private fun readZone(c: Cursor): SpeedZone {
        val arr = JSONArray(c.getString(c.getColumnIndexOrThrow("points")))
        val pts = (0 until arr.length()).map { i ->
            val p = arr.getJSONArray(i)
            doubleArrayOf(p.getDouble(0), p.getDouble(1))
        }
        val truckIdx = c.getColumnIndexOrThrow("max_speed_truck")
        return SpeedZone(
            id = c.getLong(c.getColumnIndexOrThrow("id")),
            name = c.getString(c.getColumnIndexOrThrow("name")),
            points = pts,
            maxSpeedCar = c.getInt(c.getColumnIndexOrThrow("max_speed_car")),
            maxSpeedTruck = if (c.isNull(truckIdx)) null else c.getInt(truckIdx),
            oneway = c.getInt(c.getColumnIndexOrThrow("oneway")) == 1,
        )
    }

    companion object {
        const val BUNDLED_FILE = "vietroad_data.json"
        private const val FEATURE_COLS =
            "id, type_code, lat, lng, heading, heading_tolerance, speed_limit, vehicles, road_name, note"

        @Volatile private var instance: FeatureDb? = null

        fun get(context: Context): FeatureDb =
            instance ?: synchronized(this) {
                instance ?: FeatureDb(context.applicationContext).also { instance = it }
            }
    }
}
