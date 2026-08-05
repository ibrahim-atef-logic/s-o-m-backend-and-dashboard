using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace LogicRetail.Api.Tests;

/// <summary>
/// WebApplicationFactory locked to Dynamics Mock mode with an isolated JSON store.
/// </summary>
public sealed class MockApiFactory : WebApplicationFactory<Program>
{
    private readonly string _storePath =
        Path.Combine(Path.GetTempPath(), $"logic-retail-test-{Guid.NewGuid():N}.json");

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseSetting("Dynamics:Mode", "Mock");
        builder.UseSetting("Dynamics:FinOpsBaseUrl", "");
        builder.UseSetting("Dynamics:TenantId", "");
        builder.UseSetting("Dynamics:ClientId", "");
        builder.UseSetting("Dynamics:ClientSecret", "");
        builder.UseSetting("Store:Path", _storePath);
        builder.UseSetting("Jwt:Secret", "test-secret-logic-retail-min-32-chars!!");
        builder.UseEnvironment("Development");
    }

    protected override void Dispose(bool disposing)
    {
        base.Dispose(disposing);
        try
        {
            if (File.Exists(_storePath))
            {
                File.Delete(_storePath);
            }
        }
        catch
        {
            // best-effort cleanup
        }
    }
}
