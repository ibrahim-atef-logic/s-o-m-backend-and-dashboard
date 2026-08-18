using System.Net;
using FluentAssertions;

namespace LogicRetail.Api.Tests;

public sealed class LineJobsEndpointTests : IClassFixture<MockApiFactory>
{
    private readonly MockApiFactory _factory;

    public LineJobsEndpointTests(MockApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Post_full_line_creates_line()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var item = $"ITEM-FULL-{Guid.NewGuid():N}"[..20];

        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/sales-orders/SO-000100/lines/full", new
        {
            company = "usmf",
            itemNumber = item,
            quantity = 2,
        });

        res.StatusCode.Should().Be(HttpStatusCode.Created);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("success").GetBoolean().Should().BeTrue();
        data.GetProperty("item").GetProperty("itemNumber").GetString().Should().Be(item);
        data.GetProperty("item").GetProperty("status").GetString().Should().Be("synced");
    }

    [Fact]
    public async Task Post_full_line_duplicate_returns_409_line_already_exists()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);

        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/sales-orders/SO-000100/lines/full", new
        {
            company = "usmf",
            itemNumber = "ITEM-100",
            quantity = 1,
        });

        res.StatusCode.Should().Be(HttpStatusCode.Conflict);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "LINE_ALREADY_EXISTS");
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        var error = doc.RootElement.GetProperty("error");
        error.GetProperty("itemNumber").GetString().Should().Be("ITEM-100");
        error.GetProperty("salesId").GetString().Should().Be("SO-000100");
        error.GetProperty("message").GetString().Should().Contain("ITEM-100");
        error.GetProperty("message").GetString().Should().Contain("SO-000100");
    }

    [Fact]
    public async Task Post_full_line_ifExists_add_updates_quantity()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/sales-orders/SO-000100/lines/full", new
        {
            company = "usmf",
            itemNumber = "ITEM-100",
            quantity = 2,
            ifExists = "add",
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("updated").GetBoolean().Should().BeTrue();
        data.GetProperty("quantity").GetInt32().Should().Be(4);
        data.GetProperty("itemNumber").GetString().Should().Be("ITEM-100");
    }

    [Fact]
    public async Task Post_full_line_invalid_qty_returns_400()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/sales-orders/SO-000100/lines/full", new
        {
            company = "usmf",
            itemNumber = "ITEM-NEW",
            quantity = 0,
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "INVALID_QTY");
    }

    [Fact]
    public async Task Post_full_line_forbidden_company()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/sales-orders/SO-000100/lines/full", new
        {
            company = "ussi",
            itemNumber = "ITEM-X",
            quantity = 1,
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "FORBIDDEN_COMPANY");
    }

    [Fact]
    public async Task Post_quick_line_creates_line()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        // Use a fresh SO mutation path: first clear conflict by using EMP001 + BC-100 only once per factory
        // Prefer unique approach: add ITEM via barcode that maps to ITEM-200; if already exists expect 422.
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/sales-orders/SO-000100/lines/quick", new
        {
            company = "usmf",
            lines = new[] { new { barcode = "BC-100", quantity = 1 } },
        });

        ((int)res.StatusCode).Should().BeOneOf(201, 422);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        doc.RootElement.GetProperty("data").TryGetProperty("jobId", out _).Should().BeTrue();
    }

    [Fact]
    public async Task Post_quick_line_empty_returns_validation_error()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/sales-orders/SO-000100/lines/quick", new
        {
            company = "usmf",
            lines = Array.Empty<object>(),
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Post_quick_line_max_10_enforced()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var lines = Enumerable.Range(0, 11).Select(i => new { barcode = $"BC-{i}", quantity = 1 }).ToArray();

        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/sales-orders/SO-000100/lines/quick", new
        {
            company = "usmf",
            lines,
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "MAX_LINES");
    }

    [Fact]
    public async Task Get_failed_lines_success()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);

        // Seed a failed line via duplicate full add
        await ApiTestClient.PostJsonAsync(client, "/api/v1/sales-orders/SO-000100/lines/full", new
        {
            company = "usmf",
            itemNumber = "ITEM-100",
            quantity = 1,
        });

        var res = await client.GetAsync("/api/v1/sales-orders/SO-000100/failed-lines?company=usmf");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        doc.RootElement.GetProperty("data").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Array);
    }

    [Fact]
    public async Task Line_jobs_require_auth()
    {
        var client = _factory.CreateClient();
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/sales-orders/SO-000100/lines/full", new
        {
            company = "usmf",
            itemNumber = "ITEM-X",
            quantity = 1,
        });
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
