using VietRoad.Data;
using VietRoad.Importer.Osm;

namespace VietRoad.Importer.Tests;

public class OsmAlertMapperTests
{
    private static OsmElement Node(long id, double lat, double lon, Dictionary<string, string>? tags = null) =>
        new("node", id, lat, lon, tags, null);

    private static OsmElement Relation(long id, Dictionary<string, string> tags, params OsmMember[] members) =>
        new("relation", id, null, null, tags, [.. members]);

    [Fact]
    public void SpeedCameraNode_IsMapped()
    {
        var osm = new OsmResponse([
            Node(1, 21.0, 105.8, new() { ["highway"] = "speed_camera", ["maxspeed"] = "60", ["direction"] = "NE" }),
        ]);

        var a = Assert.Single(OsmAlertMapper.Map(osm).Alerts);
        Assert.Equal("osm:node/1", a.ExternalId);
        Assert.Equal(AlertTypes.SpeedCamera, a.Type);
        Assert.Equal(60, a.SpeedLimit);
        Assert.Equal(45, a.Direction);
        Assert.Equal("Camera tốc độ", a.Name);
    }

    [Fact]
    public void RedLightRelation_UsesDeviceNodeLocation()
    {
        var osm = new OsmResponse([
            Relation(10, new() { ["type"] = "enforcement", ["enforcement"] = "traffic_signals" },
                new OsmMember("node", 2, "from"),
                new OsmMember("node", 3, "device")),
            Node(2, 21.0, 105.0),
            Node(3, 21.5, 105.5),
        ]);

        var a = Assert.Single(OsmAlertMapper.Map(osm).Alerts);
        Assert.Equal("osm:relation/10", a.ExternalId);
        Assert.Equal(AlertTypes.RedLightCamera, a.Type);
        Assert.Equal(21.5, a.Lat); // lay node "device", khong phai "from"
    }

    [Fact]
    public void RelationPointingToCameraNode_IsMergedNotDuplicated()
    {
        var osm = new OsmResponse([
            Node(5, 21.0, 105.8, new() { ["highway"] = "speed_camera" }),
            Relation(20, new() { ["type"] = "enforcement", ["enforcement"] = "maxspeed", ["maxspeed"] = "80" },
                new OsmMember("node", 5, "device")),
        ]);

        var a = Assert.Single(OsmAlertMapper.Map(osm).Alerts);
        Assert.Equal("osm:node/5", a.ExternalId);
        Assert.Equal(80, a.SpeedLimit); // lay toc do tu relation
    }

    [Fact]
    public void UnsupportedEnforcement_IsSkipped()
    {
        var osm = new OsmResponse([
            Relation(30, new() { ["type"] = "enforcement", ["enforcement"] = "maxweight" },
                new OsmMember("node", 6, "device")),
            Node(6, 21.0, 105.8),
        ]);

        var result = OsmAlertMapper.Map(osm);
        Assert.Empty(result.Alerts);
        Assert.Equal(1, result.Skipped);
    }

    [Theory]
    [InlineData("50", 50)]
    [InlineData("50 km/h", 50)]
    [InlineData("30 mph", null)]
    [InlineData("VN:urban", null)]
    [InlineData(null, null)]
    public void ParseMaxSpeed_Works(string? input, int? expected) =>
        Assert.Equal(expected, OsmAlertMapper.ParseMaxSpeed(input));

    [Theory]
    [InlineData("90", 90.0)]
    [InlineData("-90", 270.0)]
    [InlineData("SW", 225.0)]
    [InlineData("forward", null)]
    public void ParseDirection_Works(string input, double? expected) =>
        Assert.Equal(expected, OsmAlertMapper.ParseDirection(input));

    // ---------- Camera giam sat, tram thu phi, gop trung ----------

