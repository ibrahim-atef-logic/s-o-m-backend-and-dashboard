using System.Globalization;
using System.Text.Json;
using LogicRetail.Application.Common;
using LogicRetail.Application.Contracts;
using LogicRetail.Domain;

namespace LogicRetail.Integrations.D365;

public sealed class LiveDynamicsClient : IDynamicsClient
{
    private readonly D365ODataClient _odata;

    public LiveDynamicsClient(D365ODataClient odata) => _odata = odata;

    public async Task<IReadOnlyList<RetailUserRow>> GetUsersAsync(
        string? personnelNumber,
        string? password,
        string? company,
        bool activatedOnly,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parts = new List<string>();
            if (!string.IsNullOrWhiteSpace(personnelNumber))
            {
                parts.Add($"PersonnelNumber eq '{ODataEscaper.String(personnelNumber)}'");
            }

            if (password is not null)
            {
                parts.Add($"Password eq '{ODataEscaper.String(password)}'");
            }

            if (activatedOnly)
            {
                parts.Add("IsActivated eq Microsoft.Dynamics.DataEntities.NoYes'Yes'");
            }

            // Docs / Power App login: PersonnelNumber + Password + IsActivated only.
            // Do not filter GroupCompany in OData — D365 often stores UPPERCASE legal entities
            // while mobile sends lowercase (e.g. logic-trial vs LOGIC-TRIAL).
            var rows = await _odata.QueryAsync(
                "LogicRetailUserSetup_BI",
                string.Join(" and ", parts),
                cancellationToken,
                crossCompany: true);

            IEnumerable<RetailUserRow> mapped = rows.Select(MapUser);
            if (!string.IsNullOrWhiteSpace(company))
            {
                mapped = mapped.Where(u =>
                    string.Equals(u.GroupCompany, company, StringComparison.OrdinalIgnoreCase));
            }

            return mapped.ToList();
        }
        catch (D365ODataException ex)
        {
            throw MapDynamicsError(ex);
        }
    }

    public async Task<IReadOnlyList<SalesOrderHeader>> GetSalesOrderHeadersAsync(
        long? workerRecId,
        string? company,
        string? salesId,
        bool openOnly,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parts = new List<string>();
            if (workerRecId is not null)
            {
                parts.Add($"WorkerSalesTaker eq {workerRecId.Value}");
            }

            if (!string.IsNullOrWhiteSpace(company))
            {
                parts.Add($"DataArea eq '{ODataEscaper.String(company)}'");
            }

            if (!string.IsNullOrWhiteSpace(salesId))
            {
                parts.Add($"SalesId eq '{ODataEscaper.String(salesId)}'");
            }

            if (openOnly)
            {
                parts.Add("SalesStatus eq Microsoft.Dynamics.DataEntities.SalesStatus'Backorder'");
                parts.Add("DocumentStatus eq Microsoft.Dynamics.DataEntities.DocumentStatus'None'");
            }

            var rows = await _odata.QueryAsync(
                "LogicRetailSalesOrdersHeaders_BI",
                string.Join(" and ", parts),
                cancellationToken);
            return rows.Select(MapHeader).ToList();
        }
        catch (D365ODataException ex)
        {
            throw MapDynamicsError(ex);
        }
    }

    public async Task<IReadOnlyList<SalesOrderLine>> GetSalesOrderLinesAsync(
        string salesId,
        string company,
        string? itemId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var parts = new List<string>
            {
                $"SalesId eq '{ODataEscaper.String(salesId)}'",
                $"DataArea eq '{ODataEscaper.String(company)}'",
            };
            if (!string.IsNullOrWhiteSpace(itemId))
            {
                parts.Add($"ItemId eq '{ODataEscaper.String(itemId)}'");
            }

            var rows = await _odata.QueryAsync(
                "LogicRetailSalesOrdersLines_BI",
                string.Join(" and ", parts),
                cancellationToken);
            return rows.Select(MapLine).ToList();
        }
        catch (D365ODataException ex)
        {
            throw MapDynamicsError(ex);
        }
    }

    public async Task<BarcodeItem?> GetBarcodeAsync(
        string code,
        string company,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter =
                $"ItemBarCode eq '{ODataEscaper.String(code)}' and DataArea eq '{ODataEscaper.String(company)}'";
            var rows = await _odata.QueryAsync("LogicRetailItemBarcodes_BI", filter, cancellationToken);
            return rows.Count == 0 ? null : MapBarcode(rows[0]);
        }
        catch (D365ODataException ex)
        {
            throw MapDynamicsError(ex);
        }
    }

    public async Task<PriceInfo?> ResolvePriceAsync(
        string itemNumber,
        string dataArea,
        string? custAccount,
        string? priceGroupId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            if (!string.IsNullOrWhiteSpace(custAccount))
            {
                var byCustomer = await GetPriceAgreementsAsync(
                    itemNumber,
                    dataArea,
                    null,
                    custAccount,
                    cancellationToken);
                if (byCustomer.Count > 0)
                {
                    return byCustomer[0];
                }
            }

            if (!string.IsNullOrWhiteSpace(priceGroupId))
            {
                var byGroup = await GetPriceAgreementsAsync(
                    itemNumber,
                    dataArea,
                    priceGroupId,
                    string.Empty,
                    cancellationToken);
                if (byGroup.Count > 0)
                {
                    return byGroup[0];
                }
            }

            var general = await GetPriceAgreementsAsync(
                itemNumber,
                dataArea,
                string.Empty,
                string.Empty,
                cancellationToken);
            return general.Count == 0 ? null : general[0];
        }
        catch (D365ODataException ex)
        {
            throw MapDynamicsError(ex);
        }
    }

    public Task<IReadOnlyList<PriceInfo>> GetPriceAgreementsAsync(
        string itemNumber,
        string dataArea,
        string? priceGroup,
        CancellationToken cancellationToken = default) =>
        GetPriceAgreementsAsync(itemNumber, dataArea, priceGroup, null, cancellationToken);

    private async Task<IReadOnlyList<PriceInfo>> GetPriceAgreementsAsync(
        string itemNumber,
        string dataArea,
        string? priceGroup,
        string? customerAccount,
        CancellationToken cancellationToken)
    {
        var parts = new List<string>
        {
            $"ItemRelation eq '{ODataEscaper.String(itemNumber)}'",
            $"DataAreaId eq '{ODataEscaper.String(dataArea)}'",
        };
        if (priceGroup is not null)
        {
            parts.Add($"PriceCustomerGroupCode eq '{ODataEscaper.String(priceGroup)}'");
        }

        if (customerAccount is not null)
        {
            parts.Add($"CustomerAccountNumber eq '{ODataEscaper.String(customerAccount)}'");
        }

        var rows = await _odata.QueryAsync(
            "LogicRetailSalesPriceAgreements_BI",
            string.Join(" and ", parts),
            cancellationToken);
        return rows.Select(MapPrice).ToList();
    }

    public async Task<WarehouseOnHand?> GetWarehouseOnHandAsync(
        string itemNumber,
        string warehouseId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter =
                $"ItemNumber eq '{ODataEscaper.String(itemNumber)}' and InventLocationId eq '{ODataEscaper.String(warehouseId)}'";
            var rows = await _odata.QueryAsync("LogicRetailWarehouseOnHand_BI", filter, cancellationToken);
            return rows.Count == 0 ? null : MapOnHand(rows[0]);
        }
        catch (D365ODataException ex)
        {
            throw MapDynamicsError(ex);
        }
    }

    public async Task CreateSalesOrderLineAsync(
        string dataAreaId,
        string salesOrderNumber,
        string itemNumber,
        int orderedSalesQuantity,
        CancellationToken cancellationToken = default)
    {
        try
        {
            await _odata.PostAsync(
                "SalesOrderLines",
                new
                {
                    dataAreaId,
                    SalesOrderNumber = salesOrderNumber,
                    ItemNumber = itemNumber,
                    OrderedSalesQuantity = orderedSalesQuantity,
                },
                cancellationToken);
        }
        catch (D365ODataException ex)
        {
            throw MapDynamicsError(ex);
        }
    }

    private static AppException MapDynamicsError(D365ODataException ex)
    {
        if (ex.StatusCode is 503 or 502)
        {
            return new AppException(
                "Dynamics environment is unavailable (503). The trial sandbox may be sleeping — open it in LCS / browser and retry.",
                503,
                "DYNAMICS_UNAVAILABLE");
        }

        if (ex.StatusCode is 401 or 403)
        {
            return new AppException(
                "Dynamics rejected the Azure app token. Check app permissions and admin consent.",
                ex.StatusCode,
                "DYNAMICS_FORBIDDEN");
        }

        if (ex.StatusCode == 429)
        {
            return new AppException("Dynamics rate limit exceeded. Retry shortly.", 429, "DYNAMICS_THROTTLED");
        }

        return new AppException(ex.Message, ex.StatusCode >= 400 ? ex.StatusCode : 502, "DYNAMICS_ERROR");
    }

    private static RetailUserRow MapUser(JsonElement e) => new()
    {
        PersonnelNumber = GetString(e, "PersonnelNumber") ?? string.Empty,
        HcmWorkerRecId = GetLong(e, "HcmWorkerRecId"),
        Name = GetString(e, "DirPersonBaseEntity_Name"),
        GroupCompany = GetString(e, "GroupCompany") ?? string.Empty,
        GroupCompanyName = GetString(e, "GroupCompanyName"),
        GroupId = GetString(e, "GroupId"),
    };

    private static SalesOrderHeader MapHeader(JsonElement e) => new()
    {
        SalesId = GetString(e, "SalesId") ?? string.Empty,
        CustAccount = GetString(e, "CustAccount") ?? string.Empty,
        SalesName = GetString(e, "SalesName") ?? string.Empty,
        WorkerSalesTaker = GetLong(e, "WorkerSalesTaker"),
        SalesStatus = GetString(e, "SalesStatus"),
        DocumentStatus = GetString(e, "DocumentStatus"),
        DataArea = GetString(e, "DataArea") ?? string.Empty,
        PriceGroupId = GetString(e, "PriceGroupId"),
        InventLocationId = GetString(e, "InventLocationId"),
        InventSiteId = GetString(e, "InventSiteId"),
        CreatedDateTime = GetString(e, "CreatedDateTime"),
    };

    private static SalesOrderLine MapLine(JsonElement e) => new()
    {
        RecordId = GetLong(e, "RecordId"),
        SalesId = GetString(e, "SalesId") ?? string.Empty,
        ItemId = GetString(e, "ItemId") ?? string.Empty,
        ProductName = GetString(e, "ProductName"),
        SalesQty = GetDecimal(e, "SalesQty"),
        SalesUnit = GetString(e, "SalesUnit"),
        LineNum = GetDecimal(e, "LineNum"),
        DataArea = GetString(e, "DataArea") ?? string.Empty,
    };

    private static BarcodeItem MapBarcode(JsonElement e) => new()
    {
        Barcode = GetString(e, "ItemBarCode") ?? string.Empty,
        ItemNumber = GetString(e, "ItemNumber") ?? string.Empty,
        ProductName = GetString(e, "ProductName"),
        ProductDescription = GetString(e, "ProductDescription"),
        UnitId = GetString(e, "UnitId"),
        DataArea = GetString(e, "DataArea") ?? string.Empty,
    };

    private static PriceInfo MapPrice(JsonElement e) => new()
    {
        ItemNumber = GetString(e, "ItemRelation") ?? string.Empty,
        Price = GetDecimal(e, "Price"),
        UnitId = GetString(e, "UnitId"),
        CustomerAccountNumber = GetString(e, "CustomerAccountNumber"),
        PriceCustomerGroupCode = GetString(e, "PriceCustomerGroupCode"),
        DataArea = GetString(e, "DataAreaId") ?? string.Empty,
    };

    private static WarehouseOnHand MapOnHand(JsonElement e) => new()
    {
        ItemNumber = GetString(e, "ItemNumber") ?? string.Empty,
        WarehouseId = GetString(e, "InventLocationId") ?? string.Empty,
        AvailableSalesQuantity = GetDecimal(e, "AvailableSalesQuantity"),
        AvailableOnHandQuantity = GetDecimal(e, "AvailPhysical"),
        Unit = GetString(e, "Unit"),
        ProductName = GetString(e, "ProductName"),
    };

    private static string? GetString(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null)
        {
            return null;
        }

        return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
    }

    private static long GetLong(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var p))
        {
            return 0;
        }

        return p.ValueKind switch
        {
            JsonValueKind.Number => p.TryGetInt64(out var l) ? l : (long)p.GetDouble(),
            JsonValueKind.String => long.TryParse(p.GetString(), out var l) ? l : 0,
            _ => 0,
        };
    }

    private static decimal GetDecimal(JsonElement e, string name)
    {
        if (!e.TryGetProperty(name, out var p))
        {
            return 0;
        }

        return p.ValueKind switch
        {
            JsonValueKind.Number => p.TryGetDecimal(out var d) ? d : (decimal)p.GetDouble(),
            JsonValueKind.String => decimal.TryParse(p.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var d) ? d : 0,
            _ => 0,
        };
    }
}
