using System.Globalization;
using System.Text;
using System.Text.Json;
using LogicRetail.Application.Common;
using LogicRetail.Application.Contracts;
using LogicRetail.Domain;

namespace LogicRetail.Integrations.D365;

/// <summary>
/// Live FinOps OData client using LogicRetail*_BI field names from docs/03-Dynamics-Entities-Fields.md.
/// Price/stock resolution uses Sales Order Header context:
/// CustAccount, PriceGroupId, InventLocationId, DataArea (+ UnitID from barcode).
/// </summary>
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

    public async Task<MobileAuthPayload> AuthenticateUserAsync(
        string personnelNumber,
        string password,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var element = await _odata.PostActionAsync(
                "LogicRetailMobileUsersActivation_BI",
                "AuthenticateUser",
                new
                {
                    _personnelNumber = personnelNumber,
                    _password = password,
                },
                cancellationToken);
            return MapAuthPayload(element);
        }
        catch (D365ODataException ex)
        {
            throw MapDynamicsError(ex);
        }
    }

    public async Task<PasswordChangeResult> ChangePasswordAsync(
        string personnelNumber,
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var element = await _odata.PostActionAsync(
                "LogicRetailMobileUsersActivation_BI",
                "ChangePassword",
                new
                {
                    _personnelNumber = personnelNumber,
                    _oldPassword = oldPassword,
                    _newPassword = newPassword,
                },
                cancellationToken);
            return new PasswordChangeResult
            {
                IsSuccess = GetBool(element, "IsSuccess"),
                Message = GetString(element, "Message") ?? string.Empty,
                ActivationRecId = GetLong(element, "ActivationRecId"),
            };
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
                // Match trimmed + leading-space ItemId variants (trial D365 padding).
                var itemFilters = ItemNumberLookupKeys(itemId)
                    .Select(k => $"ItemId eq '{ODataEscaper.String(k)}'")
                    .ToList();
                parts.Add(itemFilters.Count == 1
                    ? itemFilters[0]
                    : $"({string.Join(" or ", itemFilters)})");
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
            // Docs field: Barcode (+ DataArea). Do NOT fall back to ItemBarCode — that property
            // does not exist on LogicRetailItemBarcodes_BI and throws DYNAMICS_ERROR for misses.
            var filter =
                $"Barcode eq '{ODataEscaper.String(code)}' and DataArea eq '{ODataEscaper.String(company)}'";
            var rows = await _odata.QueryAsync("LogicRetailItemBarcodes_BI", filter, cancellationToken);
            if (rows.Count == 0)
            {
                // Retry without DataArea (some rows only keyed by barcode)
                rows = await _odata.QueryAsync(
                    "LogicRetailItemBarcodes_BI",
                    $"Barcode eq '{ODataEscaper.String(code)}'",
                    cancellationToken,
                    crossCompany: true);
            }

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
        string? unitId = null,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // D365 trial often stores ItemNumber with a leading space (" BG410.003").
            foreach (var itemKey in ItemNumberLookupKeys(itemNumber))
            {
                // Priority 1: customer-specific price from header.CustAccount
                if (!string.IsNullOrWhiteSpace(custAccount))
                {
                    var byCustomer = await GetPriceAgreementsInternalAsync(
                        itemKey,
                        dataArea,
                        priceGroup: null,
                        customerAccount: custAccount,
                        unitId,
                        cancellationToken);
                    if (byCustomer.Count > 0)
                    {
                        return byCustomer[0];
                    }
                }

                // Priority 2: price group from header.PriceGroupId
                if (!string.IsNullOrWhiteSpace(priceGroupId))
                {
                    var byGroup = await GetPriceAgreementsInternalAsync(
                        itemKey,
                        dataArea,
                        priceGroup: priceGroupId,
                        customerAccount: null,
                        unitId,
                        cancellationToken);
                    if (byGroup.Count > 0)
                    {
                        return byGroup[0];
                    }
                }

                // Priority 3: general (empty customer + empty group)
                var general = await GetPriceAgreementsInternalAsync(
                    itemKey,
                    dataArea,
                    priceGroup: string.Empty,
                    customerAccount: string.Empty,
                    unitId,
                    cancellationToken);
                if (general.Count > 0)
                {
                    return general[0];
                }

                // Priority 3b: item + dataArea only (ignore empty-string filters if D365 treats null differently)
                var loose = await GetPriceAgreementsInternalAsync(
                    itemKey,
                    dataArea,
                    priceGroup: null,
                    customerAccount: null,
                    unitId,
                    cancellationToken);
                if (loose.Count > 0)
                {
                    // Prefer blank customer+group rows when present
                    var preferred = loose.FirstOrDefault(p =>
                        string.IsNullOrWhiteSpace(p.CustomerAccountNumber) &&
                        string.IsNullOrWhiteSpace(p.PriceCustomerGroupCode));
                    return preferred ?? loose[0];
                }
            }

            return null;
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
        string? unitId = null,
        CancellationToken cancellationToken = default) =>
        GetPriceAgreementsTryingItemVariantsAsync(itemNumber, dataArea, priceGroup, null, unitId, cancellationToken);

    private async Task<IReadOnlyList<PriceInfo>> GetPriceAgreementsTryingItemVariantsAsync(
        string itemNumber,
        string dataArea,
        string? priceGroup,
        string? customerAccount,
        string? unitId,
        CancellationToken cancellationToken)
    {
        foreach (var itemKey in ItemNumberLookupKeys(itemNumber))
        {
            var rows = await GetPriceAgreementsInternalAsync(
                itemKey,
                dataArea,
                priceGroup,
                customerAccount,
                unitId,
                cancellationToken);
            if (rows.Count > 0)
            {
                return rows;
            }
        }

        return Array.Empty<PriceInfo>();
    }

    /// <summary>
    /// null customer/group = omit filter; empty string = filter eq ''.
    /// Docs: ItemNumber + DataArea (+ UnitId) + CustomerAccountNumber and/or PriceCustomerGroupCode.
    /// </summary>
    private async Task<IReadOnlyList<PriceInfo>> GetPriceAgreementsInternalAsync(
        string itemNumber,
        string dataArea,
        string? priceGroup,
        string? customerAccount,
        string? unitId,
        CancellationToken cancellationToken)
    {
        var parts = new List<string>
        {
            $"ItemNumber eq '{ODataEscaper.String(itemNumber)}'",
            $"DataArea eq '{ODataEscaper.String(dataArea)}'",
        };

        if (priceGroup is not null)
        {
            parts.Add($"PriceCustomerGroupCode eq '{ODataEscaper.String(priceGroup)}'");
        }

        if (customerAccount is not null)
        {
            parts.Add($"CustomerAccountNumber eq '{ODataEscaper.String(customerAccount)}'");
        }

        if (!string.IsNullOrWhiteSpace(unitId))
        {
            parts.Add($"UnitId eq '{ODataEscaper.String(unitId)}'");
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
            // Docs only: InventoryWarehouseId (== header InventLocationId). Never query InventLocationId —
            // that property does not exist on LogicRetailWarehouseOnHand_BI and causes DYNAMICS_ERROR.
            foreach (var itemKey in ItemNumberLookupKeys(itemNumber))
            {
                var filter =
                    $"ItemNumber eq '{ODataEscaper.String(itemKey)}' and InventoryWarehouseId eq '{ODataEscaper.String(warehouseId)}'";
                var rows = await _odata.QueryAsync("LogicRetailWarehouseOnHand_BI", filter, cancellationToken);
                if (rows.Count > 0)
                {
                    return MapOnHand(rows[0]);
                }
            }

            return null;
        }
        catch (D365ODataException ex)
        {
            throw MapDynamicsError(ex);
        }
    }

    public async Task<IReadOnlyList<MobileWarehouse>> GetStandardWarehousesAsync(
        string dataAreaId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // InventLocationType is an OData enum, not an Edm.String.
            var filter =
                $"dataAreaId eq '{ODataEscaper.String(dataAreaId)}' and "
                + "InventLocationType eq Microsoft.Dynamics.DataEntities.InventLocationType'Standard'";
            var rows = await _odata.QueryAsync(
                "SiteAndWarehouseMobiles",
                filter,
                cancellationToken,
                crossCompany: true);
            return rows.Select(MapMobileWarehouse).ToList();
        }
        catch (D365ODataException ex)
        {
            throw MapDynamicsError(ex);
        }
    }

    public async Task<IReadOnlyList<MobileCustomer>> GetCustomersAsync(
        string dataAreaId,
        string? search,
        int top,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var filter = new StringBuilder($"dataAreaId eq '{ODataEscaper.String(dataAreaId)}'");
            if (!string.IsNullOrWhiteSpace(search))
            {
                var term = ODataEscaper.String(search.Trim());
                filter.Append(
                    $" and (contains(CustomerAccount,'{term}') or contains(OrganizationName,'{term}'))");
            }

            var rows = await _odata.QueryAsync(
                "CustomersV3",
                filter.ToString(),
                cancellationToken,
                crossCompany: true,
                select: "dataAreaId,CustomerAccount,OrganizationName,CustomerGroupId,SalesCurrencyCode,PrimaryContactPhone,AddressCity",
                top: top,
                orderBy: "CustomerAccount");
            return rows.Select(MapMobileCustomer).ToList();
        }
        catch (D365ODataException ex)
        {
            throw MapDynamicsError(ex);
        }
    }

    public async Task<CreatedSalesOrder> CreateSalesOrderHeaderAsync(
        string dataAreaId,
        string customerAccount,
        string? warehouseId,
        string? siteId,
        string? orderTakerPersonnelNumber,
        string? currencyCode,
        CancellationToken cancellationToken = default)
    {
        // SalesOrderHeadersV4 has no WorkerSalesTaker/CustAccount/InventLocationId columns;
        // the sales taker is written as a personnel number, not an HcmWorker RecId.
        var payload = new Dictionary<string, object?>
        {
            ["dataAreaId"] = dataAreaId,
            ["OrderingCustomerAccountNumber"] = customerAccount,
            ["InvoiceCustomerAccountNumber"] = customerAccount,
        };

        if (!string.IsNullOrWhiteSpace(warehouseId))
        {
            payload["DefaultShippingWarehouseId"] = warehouseId.Trim();
        }

        // D365 accepts a header without a site but does not derive one, leaving the
        // order with a blank site, so resolve it from the warehouse instead.
        var resolvedSite = string.IsNullOrWhiteSpace(siteId)
            ? await ResolveSiteForWarehouseAsync(dataAreaId, warehouseId, cancellationToken)
            : siteId.Trim();
        if (!string.IsNullOrWhiteSpace(resolvedSite))
        {
            payload["DefaultShippingSiteId"] = resolvedSite;
        }

        if (!string.IsNullOrWhiteSpace(orderTakerPersonnelNumber))
        {
            payload["OrderTakerPersonnelNumber"] = orderTakerPersonnelNumber.Trim();
        }

        if (!string.IsNullOrWhiteSpace(currencyCode))
        {
            payload["CurrencyCode"] = currencyCode.Trim();
        }

        try
        {
            var created = await _odata.PostReturningAsync(
                "SalesOrderHeadersV4",
                payload,
                cancellationToken);
            return new CreatedSalesOrder
            {
                DataAreaId = EmptyToNull(GetString(created, "dataAreaId")) ?? dataAreaId,
                SalesOrderNumber = EmptyToNull(GetString(created, "SalesOrderNumber")) ?? string.Empty,
                CustomerAccount =
                    EmptyToNull(GetString(created, "OrderingCustomerAccountNumber")) ?? customerAccount,
                WarehouseId = EmptyToNull(GetString(created, "DefaultShippingWarehouseId")) ?? warehouseId,
                SiteId = EmptyToNull(GetString(created, "DefaultShippingSiteId")) ?? siteId,
                CurrencyCode = EmptyToNull(GetString(created, "CurrencyCode")) ?? currencyCode,
                OrderTakerPersonnelNumber =
                    EmptyToNull(GetString(created, "OrderTakerPersonnelNumber")) ?? orderTakerPersonnelNumber,
            };
        }
        catch (D365ODataException ex)
        {
            throw MapDynamicsError(ex);
        }
    }

    private async Task<string?> ResolveSiteForWarehouseAsync(
        string dataAreaId,
        string? warehouseId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(warehouseId))
        {
            return null;
        }

        var filter =
            $"dataAreaId eq '{ODataEscaper.String(dataAreaId)}' and "
            + $"InventLocationId eq '{ODataEscaper.String(warehouseId.Trim())}'";
        var rows = await _odata.QueryAsync(
            "SiteAndWarehouseMobiles",
            filter,
            cancellationToken,
            crossCompany: true,
            top: 1);
        return rows.Count == 0 ? null : EmptyToNull(GetString(rows[0], "InventSiteId"));
    }

    private static MobileCustomer MapMobileCustomer(JsonElement e) => new()
    {
        DataAreaId = (GetString(e, "dataAreaId") ?? string.Empty).Trim(),
        CustomerAccount = (GetString(e, "CustomerAccount") ?? string.Empty).Trim(),
        Name = (GetString(e, "OrganizationName") ?? string.Empty).Trim(),
        CustomerGroupId = EmptyToNull(GetString(e, "CustomerGroupId")),
        SalesCurrencyCode = EmptyToNull(GetString(e, "SalesCurrencyCode")),
        PrimaryPhone = EmptyToNull(GetString(e, "PrimaryContactPhone")),
        AddressCity = EmptyToNull(GetString(e, "AddressCity")),
    };

    /// <summary>
    /// Trial D365 data often pads ItemNumber with a leading space. Try trimmed + padded keys.
    /// </summary>
    private static IEnumerable<string> ItemNumberLookupKeys(string? itemNumber)
    {
        var raw = itemNumber ?? string.Empty;
        var trimmed = raw.Trim();
        if (string.IsNullOrWhiteSpace(trimmed))
        {
            yield break;
        }

        yield return trimmed;
        yield return " " + trimmed;
        if (!string.IsNullOrEmpty(raw) &&
            !string.Equals(raw, trimmed, StringComparison.Ordinal) &&
            !string.Equals(raw, " " + trimmed, StringComparison.Ordinal))
        {
            yield return raw;
        }
    }

    public async Task CreateSalesOrderLineAsync(
        string dataAreaId,
        string salesOrderNumber,
        string itemNumber,
        int orderedSalesQuantity,
        CancellationToken cancellationToken = default)
    {
        // Trial D365 ReleasedProduct ItemIds are often left-padded (" BG410.003").
        // Posting trimmed ItemNumber fails Infolog: "Item number X does not exist" (and can be slow).
        // Prefer padded write key first to avoid wasted round-trips / HttpClient timeouts.
        D365ODataException? lastItemMissing = null;
        foreach (var itemKey in ItemNumberWriteKeys(itemNumber))
        {
            try
            {
                await _odata.PostAsync(
                    "SalesOrderLines",
                    new
                    {
                        dataAreaId,
                        SalesOrderNumber = salesOrderNumber,
                        ItemNumber = itemKey,
                        OrderedSalesQuantity = orderedSalesQuantity,
                    },
                    cancellationToken);
                return;
            }
            catch (D365ODataException ex) when (IsItemNumberMissingWriteError(ex))
            {
                lastItemMissing = ex;
            }
            catch (D365ODataException ex)
            {
                throw MapDynamicsError(ex);
            }
        }

        throw MapDynamicsError(
            lastItemMissing
            ?? new D365ODataException("No ItemNumber write variant succeeded.", 400));
    }

    /// <summary>Write-order keys: padded variant first (trial Released products), then trimmed.</summary>
    private static IEnumerable<string> ItemNumberWriteKeys(string? itemNumber)
    {
        var keys = ItemNumberLookupKeys(itemNumber).ToList();
        if (keys.Count <= 1)
        {
            return keys;
        }

        // Prefer leading-space form when present
        return keys
            .OrderByDescending(k => k.Length > 0 && k[0] == ' ')
            .ThenBy(k => k.Length);
    }

    private static bool IsItemNumberMissingWriteError(D365ODataException ex)
    {
        var msg = ex.Message ?? string.Empty;
        return msg.Contains("does not exist", StringComparison.OrdinalIgnoreCase)
               || (msg.Contains("Item number", StringComparison.OrdinalIgnoreCase)
                   && msg.Contains("not exist", StringComparison.OrdinalIgnoreCase));
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

    private static MobileAuthPayload MapAuthPayload(JsonElement e) => new()
    {
        IsSuccess = GetBool(e, "IsSuccess"),
        Message = GetString(e, "Message") ?? string.Empty,
        ActivationRecId = GetLong(e, "ActivationRecId"),
        HcmWorkerRecId = GetLong(e, "HcmWorkerRecId"),
        PersonnelNumber = GetString(e, "PersonnelNumber") ?? string.Empty,
        UserId = GetString(e, "UserId"),
        WorkerName = GetString(e, "WorkerName"),
        Company = GetString(e, "Company"),
        IsActive = GetBool(e, "IsActive"),
        UserInfoEnable = GetBool(e, "UserInfoEnable"),
        RetailChannelTableRecId = GetLong(e, "RetailChannelTableRecID", "RetailChannelTableRecId"),
        RetailChannelId = EmptyToNull(GetString(e, "RetailChannelId")),
        ChannelType = (int)GetLong(e, "ChannelType"),
        InventLocation = EmptyToNull(GetString(e, "InventLocation")),
        InventLocationDataAreaId = EmptyToNull(GetString(e, "InventLocationDataAreaId")),
        Currency = EmptyToNull(GetString(e, "Currency")),
        DefaultCustAccount = EmptyToNull(GetString(e, "DefaultCustAccount")),
        DefaultCustDataAreaId = EmptyToNull(GetString(e, "DefaultCustDataAreaId")),
    };

    private static string? EmptyToNull(string? value) =>
        string.IsNullOrWhiteSpace(value) ? null : value.Trim();

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
        CreatedDateTime = GetString(e, "CreatedDateTime1", "CreatedDateTime"),
    };

    private static SalesOrderLine MapLine(JsonElement e) => new()
    {
        RecordId = GetLong(e, "RecordID", "RecordId"),
        SalesId = GetString(e, "SalesId") ?? string.Empty,
        ItemId = (GetString(e, "ItemId") ?? string.Empty).Trim(),
        ProductName = GetString(e, "ProductName"),
        SalesQty = GetDecimal(e, "SalesQty"),
        SalesUnit = GetString(e, "SalesUnit"),
        LineNum = GetDecimal(e, "LineNum"),
        DataArea = (GetString(e, "DataArea") ?? string.Empty).Trim(),
    };

    private static BarcodeItem MapBarcode(JsonElement e) => new()
    {
        Barcode = (GetString(e, "Barcode", "ItemBarCode") ?? string.Empty).Trim(),
        ItemNumber = (GetString(e, "ItemNumber") ?? string.Empty).Trim(),
        ProductName = GetString(e, "ProductName"),
        ProductDescription = GetString(e, "ProductDescription"),
        UnitId = GetString(e, "UnitID", "UnitId")?.Trim(),
        DataArea = (GetString(e, "DataArea") ?? string.Empty).Trim(),
    };

    private static PriceInfo MapPrice(JsonElement e) => new()
    {
        ItemNumber = (GetString(e, "ItemNumber", "ItemRelation") ?? string.Empty).Trim(),
        Price = GetDecimal(e, "Price"),
        UnitId = GetString(e, "UnitId", "UnitID")?.Trim(),
        CustomerAccountNumber = GetString(e, "CustomerAccountNumber")?.Trim(),
        PriceCustomerGroupCode = GetString(e, "PriceCustomerGroupCode")?.Trim(),
        DataArea = (GetString(e, "DataArea", "DataAreaId") ?? string.Empty).Trim(),
    };

    private static WarehouseOnHand MapOnHand(JsonElement e) => new()
    {
        ItemNumber = (GetString(e, "ItemNumber") ?? string.Empty).Trim(),
        WarehouseId = (GetString(e, "InventoryWarehouseId", "InventLocationId") ?? string.Empty).Trim(),
        AvailableSalesQuantity = GetDecimal(e, "AvailableSalesQuantity"),
        AvailableOnHandQuantity = GetDecimal(e, "AvailableOnHandQuantity", "AvailPhysical"),
        Unit = GetString(e, "ConvertedUnitSymbol", "OriginalUnitSymbol", "Unit")?.Trim(),
        ProductName = GetString(e, "ProductName"),
    };

    private static MobileWarehouse MapMobileWarehouse(JsonElement e) => new()
    {
        DataAreaId = (GetString(e, "dataAreaId", "DataAreaId") ?? string.Empty).Trim(),
        InventLocationId = (GetString(e, "InventLocationId") ?? string.Empty).Trim(),
        Name = (GetString(e, "Name") ?? string.Empty).Trim(),
        InventSiteId = EmptyToNull(GetString(e, "InventSiteId")),
        InventLocationType = (GetString(e, "InventLocationType") ?? string.Empty).Trim(),
    };

    private static bool GetBool(JsonElement e, params string[] names)
    {
        foreach (var name in names)
        {
            if (!e.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            return p.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.Number => p.TryGetInt64(out var n) && n != 0,
                JsonValueKind.String => p.GetString() is { } s
                    && (s.Equals("true", StringComparison.OrdinalIgnoreCase)
                        || s.Equals("Yes", StringComparison.OrdinalIgnoreCase)
                        || s == "1"),
                _ => false,
            };
        }

        return false;
    }

    private static string? GetString(JsonElement e, params string[] names)
    {
        foreach (var name in names)
        {
            if (!e.TryGetProperty(name, out var p) || p.ValueKind == JsonValueKind.Null)
            {
                continue;
            }

            return p.ValueKind == JsonValueKind.String ? p.GetString() : p.ToString();
        }

        return null;
    }

    private static long GetLong(JsonElement e, params string[] names)
    {
        foreach (var name in names)
        {
            if (!e.TryGetProperty(name, out var p))
            {
                continue;
            }

            return p.ValueKind switch
            {
                JsonValueKind.Number => p.TryGetInt64(out var l) ? l : (long)p.GetDouble(),
                JsonValueKind.String => long.TryParse(p.GetString(), out var l) ? l : 0,
                _ => 0,
            };
        }

        return 0;
    }

    private static decimal GetDecimal(JsonElement e, params string[] names)
    {
        foreach (var name in names)
        {
            if (!e.TryGetProperty(name, out var p))
            {
                continue;
            }

            return p.ValueKind switch
            {
                JsonValueKind.Number => p.TryGetDecimal(out var d) ? d : (decimal)p.GetDouble(),
                JsonValueKind.String => decimal.TryParse(
                    p.GetString(),
                    NumberStyles.Any,
                    CultureInfo.InvariantCulture,
                    out var d)
                    ? d
                    : 0,
                _ => 0,
            };
        }

        return 0;
    }
}
