namespace LogicRetail.Domain;

public sealed class CompanyInfo
{
    public required string Code { get; init; }
    public required string Name { get; init; }
    public string? GroupId { get; init; }
}

public sealed class UserSession
{
    public required string PersonnelNumber { get; init; }
    public required long WorkerRecId { get; init; }
    public required string Name { get; init; }
    public required IReadOnlyList<CompanyInfo> Companies { get; init; }
}

public sealed class SalesOrderHeader
{
    public required string SalesId { get; init; }
    public required string CustAccount { get; init; }
    public required string SalesName { get; init; }
    public long WorkerSalesTaker { get; init; }
    public string? SalesStatus { get; init; }
    public string? DocumentStatus { get; init; }
    public required string DataArea { get; init; }
    public string? PriceGroupId { get; init; }
    public string? InventLocationId { get; init; }
    public string? InventSiteId { get; init; }
    public string? CreatedDateTime { get; init; }
}

public sealed class SalesOrderLine
{
    public long RecordId { get; init; }
    public required string SalesId { get; init; }
    public required string ItemId { get; init; }
    public string? ProductName { get; init; }
    public decimal SalesQty { get; init; }
    public string? SalesUnit { get; init; }
    public decimal LineNum { get; init; }
    public required string DataArea { get; init; }
}

public sealed class BarcodeItem
{
    public required string Barcode { get; init; }
    public required string ItemNumber { get; init; }
    public string? ProductName { get; init; }
    public string? ProductDescription { get; init; }
    public string? UnitId { get; init; }
    public required string DataArea { get; init; }
}

public sealed class PriceInfo
{
    public required string ItemNumber { get; init; }
    public decimal Price { get; init; }
    public string? UnitId { get; init; }
    public string? CustomerAccountNumber { get; init; }
    public string? PriceCustomerGroupCode { get; init; }
    public required string DataArea { get; init; }
}

public sealed class WarehouseOnHand
{
    public required string ItemNumber { get; init; }
    public required string WarehouseId { get; init; }
    public decimal AvailableSalesQuantity { get; init; }
    public decimal AvailableOnHandQuantity { get; init; }
    public string? Unit { get; init; }
    public string? ProductName { get; init; }
}
