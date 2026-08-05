using System.Net;
using FluentAssertions;

namespace LogicRetail.Api.Tests;

/// <summary>
/// End-to-end mobile cycle covering the primary happy path across endpoints.
/// </summary>
public sealed class ApiCycleTests : IClassFixture<MockApiFactory>
{
    private readonly MockApiFactory _factory;

    public ApiCycleTests(MockApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Full_mobile_cycle_mock()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);

        var orders = await client.GetAsync("/api/v1/sales-orders?company=usmf");
        orders.StatusCode.Should().Be(HttpStatusCode.OK);

        var order = await client.GetAsync("/api/v1/sales-orders/SO-000100?company=usmf");
        order.StatusCode.Should().Be(HttpStatusCode.OK);

        var lines = await client.GetAsync("/api/v1/sales-orders/SO-000100/lines?company=usmf");
        lines.StatusCode.Should().Be(HttpStatusCode.OK);

        var barcode = await client.GetAsync("/api/v1/barcodes/123456?company=usmf");
        barcode.StatusCode.Should().Be(HttpStatusCode.OK);

        var pricing = await client.GetAsync("/api/v1/pricing?item=ITEM-200&company=usmf");
        pricing.StatusCode.Should().Be(HttpStatusCode.OK);

        var inventory = await client.GetAsync("/api/v1/inventory?item=ITEM-200&warehouse=11&company=usmf");
        inventory.StatusCode.Should().Be(HttpStatusCode.OK);

        var item = $"ITEM-CYCLE-{Guid.NewGuid():N}"[..22];
        var full = await ApiTestClient.PostJsonAsync(client, "/api/v1/sales-orders/SO-000100/lines/full", new
        {
            company = "usmf",
            itemNumber = item,
            quantity = 1,
        });
        full.StatusCode.Should().Be(HttpStatusCode.Created);

        var quick = await ApiTestClient.PostJsonAsync(client, "/api/v1/sales-orders/SO-000100/lines/quick", new
        {
            company = "usmf",
            lines = new[] { new { barcode = "UNKNOWN-BC", quantity = 1 } },
        });
        // Unknown barcode still creates a job (partial failure → 422 or success envelope with failed items)
        ((int)quick.StatusCode).Should().BeOneOf(201, 422);

        var failed = await client.GetAsync("/api/v1/sales-orders/SO-000100/failed-lines?company=usmf");
        failed.StatusCode.Should().Be(HttpStatusCode.OK);

        var me = await client.GetAsync("/api/v1/auth/me");
        me.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}
