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

    public string? UserId { get; init; }
    public long ActivationRecId { get; init; }
    public bool IsActive { get; init; } = true;
    public bool UserInfoEnable { get; init; } = true;
    public long RetailChannelTableRecId { get; init; }
    public string? RetailChannelId { get; init; }
    public int ChannelType { get; init; }
    public string? InventLocation { get; init; }
    public string? InventLocationDataAreaId { get; init; }
    public string? Currency { get; init; }
    public string? DefaultCustAccount { get; init; }
    public string? DefaultCustDataAreaId { get; init; }
    public string ActiveCompany { get; init; } = string.Empty;
    public string? ActiveWarehouse { get; init; }
    public bool NeedsWarehouseSelection { get; init; }
}

/// <summary>Payload from D365 AuthenticateUser (after inner JSON deserialize).</summary>
public sealed class MobileAuthPayload
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public long ActivationRecId { get; init; }
    public long HcmWorkerRecId { get; init; }
    public string PersonnelNumber { get; init; } = string.Empty;
    public string? UserId { get; init; }
    public string? WorkerName { get; init; }
    public string? Company { get; init; }
    public bool IsActive { get; init; }
    public bool UserInfoEnable { get; init; }
    public long RetailChannelTableRecId { get; init; }
    public string? RetailChannelId { get; init; }
    public int ChannelType { get; init; }
    public string? InventLocation { get; init; }
    public string? InventLocationDataAreaId { get; init; }
    public string? Currency { get; init; }
    public string? DefaultCustAccount { get; init; }
    public string? DefaultCustDataAreaId { get; init; }

    public string ActiveCompany
    {
        get
        {
            if (!string.IsNullOrWhiteSpace(InventLocationDataAreaId))
            {
                return InventLocationDataAreaId.Trim();
            }

            return (Company ?? string.Empty).Trim();
        }
    }

    public string? ActiveWarehouse =>
        string.IsNullOrWhiteSpace(InventLocation) ? null : InventLocation.Trim();

    public bool NeedsWarehouseSelection => string.IsNullOrWhiteSpace(ActiveWarehouse);
}

public sealed class PasswordChangeResult
{
    public bool IsSuccess { get; init; }
    public string Message { get; init; } = string.Empty;
    public long ActivationRecId { get; init; }
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
    public string? InventoryLotId { get; init; }
}

public sealed class UpdatedSalesOrderLine
{
    public required string SalesOrderNumber { get; init; }
    public required string ItemNumber { get; init; }
    public decimal Quantity { get; init; }
    public string? InventoryLotId { get; init; }
    public long RecordId { get; init; }
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

public sealed class MobileWarehouse
{
    public required string DataAreaId { get; init; }
    public required string InventLocationId { get; init; }
    public required string Name { get; init; }
    public string? InventSiteId { get; init; }
    public required string InventLocationType { get; init; }
}

public sealed class MobileCustomer
{
    public required string DataAreaId { get; init; }
    public required string CustomerAccount { get; init; }
    public required string Name { get; init; }
    public string? CustomerGroupId { get; init; }
    public string? SalesCurrencyCode { get; init; }
    public string? PrimaryPhone { get; init; }
    public string? AddressCity { get; init; }
}

public sealed class CreatedSalesOrder
{
    public required string DataAreaId { get; init; }
    public required string SalesOrderNumber { get; init; }
    public required string CustomerAccount { get; init; }
    public string? WarehouseId { get; init; }
    public string? SiteId { get; init; }
    public string? CurrencyCode { get; init; }
    public string? OrderTakerPersonnelNumber { get; init; }
}
