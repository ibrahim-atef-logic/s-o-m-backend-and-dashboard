using FluentAssertions;
using LogicRetail.Application.Contracts;
using LogicRetail.Application.Options;
using LogicRetail.Infrastructure.Persistence;
using Microsoft.Extensions.Options;

namespace LogicRetail.Unit.Tests;

public sealed class JsonFileStoreTests : IDisposable
{
    private readonly string _path = Path.Combine(Path.GetTempPath(), $"store-{Guid.NewGuid():N}.json");
    private readonly JsonFileStore _sut;

    public JsonFileStoreTests()
    {
        _sut = new JsonFileStore(Options.Create(new StoreOptions { Path = _path }));
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_path))
            {
                File.Delete(_path);
            }
        }
        catch
        {
            // ignore
        }
    }

    [Fact]
    public void Refresh_token_insert_find_delete()
    {
        _sut.InsertRefreshToken("id1", "EMP001", "hash-abc", DateTimeOffset.UtcNow.AddDays(1));
        _sut.FindRefreshToken("hash-abc").Should().NotBeNull();
        _sut.DeleteRefreshToken("hash-abc");
        _sut.FindRefreshToken("hash-abc").Should().BeNull();
    }

    [Fact]
    public void Line_job_and_failed_items_roundtrip()
    {
        var jobId = Guid.NewGuid().ToString();
        _sut.InsertJob(new LineJobRow
        {
            Id = jobId,
            SalesId = "SO-1",
            Company = "logic-trial",
            WorkerRecId = 1,
            Mode = "full",
            Status = "completed",
            IsFailed = true,
        });
        _sut.InsertJobItem(new LineJobItemRow
        {
            Id = Guid.NewGuid().ToString(),
            JobId = jobId,
            ItemNumber = "ITEM-100",
            Quantity = 1,
            Status = "failed",
            CommentEn = "exists",
            CommentAr = "موجود",
        });

        var jobs = _sut.FindFailedJobs("SO-1", "logic-trial", "full");
        jobs.Should().ContainSingle(j => j.Id == jobId);
        var items = _sut.FindFailedItems(jobs.Select(j => j.Id));
        items.Should().ContainSingle(i => i.ItemNumber == "ITEM-100");
    }
}
