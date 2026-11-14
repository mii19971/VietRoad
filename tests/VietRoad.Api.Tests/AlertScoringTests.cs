using VietRoad.Data;

namespace VietRoad.Api.Tests;

/// <summary>Kiem tra giam do tin cay theo thoi gian (du lieu luon moi): ham thuan, khong can database.</summary>
public class AlertScoringTests
{
    private static readonly DateTimeOffset T0 = new(2026, 1, 1, 0, 0, 0, TimeSpan.Zero);

    private static AlertEntity UserReport(DateTimeOffset lastConfirmed, int confirm = 1, int deny = 0, bool verified = false) =>
        new()
        {
            Type = AlertTypes.SpeedCamera, Name = "Camera", Source = AlertSources.User,
            ConfirmCount = confirm, DenyCount = deny,
            CreatedAt = lastConfirmed, LastConfirmedAt = lastConfirmed,
            VerifiedAt = verified ? lastConfirmed : null,
        };

    [Fact]
    public void FreshUserReport_WithinGrace_NotDecayed()
    {
        var a = UserReport(T0); // goc 30 + 1 xac nhan = 40
        Assert.Equal(40, AlertScoring.Effective(a, T0));
        Assert.Equal(40, AlertScoring.Effective(a, T0.AddDays(AlertScoring.DecayGraceDays))); // het an han: chua tru
        Assert.True(AlertScoring.IsVisible(a, T0.AddDays(AlertScoring.DecayGraceDays)));
    }

    [Fact]
    public void UserReport_DecaysAfterGrace_ThenHidden()
    {
        var a = UserReport(T0); // 40 diem, 1 nguoi bao
        // Sau an han (3 ngay) + 5 ngay, tru 5 * 2 = 10 -> con 30.
        Assert.Equal(30, AlertScoring.Effective(a, T0.AddDays(8)));
        // Tru du 20 diem (xuong < nguong 20) -> an. 40 -> 20 can 10 ngay sau an han = ngay 13.
        Assert.True(AlertScoring.IsVisible(a, T0.AddDays(12)));
        Assert.False(AlertScoring.IsVisible(a, T0.AddDays(14)));
    }

    [Fact]
    public void MoreConfirmations_PersistLonger()
    {
        var one = UserReport(T0, confirm: 1);               // 40
        var many = UserReport(T0, confirm: 3);              // 30 + 30 = 60
        // Cung mot thoi diem rat lau sau: diem nhieu nguoi xac nhan van con hien, diem 1 nguoi thi mat.
        Assert.False(AlertScoring.IsVisible(one, T0.AddDays(20)));
        Assert.True(AlertScoring.IsVisible(many, T0.AddDays(20)));
    }

    [Fact]
    public void Confirmation_ResetsDecayClock()
    {
        var stale = UserReport(T0);                          // suyt bi an
        Assert.False(AlertScoring.IsVisible(stale, T0.AddDays(20)));
        // Co nguoi xac nhan lai o ngay 20 -> LastConfirmedAt moi, dong ho giam dan tinh lai tu dau.
        var reconfirmed = UserReport(T0.AddDays(20), confirm: 2);
        Assert.True(AlertScoring.IsVisible(reconfirmed, T0.AddDays(21)));
    }

    [Theory]
    [InlineData(AlertSources.Osm)]
    [InlineData(AlertSources.Admin)]
    [InlineData(AlertSources.Partner)]
    public void NonUserSources_DoNotDecay(string source)
    {
        var a = UserReport(T0);
        a.Source = source;
        // Du rat lau khong ai xac nhan, du lieu co nguoi quan ly khong tu ha.
        Assert.Equal(AlertScoring.Compute(a), AlertScoring.Effective(a, T0.AddDays(365)));
    }

    [Fact]
    public void AdminVerifiedUserReport_DoesNotDecay()
    {
        var a = UserReport(T0, verified: true);
        Assert.Equal(AlertScoring.Compute(a), AlertScoring.Effective(a, T0.AddDays(365)));
    }
}
