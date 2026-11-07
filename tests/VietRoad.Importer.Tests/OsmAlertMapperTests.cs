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
}
