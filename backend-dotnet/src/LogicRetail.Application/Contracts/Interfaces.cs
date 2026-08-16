using LogicRetail.Domain;

namespace LogicRetail.Application.Contracts;

public interface IDynamicsClient
{
    Task<IReadOnlyList<RetailUserRow>> GetUsersAsync(
        string? personnelNumber,
        string? password,
        string? company,
        bool activatedOnly,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesOrderHeader>> GetSalesOrderHeadersAsync(
        long? workerRecId,
        string? company,
        string? salesId,
        bool openOnly,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<SalesOrderLine>> GetSalesOrderLinesAsync(
        string salesId,
        string company,
        string? itemId = null,
        CancellationToken cancellationToken = default);

    Task<BarcodeItem?> GetBarcodeAsync(
        string code,
        string company,
        CancellationToken cancellationToken = default);

    Task<PriceInfo?> ResolvePriceAsync(
        string itemNumber,
        string dataArea,
        string? custAccount,
        string? priceGroupId,
        string? unitId = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<PriceInfo>> GetPriceAgreementsAsync(
        string itemNumber,
        string dataArea,
        string? priceGroup,
        string? unitId = null,
        CancellationToken cancellationToken = default);

    Task<WarehouseOnHand?> GetWarehouseOnHandAsync(
        string itemNumber,
        string warehouseId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<MobileWarehouse>> GetStandardWarehousesAsync(
        string dataAreaId,
        CancellationToken cancellationToken = default);

    Task CreateSalesOrderLineAsync(
        string dataAreaId,
        string salesOrderNumber,
        string itemNumber,
        int orderedSalesQuantity,
        CancellationToken cancellationToken = default);

    Task<MobileAuthPayload> AuthenticateUserAsync(
        string personnelNumber,
        string password,
        CancellationToken cancellationToken = default);

    Task<PasswordChangeResult> ChangePasswordAsync(
        string personnelNumber,
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken = default);
}

public sealed class RetailUserRow
{
    public required string PersonnelNumber { get; init; }
    public long HcmWorkerRecId { get; init; }
    public string? Name { get; init; }
    public required string GroupCompany { get; init; }
    public string? GroupCompanyName { get; init; }
    public string? GroupId { get; init; }
}

public interface IJsonStore
{
    void InsertRefreshToken(string id, string personnelNumber, string tokenHash, DateTimeOffset expiresAt);
    RefreshTokenRow? FindRefreshToken(string tokenHash);
    void DeleteRefreshToken(string tokenHash);
    void InsertJob(LineJobRow job);
    void UpdateJob(string id, string status, bool isFailed);
    void InsertJobItem(LineJobItemRow item);
    IReadOnlyList<LineJobRow> FindFailedJobs(string salesId, string company, string? mode);
    IReadOnlyList<LineJobItemRow> FindFailedItems(IEnumerable<string> jobIds);

    IReadOnlyList<CompanyCredentialRow> ListCompanies();
    CompanyCredentialRow? FindCompany(string code);
    void UpsertCompany(CompanyCredentialRow company);
    bool DeleteCompany(string code);
}

public sealed class CompanyCredentialRow
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public required string TenantId { get; init; }
    public required string ClientId { get; init; }
    public required string ClientSecret { get; init; }
    public required string FinOpsBaseUrl { get; init; }
    public bool IsActive { get; init; } = true;
    public DateTimeOffset UpdatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class RefreshTokenRow
{
    public required string Id { get; init; }
    public required string PersonnelNumber { get; init; }
    public required string TokenHash { get; init; }
    public required DateTimeOffset ExpiresAt { get; init; }
}

public sealed class LineJobRow
{
    public required string Id { get; init; }
    public required string SalesId { get; init; }
    public required string Company { get; init; }
    public long WorkerRecId { get; init; }
    public required string Mode { get; init; }
    public required string Status { get; init; }
    public bool IsFailed { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}

public sealed class LineJobItemRow
{
    public required string Id { get; init; }
    public required string JobId { get; init; }
    public string? Barcode { get; init; }
    public string? ItemNumber { get; init; }
    public decimal Quantity { get; init; }
    public required string Status { get; init; }
    public string? CommentAr { get; init; }
    public string? CommentEn { get; init; }
    public DateTimeOffset CreatedAt { get; init; } = DateTimeOffset.UtcNow;
}
