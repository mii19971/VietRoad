using VietRoad.Api.Utils;

namespace VietRoad.Api.Tests;

public class GeoUtilsTests
{
    [Fact]
    public void DistanceMeters_HaNoiToHcm_IsAbout1140Km()
    {
        var d = GeoUtils.DistanceMeters(21.0285, 105.8542, 10.7769, 106.7009);
        Assert.InRange(d, 1_130_000, 1_150_000);
    }

    [Fact]
    public void BoundingBox_ContainsPointsWithinRadius()
    {
        // Diem cach Ho Guom ~1421 m (den do Tran Hung Dao) phai nam trong hop ban kinh 1500 m.
        var box = GeoUtils.BoundingBox(21.0285, 105.8542, 1500);
        Assert.InRange(21.0245, box.MinLat, box.MaxLat);
        Assert.InRange(105.8412, box.MinLng, box.MaxLng);

        // Camera TP.HCM thi khong.
        Assert.False(10.7769 >= box.MinLat);
    }

    [Theory]
    [InlineData(21.0, 105.8, true)]
    [InlineData(91, 105.8, false)]
    [InlineData(21.0, -181, false)]
    public void IsValidCoord_Works(double lat, double lng, bool expected) =>
        Assert.Equal(expected, GeoUtils.IsValidCoord(lat, lng));
}
