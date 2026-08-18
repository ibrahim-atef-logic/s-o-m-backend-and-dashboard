using System.Text.Json;
using FluentAssertions;
using LogicRetail.Application.Common;
using LogicRetail.Application.Options;
using LogicRetail.Infrastructure.Persistence;
using LogicRetail.Infrastructure.Services;
using LogicRetail.Integrations.D365;
using Microsoft.Extensions.Options;

namespace LogicRetail.Unit.Tests;

public sealed class LineJobsServiceTests : IDisposable
{
    private readonly string _storePath = Path.Combine(Path.GetTempPath(), $"jobs-store-{Guid.NewGuid():N}.json");
    private readonly LineJobsService _sut;
    private const long WorkerRecId = 5637144578;
    private const string Company = "logic-trial";
    private const string SalesId = "SO-000200";

    public LineJobsServiceTests()
    {
        var store = new JsonFileStore(Options.Create(new StoreOptions { Path = _storePath }));
        _sut = new LineJobsService(new MockDynamicsClient(), store);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_storePath))
            {
                File.Delete(_storePath);
            }
        }
        catch
        {
            // ignore
        }
    }

    [Fact]
    public async Task SubmitFull_success_for_new_item()
    {
        var item = $"ITEM-U-{Guid.NewGuid():N}"[..16];
        var result = await _sut.SubmitFullAsync(SalesId, Company, WorkerRecId, item, 2, CancellationToken.None);
        var json = JsonSerializer.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("item").GetProperty("itemNumber").GetString().Should().Be(item);
    }

    [Fact]
    public async Task SubmitFull_duplicate_ITEM100_throws_LINE_ALREADY_EXISTS()
    {
        var act = () => _sut.SubmitFullAsync(SalesId, Company, WorkerRecId, "ITEM-100", 1, CancellationToken.None);
        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.Code.Should().Be("LINE_ALREADY_EXISTS");
        ex.Which.StatusCode.Should().Be(409);
        ex.Which.ItemNumber.Should().Be("ITEM-100");
        ex.Which.SalesId.Should().Be(SalesId);
    }

    [Fact]
    public async Task SubmitFull_ifExists_add_increases_quantity()
    {
        var result = await _sut.SubmitFullAsync(
            SalesId,
            Company,
            WorkerRecId,
            "ITEM-100",
            2,
            CancellationToken.None,
            "add");
        using var doc = JsonDocument.Parse(JsonSerializer.Serialize(result));
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("updated").GetBoolean().Should().BeTrue();
        doc.RootElement.GetProperty("quantity").GetInt32().Should().Be(3);
    }

    [Fact]
    public async Task SubmitFull_invalid_qty_throws()
    {
        var act = () => _sut.SubmitFullAsync(SalesId, Company, WorkerRecId, "ITEM-X", 0, CancellationToken.None);
        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.Code.Should().Be("INVALID_QTY");
    }

    [Fact]
    public async Task SubmitFull_unknown_so_throws_SO_NOT_OPEN()
    {
        var act = () => _sut.SubmitFullAsync("SO-MISSING", Company, WorkerRecId, "ITEM-X", 1, CancellationToken.None);
        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.Code.Should().Be("SO_NOT_OPEN");
    }

    [Fact]
    public async Task SubmitQuick_unknown_barcode_returns_partial_failure()
    {
        var result = await _sut.SubmitQuickAsync(
            SalesId,
            Company,
            WorkerRecId,
            new List<(string, int)> { ("UNKNOWN-BC", 1) },
            CancellationToken.None);
        var json = JsonSerializer.Serialize(result);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("isFailed").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task SubmitQuick_empty_lines_throws()
    {
        var act = () => _sut.SubmitQuickAsync(
            SalesId,
            Company,
            WorkerRecId,
            Array.Empty<(string, int)>(),
            CancellationToken.None);
        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.Code.Should().Be("VALIDATION_ERROR");
    }

    [Fact]
    public async Task SubmitQuick_over_max_lines_throws()
    {
        var lines = Enumerable.Range(0, 11).Select(i => ($"B{i}", 1)).ToList();
        var act = () => _sut.SubmitQuickAsync(SalesId, Company, WorkerRecId, lines, CancellationToken.None);
        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.Code.Should().Be("MAX_LINES");
    }

    [Fact]
    public async Task GetFailedLines_after_duplicate_full_contains_items()
    {
        var act = () => _sut.SubmitFullAsync(SalesId, Company, WorkerRecId, "ITEM-100", 1, CancellationToken.None);
        await act.Should().ThrowAsync<AppException>();
        var failed = _sut.GetFailedLines(SalesId, Company, "full");
        var json = JsonSerializer.Serialize(failed);
        using var doc = JsonDocument.Parse(json);
        doc.RootElement.GetArrayLength().Should().BeGreaterThan(0);
    }
}
