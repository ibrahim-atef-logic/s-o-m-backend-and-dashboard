using System.Text.Json;
using LogicRetail.Application.Contracts;
using LogicRetail.Application.Options;
using Microsoft.Extensions.Options;

namespace LogicRetail.Infrastructure.Persistence;

public sealed class JsonFileStore : IJsonStore
{
    private readonly string _path;
    private readonly object _gate = new();
    private StoreData _data;

    public JsonFileStore(IOptions<StoreOptions> options)
    {
        _path = Path.GetFullPath(options.Value.Path);
        var dir = Path.GetDirectoryName(_path);
        if (!string.IsNullOrEmpty(dir))
        {
            Directory.CreateDirectory(dir);
        }

        _data = Load();
        EnsureSeedCompanies();
    }

    public void InsertRefreshToken(string id, string personnelNumber, string tokenHash, DateTimeOffset expiresAt)
    {
        lock (_gate)
        {
            _data.RefreshTokens.Add(new RefreshTokenRow
            {
                Id = id,
                PersonnelNumber = personnelNumber,
                TokenHash = tokenHash,
                ExpiresAt = expiresAt,
            });
            Save();
        }
    }

    public RefreshTokenRow? FindRefreshToken(string tokenHash)
    {
        lock (_gate)
        {
            return _data.RefreshTokens.FirstOrDefault(t => t.TokenHash == tokenHash);
        }
    }

    public void DeleteRefreshToken(string tokenHash)
    {
        lock (_gate)
        {
            _data.RefreshTokens.RemoveAll(t => t.TokenHash == tokenHash);
            Save();
        }
    }

    public void InsertJob(LineJobRow job)
    {
        lock (_gate)
        {
            _data.LineJobs.Add(job);
            Save();
        }
    }

    public void UpdateJob(string id, string status, bool isFailed)
    {
        lock (_gate)
        {
            var job = _data.LineJobs.FirstOrDefault(j => j.Id == id);
            if (job is null)
            {
                return;
            }

            var idx = _data.LineJobs.IndexOf(job);
            _data.LineJobs[idx] = new LineJobRow
            {
                Id = job.Id,
                SalesId = job.SalesId,
                Company = job.Company,
                WorkerRecId = job.WorkerRecId,
                Mode = job.Mode,
                Status = status,
                IsFailed = isFailed,
                CreatedAt = job.CreatedAt,
            };
            Save();
        }
    }

    public void InsertJobItem(LineJobItemRow item)
    {
        lock (_gate)
        {
            _data.LineJobItems.Add(item);
            Save();
        }
    }

    public IReadOnlyList<LineJobRow> FindFailedJobs(string salesId, string company, string? mode)
    {
        lock (_gate)
        {
            return _data.LineJobs
                .Where(j => j.SalesId == salesId
                    && string.Equals(j.Company, company, StringComparison.OrdinalIgnoreCase)
                    && j.IsFailed
                    && (mode is null || j.Mode == mode))
                .ToList();
        }
    }

    public IReadOnlyList<LineJobItemRow> FindFailedItems(IEnumerable<string> jobIds)
    {
        var set = jobIds.ToHashSet();
        lock (_gate)
        {
            return _data.LineJobItems
                .Where(i => set.Contains(i.JobId) && i.Status == "failed")
                .ToList();
        }
    }

    public IReadOnlyList<CompanyCredentialRow> ListCompanies()
    {
        lock (_gate)
        {
            return _data.Companies
                .OrderBy(c => c.Code, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }
    }

    public CompanyCredentialRow? FindCompany(string code)
    {
        lock (_gate)
        {
            return _data.Companies.FirstOrDefault(c =>
                string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
        }
    }

    public void UpsertCompany(CompanyCredentialRow company)
    {
        lock (_gate)
        {
            var idx = _data.Companies.FindIndex(c =>
                string.Equals(c.Code, company.Code, StringComparison.OrdinalIgnoreCase));
            if (idx >= 0)
            {
                _data.Companies[idx] = company;
            }
            else
            {
                _data.Companies.Add(company);
            }

            Save();
        }
    }

    public bool DeleteCompany(string code)
    {
        lock (_gate)
        {
            var removed = _data.Companies.RemoveAll(c =>
                string.Equals(c.Code, code, StringComparison.OrdinalIgnoreCase));
            if (removed > 0)
            {
                Save();
            }

            return removed > 0;
        }
    }

    private void EnsureSeedCompanies()
    {
        lock (_gate)
        {
            if (_data.Companies.Count > 0)
            {
                return;
            }

            // Seed registry codes for local Mock only — put real secrets via dashboard/env.
            _data.Companies.AddRange(
            [
                new CompanyCredentialRow
                {
                    Code = "logic-trial",
                    Name = "Logic Trial",
                    TenantId = "REPLACE_ME_TENANT_ID",
                    ClientId = "REPLACE_ME_CLIENT_ID",
                    ClientSecret = "REPLACE_ME_CLIENT_SECRET",
                    FinOpsBaseUrl = "https://YOUR_ENV.operations.dynamics.com",
                    IsActive = true,
                },
                new CompanyCredentialRow
                {
                    Code = "usmf",
                    Name = "USMF Contoso",
                    TenantId = "REPLACE_ME_TENANT_ID",
                    ClientId = "REPLACE_ME_CLIENT_ID",
                    ClientSecret = "REPLACE_ME_CLIENT_SECRET",
                    FinOpsBaseUrl = "https://YOUR_ENV.operations.dynamics.com",
                    IsActive = true,
                },
            ]);
            Save();
        }
    }

    private StoreData Load()
    {
        if (!File.Exists(_path))
        {
            return new StoreData();
        }

        var json = File.ReadAllText(_path);
        return JsonSerializer.Deserialize<StoreData>(json) ?? new StoreData();
    }

    private void Save()
    {
        var json = JsonSerializer.Serialize(_data, new JsonSerializerOptions { WriteIndented = true });
        File.WriteAllText(_path, json);
    }

    private sealed class StoreData
    {
        public List<RefreshTokenRow> RefreshTokens { get; set; } = [];
        public List<LineJobRow> LineJobs { get; set; } = [];
        public List<LineJobItemRow> LineJobItems { get; set; } = [];
        public List<CompanyCredentialRow> Companies { get; set; } = [];
    }
}