    [Fact]
    public void TrafficSurveillanceCamera_IsMapped_ButOtherSurveillanceIgnored()
    {
        var osm = new OsmResponse([
            Node(1, 21.0, 105.8, new() { ["man_made"] = "surveillance", ["surveillance:zone"] = "traffic", ["camera:direction"] = "180" }),
            Node(2, 21.1, 105.8, new() { ["man_made"] = "surveillance", ["surveillance:type"] = "ALPR" }),
            Node(3, 21.2, 105.8, new() { ["man_made"] = "surveillance", ["surveillance"] = "shop" }), // camera cua hang: bo
        ]);

        var alerts = OsmAlertMapper.Map(osm).Alerts;
        Assert.Equal(2, alerts.Count);
        Assert.All(alerts, a => Assert.Equal(AlertTypes.TrafficCamera, a.Type));
        Assert.Equal(180, alerts.Single(a => a.ExternalId == "osm:node/1").Direction);
        Assert.Equal("Camera giám sát giao thông", alerts[0].Name);
    }

    [Fact]
    public void TollBooth_LanesOfOneStation_AreMergedIntoOne_KeepingName()
    {
        // 1 tram 4 lan xe (moi lan 1 node, cach nhau ~10 m), chi 1 lan co ten; + 1 tram khac cach 5 km.
        var osm = new OsmResponse([
            Node(11, 21.00000, 105.80000, new() { ["barrier"] = "toll_booth" }),
            Node(12, 21.00000, 105.80010, new() { ["barrier"] = "toll_booth", ["name"] = "Trạm thu phí Pháp Vân" }),
            Node(13, 21.00000, 105.80020, new() { ["barrier"] = "toll_booth" }),
            Node(14, 21.00000, 105.80030, new() { ["barrier"] = "toll_booth" }),
            Node(20, 21.05000, 105.80000, new() { ["barrier"] = "toll_booth" }),
        ]);

        var result = OsmAlertMapper.Map(osm);

        Assert.Equal(2, result.Alerts.Count);
        Assert.Equal(3, result.Merged);
        var phapVan = result.Alerts.Single(a => a.Name == "Trạm thu phí Pháp Vân");
        Assert.Equal("osm:node/12", phapVan.ExternalId);          // diem co ten lam dai dien
        Assert.Equal(AlertTypes.TollBooth, phapVan.Type);
        Assert.Contains(result.Alerts, a => a.Name == "Trạm thu phí"); // tram khong ten: ten mac dinh
    }

    [Fact]
    public void TollBoothWay_UsesCenter_AndMergesWithNearbyNode()
    {
        var osm = new OsmResponse([
            new OsmElement("way", 500, null, null, new() { ["barrier"] = "toll_booth", ["name"] = "Trạm thu phí Dầu Giây" },
                null, new OsmCenter(10.9, 107.1)),
            Node(501, 10.9005, 107.1, new() { ["barrier"] = "toll_booth" }),  // cach ~55 m -> cung tram
            new OsmElement("way", 600, null, null, new() { ["barrier"] = "toll_booth" }, null, new OsmCenter(11.5, 107.5)),
        ]);

        var alerts = OsmAlertMapper.Map(osm).Alerts;
        Assert.Equal(2, alerts.Count);
        Assert.Contains(alerts, a => a.ExternalId == "osm:way/500" && a.Lat == 10.9);
        Assert.Contains(alerts, a => a.ExternalId == "osm:way/600");
    }

    [Fact]
    public void Cameras_DuplicateWithin30m_Merged_ButOppositeDirectionsKept()
    {
        var osm = new OsmResponse([
            // 2 node cho 1 camera (cach ~25 m), chi 1 node co gioi han toc do -> gop, giu 60.
            Node(1, 10.95609, 106.81294, new() { ["highway"] = "speed_camera" }),
            Node(2, 10.95622, 106.81270, new() { ["highway"] = "speed_camera", ["maxspeed"] = "60" }),
            // 2 camera cung cho nhung 2 chieu nguoc nhau -> giu ca 2.
            Node(3, 11.0, 107.0, new() { ["highway"] = "speed_camera", ["direction"] = "0" }),
            Node(4, 11.0, 107.0001, new() { ["highway"] = "speed_camera", ["direction"] = "180" }),
        ]);

        var result = OsmAlertMapper.Map(osm);
        Assert.Equal(3, result.Alerts.Count);
        Assert.Equal(1, result.Merged);
        Assert.Equal(60, result.Alerts.Single(a => a.Lat < 10.96).SpeedLimit);
    }

    // ---------- Gioi han toc do duong ----------

