using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using VietRoad.Data;

namespace VietRoad.Api.Tests;

/// <summary>
/// Dung app voi moi truong "Testing":
///  - khong doc appsettings.Development.json / User Secrets -> khong co chuoi ket noi
///    -> canh bao dung InMemoryAlertService
///  - User Service dung database gia trong bo nho (EF InMemory), moi factory 1 database rieng
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
            services.AddDbContext<VietRoadDbContext>(o => o.UseInMemoryDatabase(_dbName)));
    }
}
