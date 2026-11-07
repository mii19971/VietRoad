using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace VietRoad.Api.Tests;

/// <summary>
/// Dung app voi moi truong "Testing": khong doc appsettings.Development.json,
/// nen khong co chuoi ket noi -> app tu dung InMemoryAlertService, khong can database.
/// </summary>
public class TestAppFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder) =>
        builder.UseEnvironment("Testing");
}