    private static OsmElement Way(long id, Dictionary<string, string> tags, params (double Lat, double Lon)[] pts) =>
        new("way", id, null, null, tags, null, null, [.. pts.Select(p => new OsmCenter(p.Lat, p.Lon))]);

    [Fact]
    public void SpeedLimits_ParseOnewayAndSkipNonNumeric()
    {
        var osm = new OsmResponse([
            Way(1, new() { ["highway"] = "trunk", ["maxspeed"] = "80", ["ref"] = "QL1A" }, (21, 105), (21.001, 105)),
            Way(2, new() { ["highway"] = "motorway", ["maxspeed"] = "120" }, (21, 106), (21.01, 106)),             // cao toc: mac dinh 1 chieu
            Way(3, new() { ["highway"] = "primary", ["maxspeed"] = "60", ["oneway"] = "-1" }, (20, 105), (20.001, 105.0002)),
            Way(4, new() { ["highway"] = "primary", ["maxspeed"] = "VN:urban" }, (20, 105), (20.1, 105)),         // khong phai so: bo
            Way(5, new() { ["highway"] = "primary", ["maxspeed"] = "50" }, (20, 105)),                            // 1 diem: bo
        ]);

        var (roads, skipped) = SpeedLimitMapper.Map(osm);

        Assert.Equal(2, skipped);
        Assert.Equal(3, roads.Count);
        Assert.False(roads.Single(r => r.Id == 1).Oneway);
        Assert.Equal("QL1A", roads.Single(r => r.Id == 1).Ref);
        Assert.True(roads.Single(r => r.Id == 2).Oneway);
        var reversed = roads.Single(r => r.Id == 3);
        Assert.True(reversed.Oneway);
        Assert.Equal((20.001, 105.0002), reversed.Points[0]);        // oneway=-1: dao lai theo chieu xe chay
    }

    [Fact]
    public void SpeedLimits_PointsJson() =>
        Assert.Equal("[[21.0285,105.8542],[21.03,105.86]]", SpeedLimitMapper.PointsJson([(21.0285, 105.8542), (21.03, 105.86)]));

    [Theory]
    [InlineData("IC11", "Trạm thu phí IC11")]
    [InlineData("Hầm Cù Mông", "Trạm thu phí Hầm Cù Mông")]
    [InlineData("Trạm Thu Phí Bắc Thăng Long", "Trạm Thu Phí Bắc Thăng Long")]
    [InlineData(null, "Trạm thu phí")]
    public void TollName_AddsPrefixWhenMissing(string? osmName, string expected) =>
        Assert.Equal(expected, OsmAlertMapper.TollName(osmName is null ? null : new() { ["name"] = osmName }));

    [Fact]
    public void Deduplicate_IsStable_AcrossInputOrder()
    {
        var items = new[]
        {
            new ImportedAlert("osm:node/7", AlertTypes.TollBooth, "Trạm thu phí", 21, 105, null, null),
            new ImportedAlert("osm:node/3", AlertTypes.TollBooth, "Trạm thu phí", 21, 105.0001, null, null),
        };
        var a = OsmAlertMapper.Deduplicate(items, 150, false).Single().ExternalId;
        var b = OsmAlertMapper.Deduplicate(items.Reverse(), 150, false).Single().ExternalId;
        Assert.Equal(a, b); // chay lai import khong doi dai dien -> khong them / an lung tung
    }

    [Fact]
    public void RailwayCrossings_DoubleTrackMerged_UnguardedNamed()
    {
        var osm = new OsmResponse([
            Node(1, 21.00000, 105.80000, new() { ["railway"] = "level_crossing" }),
            Node(2, 21.00010, 105.80000, new() { ["railway"] = "level_crossing", ["crossing:barrier"] = "no", ["name"] = "Km 12+300" }), // ray thu 2, ~11 m
            Node(3, 21.10000, 105.80000, new() { ["railway"] = "level_crossing", ["crossing:barrier"] = "full" }),
        ]);

        var result = OsmAlertMapper.Map(osm);
        Assert.Equal(2, result.Alerts.Count);
        Assert.Equal(1, result.Merged);
        Assert.All(result.Alerts, a => Assert.Equal(AlertTypes.RailwayCrossing, a.Type));
        // Diem gop giu ten "khong co rao chan" (thong tin quan trong hon), khong dung ten OSM "Km 12+300".
        Assert.Equal("Đường ngang không có rào chắn", result.Alerts.Single(a => a.Lat < 21.05).Name);
        Assert.Equal("Đường ngang đường sắt", result.Alerts.Single(a => a.Lat > 21.05).Name);
    }

