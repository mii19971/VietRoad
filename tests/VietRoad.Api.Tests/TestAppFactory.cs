using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using VietRoad.Api.Services;
using VietRoad.Data;

namespace VietRoad.Api.Tests;

/// <summary>
/// Dung app voi moi truong "Testing":
///  - khong doc appsettings.Development.json / User Secrets -> khong co chuoi ket noi
///  - dung database gia trong bo nho (EF InMemory), moi factory 1 database rieng,
///    co san 4 canh bao mau; tim canh bao di qua DbAlertService giong khi chay that
///  - khoa ky JWT rieng cho test
/// </summary>
public class TestAppFactory : WebApplicationFactory<Program>
{
    private readonly string _dbName = $"vietroad-tests-{Guid.NewGuid()}";

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("Jwt:SigningKey", "test-signing-key-chi-dung-trong-test-0123456789");

        builder.ConfigureTestServices(services =>
        {
            services.AddDbContext<VietRoadDbContext>(o => o.UseInMemoryDatabase(_dbName));
            // Dang ky sau cung -> thay cho InMemoryAlertService ma Program.cs da dang ky.
            services.AddScoped<IAlertService, DbAlertService>();
        });
    }

    // Chay 1 lan khi app test khoi dong: chen du lieu mau.
    protected override IHost CreateHost(IHostBuilder builder)
    {
        var host = base.CreateHost(builder);

        using var scope = host.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<VietRoadDbContext>();
        db.Alerts.AddRange(
            Sample(1, AlertTypes.SpeedCamera, 21.0285, 105.8542, "Camera Ho Hoan Kiem"),
            Sample(2, AlertTypes.SpeedCamera, 21.0368, 105.8345, "Camera Kim Ma"),
            Sample(3, AlertTypes.RedLightCamera, 21.0245, 105.8412, "Den do Tran Hung Dao"),
            Sample(4, AlertTypes.SpeedCamera, 10.7769, 106.7009, "Camera Q1 TP.HCM"));
        db.SaveChanges();

        return host;
    }

    private static AlertEntity Sample(int id, string type, double lat, double lng, string name) => new()
    {
        Id = id, Type = type, Lat = lat, Lng = lng, Name = name,
        Source = AlertSources.Osm,
        Confidence = AlertScoring.BaseConfidence(AlertSources.Osm),
        CreatedAt = DateTimeOffset.UtcNow, UpdatedAt = DateTimeOffset.UtcNow,
    };
}
