using System.Net;
using FluentAssertions;

namespace LogicRetail.Api.Tests;

public sealed class CatalogEndpointTests : IClassFixture<MockApiFactory>
{
    private readonly MockApiFactory _factory;

    public CatalogEndpointTests(MockApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Get_sales_orders_requires_auth()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/v1/sales-orders?company=usmf");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_sales_orders_lists_worker_orders()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await client.GetAsync("/api/v1/sales-orders?company=usmf");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        doc.RootElement.GetProperty("data").GetArrayLength().Should().BeGreaterThan(0);
        doc.RootElement.GetProperty("data")[0].GetProperty("salesId").GetString().Should().Be("SO-000100");
    }

    [Fact]
    public async Task Get_sales_orders_forbidden_for_other_company()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await client.GetAsync("/api/v1/sales-orders?company=ussi");

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "FORBIDDEN_COMPANY");
    }

    [Fact]
    public async Task Get_sales_orders_missing_company_returns_bad_request()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await client.GetAsync("/api/v1/sales-orders");

        // Non-nullable [FromQuery] company is required by ASP.NET model binding (ProblemDetails),
        // or AssertCompanyAccess returns VALIDATION_ERROR envelope.
        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        var body = await res.Content.ReadAsStringAsync();
        (body.Contains("VALIDATION_ERROR", StringComparison.Ordinal)
            || body.Contains("company", StringComparison.OrdinalIgnoreCase)).Should().BeTrue();
    }

    [Fact]
    public async Task Get_sales_order_by_id_success()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await client.GetAsync("/api/v1/sales-orders/SO-000100?company=usmf");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        doc.RootElement.GetProperty("data").GetProperty("salesId").GetString().Should().Be("SO-000100");
        doc.RootElement.GetProperty("data").GetProperty("custAccount").GetString().Should().Be("US-001");
    }

    [Fact]
    public async Task Get_sales_order_by_id_not_found()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await client.GetAsync("/api/v1/sales-orders/SO-MISSING?company=usmf");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "NOT_FOUND");
    }

    [Fact]
    public async Task Get_sales_order_lines_success()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await client.GetAsync("/api/v1/sales-orders/SO-000100/lines?company=usmf");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        doc.RootElement.GetProperty("data").GetArrayLength().Should().BeGreaterThan(0);
        doc.RootElement.GetProperty("data")[0].GetProperty("itemId").GetString().Should().Be("ITEM-100");
    }

    [Fact]
    public async Task Get_sales_order_lines_so_not_open()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await client.GetAsync("/api/v1/sales-orders/SO-MISSING/lines?company=usmf");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "SO_NOT_OPEN");
    }

    [Fact]
    public async Task Get_barcode_success()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await client.GetAsync("/api/v1/barcodes/BC-100?company=usmf");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        doc.RootElement.GetProperty("data").GetProperty("itemNumber").GetString().Should().Be("ITEM-200");
        doc.RootElement.GetProperty("data").GetProperty("barcode").GetString().Should().Be("BC-100");
    }

    [Fact]
    public async Task Get_barcode_not_found()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await client.GetAsync("/api/v1/barcodes/UNKNOWN?company=usmf");

        res.StatusCode.Should().Be(HttpStatusCode.NotFound);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "BARCODE_NOT_FOUND");
    }

    [Fact]
    public async Task Get_pricing_success()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await client.GetAsync("/api/v1/pricing?item=ITEM-200&company=usmf&custAccount=US-001");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        doc.RootElement.GetProperty("data").GetProperty("itemNumber").GetString().Should().Be("ITEM-200");
        doc.RootElement.GetProperty("data").GetProperty("price").GetDecimal().Should().Be(25.5m);
    }

    [Fact]
    public async Task Get_inventory_success()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await client.GetAsync("/api/v1/inventory?item=ITEM-200&warehouse=11&company=usmf");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        doc.RootElement.GetProperty("data").GetProperty("availableSalesQuantity").GetDecimal().Should().Be(100);
        doc.RootElement.GetProperty("data").GetProperty("warehouseId").GetString().Should().Be("11");
    }
}