    [Fact]
    public void SpeedBumps_RumbleStripSeriesMerged_IslandIgnored()
    {
        var osm = new OsmResponse([
            // Chuoi 4 vach son cach nhau ~20 m truoc nut giao + 1 go giam toc that o giua.
            Node(1, 21.00000, 105.80000, new() { ["traffic_calming"] = "rumble_strip" }),
            Node(2, 21.00018, 105.80000, new() { ["traffic_calming"] = "rumble_strip" }),
            Node(3, 21.00036, 105.80000, new() { ["traffic_calming"] = "bump" }),
            Node(4, 21.00054, 105.80000, new() { ["traffic_calming"] = "rumble_strip" }),
            Node(5, 21.02000, 105.80000, new() { ["traffic_calming"] = "rumble_strip" }),
            Node(6, 21.04000, 105.80000, new() { ["traffic_calming"] = "island" }),   // dao giao thong: khong phai go
        ]);

        var alerts = OsmAlertMapper.Map(osm).Alerts;
        Assert.Equal(2, alerts.Count);
        Assert.All(alerts, a => Assert.Equal(AlertTypes.SpeedBump, a.Type));
        Assert.Equal("Gờ giảm tốc", alerts.Single(a => a.Lat < 21.01).Name);    // go that duoc chon dai dien
        Assert.Equal("Vạch giảm tốc", alerts.Single(a => a.Lat > 21.01).Name);
    }

    [Fact]
    public void Hazard_GetsRoadAxis_NullAtJunction()
    {
        // Ngo chay Dong - Tay qua duong ngang (node 1); node 2 la nga tu cua 2 duong vuong goc.
        var osm = new OsmResponse([
            Node(1, 21.0, 105.8001, new() { ["railway"] = "level_crossing" }),
            Node(2, 21.0, 105.8002, new() { ["traffic_calming"] = "bump" }),
            new OsmElement("way", 10, null, null, new() { ["highway"] = "residential" }, null,
                Geometry: [new(21.0, 105.8), new(21.0, 105.8001), new(21.0, 105.8002)], Nodes: [100, 1, 2]),
            new OsmElement("way", 11, null, null, new() { ["highway"] = "residential" }, null,
                Geometry: [new(20.999, 105.8002), new(21.0, 105.8002), new(21.001, 105.8002)], Nodes: [200, 2, 201]),
        ]);

        var alerts = OsmAlertMapper.Map(osm).Alerts;
        Assert.Equal(90, alerts.Single(a => a.Type == AlertTypes.RailwayCrossing).Direction);
        Assert.Null(alerts.Single(a => a.Type == AlertTypes.SpeedBump).Direction);
    }

    [Fact]
    public void Schools_NodeWayRelation_Mapped_NearbyMerged()
    {
        var osm = new OsmResponse([
            Node(1, 21.00000, 105.80000, new() { ["amenity"] = "school", ["name"] = "Trường THCS A" }),
            // Way (vung) co diem giua cach ~60 m -> gop vao cum "khu vuc truong hoc".
            new OsmElement("way", 2, null, null, new() { ["amenity"] = "school", ["name"] = "Trường Tiểu học B" }, null,
                Center: new OsmCenter(21.00054, 105.80000)),
            new OsmElement("relation", 3, null, null, new() { ["amenity"] = "school" }, null,
                Center: new OsmCenter(21.10000, 105.80000)),  // xa 11 km -> truong khac
        ]);

        var result = OsmAlertMapper.Map(osm);
        var zones = result.Alerts.Where(a => a.Type == AlertTypes.SchoolZone).ToList();
        Assert.Equal(2, zones.Count);
        Assert.Equal(1, result.Merged);
        Assert.All(zones, z => Assert.Null(z.Direction));          // bao moi phia
        Assert.Contains(zones, z => z.Name == "Trường THCS A");    // cum gan giu ten truong co ten
        Assert.Contains(zones, z => z.Name == "Khu vực trường học"); // relation khong ten -> mac dinh
    }
}
