using System.Net;
using FluentAssertions;

namespace LogicRetail.Api.Tests;

public sealed class HealthEndpointTests : IClassFixture<MockApiFactory>
{
    private readonly MockApiFactory _factory;

    public HealthEndpointTests(MockApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_health_returns_ok_mock_mode()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/health");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        doc.RootElement.GetProperty("ok").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("dynamicsMode").GetString().Should().Be("mock");
        doc.RootElement.GetProperty("liveConfigured").GetBoolean().Should().BeFalse();
    }
}
