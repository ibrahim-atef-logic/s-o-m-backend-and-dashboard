using LogicRetail.Application.Common;
using LogicRetail.Application.Contracts;
using LogicRetail.Domain;

namespace LogicRetail.Integrations.D365;

public sealed class MockDynamicsClient : IDynamicsClient
{
    private static readonly List<RetailUserRow> Users =
    [
        new()
        {
            PersonnelNumber = "EMP001",
            HcmWorkerRecId = 5637144576,
            Name = "Ahmed Sales",
            GroupCompany = "usmf",
            GroupCompanyName = "USMF Contoso",
            GroupId = "GRP-USMF",
        },
        new()
        {
            PersonnelNumber = "EMP001",
            HcmWorkerRecId = 5637144576,
            Name = "Ahmed Sales",
            GroupCompany = "ussi",
            GroupCompanyName = "USSI Contoso",
            GroupId = "GRP-USSI",
        },
        new()
        {
            PersonnelNumber = "EMP002",
            HcmWorkerRecId = 5637144577,
            Name = "Sara Retail",
            GroupCompany = "usmf",
            GroupCompanyName = "USMF Contoso",
            GroupId = "GRP-USMF",
        },
        new()
        {
            PersonnelNumber = "1006",
            HcmWorkerRecId = 5637144578,
            Name = "Trial User",
            GroupCompany = "usmf",
            GroupCompanyName = "USMF Contoso",
            GroupId = "GRP-USMF",
        },
        // Emulator/demo company code typed on login (trial environment label)
        new()
        {
            PersonnelNumber = "1006",
            HcmWorkerRecId = 5637144578,
            Name = "Trial User",
            GroupCompany = "logic-trial",
            GroupCompanyName = "Logic Trial",
            GroupId = "GRP-TRIAL",
        },
    ];

    private static readonly List<SalesOrderHeader> Headers =
    [
        new()
        {
            SalesId = "SO-000100",
            CustAccount = "US-001",
            SalesName = "Contoso Retail",
            WorkerSalesTaker = 5637144576,
            SalesStatus = "Backorder",
            DocumentStatus = "None",
            DataArea = "usmf",
            PriceGroupId = "Retail",
            InventLocationId = "11",
            InventSiteId = "1",
            CreatedDateTime = DateTime.UtcNow.ToString("O"),
        },
        new()
        {
            SalesId = "SO-000100",
            CustAccount = "US-001",
            SalesName = "Contoso Retail",
            WorkerSalesTaker = 5637144578,
            SalesStatus = "Backorder",
            DocumentStatus = "None",
            DataArea = "usmf",
            PriceGroupId = "Retail",
            InventLocationId = "11",
            InventSiteId = "1",
            CreatedDateTime = DateTime.UtcNow.ToString("O"),
        },
        new()
        {
            SalesId = "SO-000200",
            CustAccount = "TR-001",
            SalesName = "Logic Trial Retail",
            WorkerSalesTaker = 5637144578,
            SalesStatus = "Backorder",
            DocumentStatus = "None",
            DataArea = "logic-trial",
            PriceGroupId = "Retail",
            InventLocationId = "11",
            InventSiteId = "1",
            CreatedDateTime = DateTime.UtcNow.ToString("O"),
        },
        new()
        {
            SalesId = "SO-MM-1006",
            CustAccount = "10-10002",
            SalesName = "محمد عفيف",
            WorkerSalesTaker = 5637144578,
            SalesStatus = "Backorder",
            DocumentStatus = "None",
            DataArea = "mm",
            PriceGroupId = "Retail",
            InventLocationId = "MMS000WH",
            InventSiteId = "1",
            CreatedDateTime = DateTime.UtcNow.ToString("O"),
        },
    ];

    private readonly List<SalesOrderLine> _lines =
    [
        new()
        {
            RecordId = 1,
            SalesId = "SO-000100",
            ItemId = "ITEM-100",
            ProductName = "Demo Item",
            SalesQty = 2,
            SalesUnit = "ea",
            LineNum = 1,
            DataArea = "usmf",
        },
        new()
        {
            RecordId = 2,
            SalesId = "SO-000200",
            ItemId = "ITEM-100",
            ProductName = "Demo Item",
            SalesQty = 1,
            SalesUnit = "ea",
            LineNum = 1,
            DataArea = "logic-trial",
        },
        new()
        {
            RecordId = 3,
            SalesId = "SO-MM-1006",
            ItemId = "ITEM-100",
            ProductName = "Demo Item",
            SalesQty = 1,
            SalesUnit = "ea",
            LineNum = 1,
            DataArea = "mm",
        },
    ];

    public Task<IReadOnlyList<RetailUserRow>> GetUsersAsync(
        string? personnelNumber,
        string? password,
        string? company,
        bool activatedOnly,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<RetailUserRow> q = Users;
        if (!string.IsNullOrWhiteSpace(personnelNumber))
        {
            q = q.Where(u => u.PersonnelNumber == personnelNumber);
        }

        if (password is not null)
        {
            // Mock passwords: EMP001/1234, EMP002/pass, 1006/123
            q = q.Where(u =>
                (u.PersonnelNumber == "EMP001" && password == "1234")
                || (u.PersonnelNumber == "EMP002" && password == "pass")
                || (u.PersonnelNumber == "1006" && password == "123")
                || password is null);
        }

        if (!string.IsNullOrWhiteSpace(company))
        {
            q = q.Where(u => string.Equals(u.GroupCompany, company, StringComparison.OrdinalIgnoreCase));
        }

        return Task.FromResult<IReadOnlyList<RetailUserRow>>(q.ToList());
    }

    public Task<MobileAuthPayload> AuthenticateUserAsync(
        string personnelNumber,
        string password,
        CancellationToken cancellationToken = default)
    {
        var key = personnelNumber.Trim();
        if (!MockPasswords.TryGetValue(key, out var expected) || expected != password)
        {
            return Task.FromResult(new MobileAuthPayload
            {
                IsSuccess = false,
                Message = $"Authentication failed for worker {key}.",
                PersonnelNumber = key,
            });
        }

        if (MockAuth.TryGetValue(key, out var payload))
        {
            return Task.FromResult(payload);
        }

        return Task.FromResult(new MobileAuthPayload
        {
            IsSuccess = false,
            Message = $"Authentication failed for worker {key}.",
            PersonnelNumber = key,
        });
    }

    public Task<PasswordChangeResult> ChangePasswordAsync(
        string personnelNumber,
        string oldPassword,
        string newPassword,
        CancellationToken cancellationToken = default)
    {
        var key = personnelNumber.Trim();
        if (!MockPasswords.TryGetValue(key, out var expected) || expected != oldPassword)
        {
            return Task.FromResult(new PasswordChangeResult
            {
                IsSuccess = false,
                Message = $"The current password is incorrect for worker {key}.",
            });
        }

        if (string.IsNullOrWhiteSpace(newPassword))
        {
            return Task.FromResult(new PasswordChangeResult
            {
                IsSuccess = false,
                Message = "New password is required.",
            });
        }

        var recId = MockAuth.TryGetValue(key, out var payload) ? payload.ActivationRecId : 0;
        return Task.FromResult(new PasswordChangeResult
        {
            IsSuccess = true,
            Message = $"Password changed successfully for worker {key}.",
            ActivationRecId = recId,
        });
    }

    private static readonly Dictionary<string, string> MockPasswords = new(StringComparer.Ordinal)
    {
        ["EMP001"] = "1234",
        ["EMP002"] = "pass",
        ["1006"] = "123",
        ["12344"] = "123",
        ["DISABLED"] = "123",
    };

    private static readonly Dictionary<string, MobileAuthPayload> MockAuth = new(StringComparer.Ordinal)
    {
        ["EMP001"] = new()
        {
            IsSuccess = true,
            Message = "Authentication successful for worker EMP001.",
            ActivationRecId = 5637140001,
            HcmWorkerRecId = 5637144576,
            PersonnelNumber = "EMP001",
            UserId = "ahmed.sales",
            WorkerName = "Ahmed Sales",
            Company = "usmf",
            IsActive = true,
            UserInfoEnable = true,
            RetailChannelTableRecId = 100,
            RetailChannelId = "S0001",
            ChannelType = 0,
            InventLocation = "11",
            InventLocationDataAreaId = "usmf",
            Currency = "USD",
            DefaultCustAccount = "US-001",
            DefaultCustDataAreaId = "usmf",
        },
        ["EMP002"] = new()
        {
            IsSuccess = true,
            Message = "Authentication successful for worker EMP002.",
            ActivationRecId = 5637140002,
            HcmWorkerRecId = 5637144577,
            PersonnelNumber = "EMP002",
            UserId = "sara.retail",
            WorkerName = "Sara Retail",
            Company = "usmf",
            IsActive = true,
            UserInfoEnable = true,
            InventLocation = "11",
            InventLocationDataAreaId = "usmf",
            Currency = "USD",
            DefaultCustAccount = "US-001",
            DefaultCustDataAreaId = "usmf",
        },
        ["1006"] = new()
        {
            IsSuccess = true,
            Message = "Authentication successful for worker 1006.",
            ActivationRecId = 5637144576,
            HcmWorkerRecId = 5637144578,
            PersonnelNumber = "1006",
            UserId = "m.afif",
            WorkerName = "محمد عفيف",
            Company = "MM",
            IsActive = true,
            UserInfoEnable = true,
            RetailChannelTableRecId = 5637152827,
            RetailChannelId = "912",
            ChannelType = 4,
            InventLocation = "MMS000WH",
            InventLocationDataAreaId = "mm",
            Currency = "SAR",
            DefaultCustAccount = "10-10002",
            DefaultCustDataAreaId = "mm",
        },
        ["12344"] = new()
        {
            IsSuccess = true,
            Message = "Authentication successful for worker 12344.",
            ActivationRecId = 5637145326,
            HcmWorkerRecId = 5637224076,
            PersonnelNumber = "12344",
            UserId = "m.wahas",
            WorkerName = "مروان وهاس",
            Company = "PLTR",
            IsActive = true,
            UserInfoEnable = true,
            RetailChannelTableRecId = 0,
            RetailChannelId = null,
            ChannelType = 0,
            InventLocation = null,
            InventLocationDataAreaId = null,
            Currency = null,
            DefaultCustAccount = null,
            DefaultCustDataAreaId = null,
        },
        ["DISABLED"] = new()
        {
            IsSuccess = true,
            Message = "Authentication successful for worker DISABLED.",
            ActivationRecId = 1,
            HcmWorkerRecId = 1,
            PersonnelNumber = "DISABLED",
            UserId = "disabled.user",
            WorkerName = "Disabled User",
            Company = "usmf",
            IsActive = false,
            UserInfoEnable = true,
            InventLocationDataAreaId = "usmf",
        },
    };

    public Task<IReadOnlyList<SalesOrderHeader>> GetSalesOrderHeadersAsync(
        long? workerRecId,
        string? company,
        string? salesId,
        bool openOnly,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<SalesOrderHeader> q = Headers;
        if (workerRecId is not null)
        {
            q = q.Where(h => h.WorkerSalesTaker == workerRecId);
        }

        if (!string.IsNullOrWhiteSpace(company))
        {
            q = q.Where(h => string.Equals(h.DataArea, company, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(salesId))
        {
            q = q.Where(h => h.SalesId == salesId);
        }

        return Task.FromResult<IReadOnlyList<SalesOrderHeader>>(q.DistinctBy(h => (h.SalesId, h.WorkerSalesTaker)).ToList());
    }

    public Task<IReadOnlyList<SalesOrderLine>> GetSalesOrderLinesAsync(
        string salesId,
        string company,
        string? itemId = null,
        CancellationToken cancellationToken = default)
    {
        IEnumerable<SalesOrderLine> q = _lines.Where(l =>
            l.SalesId == salesId
            && string.Equals(l.DataArea, company, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(itemId))
        {
            q = q.Where(l => l.ItemId == itemId);
        }

        return Task.FromResult<IReadOnlyList<SalesOrderLine>>(q.ToList());
    }

    public Task<BarcodeItem?> GetBarcodeAsync(
        string code,
        string company,
        CancellationToken cancellationToken = default)
    {
        if (code is "BC-100" or "123456")
        {
            return Task.FromResult<BarcodeItem?>(new BarcodeItem
            {
                Barcode = code,
                ItemNumber = "ITEM-200",
                ProductName = "Scan Item",
                UnitId = "ea",
                DataArea = company,
            });
        }

        return Task.FromResult<BarcodeItem?>(null);
    }

    public Task<PriceInfo?> ResolvePriceAsync(
        string itemNumber,
        string dataArea,
        string? custAccount,
        string? priceGroupId,
        string? unitId = null,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<PriceInfo?>(new PriceInfo
        {
            ItemNumber = itemNumber,
            Price = 25.5m,
            UnitId = unitId ?? "ea",
            CustomerAccountNumber = custAccount,
            PriceCustomerGroupCode = priceGroupId,
            DataArea = dataArea,
        });

    public Task<IReadOnlyList<PriceInfo>> GetPriceAgreementsAsync(
        string itemNumber,
        string dataArea,
        string? priceGroup,
        string? unitId = null,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PriceInfo> list =
        [
            new PriceInfo
            {
                ItemNumber = itemNumber,
                Price = 25.5m,
                UnitId = unitId ?? "ea",
                PriceCustomerGroupCode = priceGroup,
                DataArea = dataArea,
            },
        ];
        return Task.FromResult(list);
    }

    public Task<WarehouseOnHand?> GetWarehouseOnHandAsync(
        string itemNumber,
        string warehouseId,
        CancellationToken cancellationToken = default) =>
        Task.FromResult<WarehouseOnHand?>(new WarehouseOnHand
        {
            ItemNumber = itemNumber,
            WarehouseId = warehouseId,
            AvailableSalesQuantity = 100,
            AvailableOnHandQuantity = 100,
            Unit = "ea",
            ProductName = "Scan Item",
        });

    public Task<IReadOnlyList<MobileWarehouse>> GetStandardWarehousesAsync(
        string dataAreaId,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<MobileWarehouse> warehouses =
        [
            new()
            {
                DataAreaId = dataAreaId,
                InventLocationId = "11",
                Name = "Main Warehouse",
                InventSiteId = "1",
                InventLocationType = "Standard",
            },
            new()
            {
                DataAreaId = dataAreaId,
                InventLocationId = "12",
                Name = "Secondary Warehouse",
                InventSiteId = "1",
                InventLocationType = "Standard",
            },
        ];
        return Task.FromResult(warehouses);
    }

    public Task<IReadOnlyList<MobileCustomer>> GetCustomersAsync(
        string dataAreaId,
        string? search,
        int top,
        CancellationToken cancellationToken = default)
    {
        var all = new List<MobileCustomer>
        {
            new()
            {
                DataAreaId = dataAreaId,
                CustomerAccount = "10-10002",
                Name = "Contoso Retail",
                CustomerGroupId = "10",
                SalesCurrencyCode = "SAR",
                PrimaryPhone = "+966500000001",
                AddressCity = "Riyadh",
            },
            new()
            {
                DataAreaId = dataAreaId,
                CustomerAccount = "MMS021",
                Name = "عميل نقدي ميرا مارت جدة 01",
                CustomerGroupId = "20",
                SalesCurrencyCode = "SAR",
                PrimaryPhone = "+966500000002",
                AddressCity = "Jeddah",
            },
        };

        IEnumerable<MobileCustomer> query = all;
        if (!string.IsNullOrWhiteSpace(search))
        {
            query = query.Where(c =>
                c.CustomerAccount.Contains(search, StringComparison.OrdinalIgnoreCase) ||
                c.Name.Contains(search, StringComparison.OrdinalIgnoreCase));
        }

        IReadOnlyList<MobileCustomer> result = query.Take(top > 0 ? top : 50).ToList();
        return Task.FromResult(result);
    }

    public Task<CreatedSalesOrder> CreateSalesOrderHeaderAsync(
        string dataAreaId,
        string customerAccount,
        string? warehouseId,
        string? siteId,
        string? orderTakerPersonnelNumber,
        string? currencyCode,
        CancellationToken cancellationToken = default)
    {
        _createdOrderSeq++;
        var salesOrderNumber = $"{dataAreaId.ToUpperInvariant()}-{_createdOrderSeq:D6}";
        Headers.Add(new SalesOrderHeader
        {
            SalesId = salesOrderNumber,
            CustAccount = customerAccount,
            SalesName = customerAccount,
            DataArea = dataAreaId,
            InventLocationId = warehouseId,
            InventSiteId = siteId,
            WorkerSalesTaker = 5637227826,
        });

        return Task.FromResult(new CreatedSalesOrder
        {
            DataAreaId = dataAreaId,
            SalesOrderNumber = salesOrderNumber,
            CustomerAccount = customerAccount,
            WarehouseId = warehouseId,
            SiteId = siteId,
            CurrencyCode = currencyCode ?? "SAR",
            OrderTakerPersonnelNumber = orderTakerPersonnelNumber,
        });
    }

    private int _createdOrderSeq = 900000;

    public Task CreateSalesOrderLineAsync(
        string dataAreaId,
        string salesOrderNumber,
        string itemNumber,
        int orderedSalesQuantity,
        CancellationToken cancellationToken = default)
    {
        _lines.Add(new SalesOrderLine
        {
            RecordId = _lines.Count + 1,
            SalesId = salesOrderNumber,
            ItemId = itemNumber,
            ProductName = itemNumber,
            SalesQty = orderedSalesQuantity,
            SalesUnit = "ea",
            LineNum = _lines.Count + 1,
            DataArea = dataAreaId,
        });
        return Task.CompletedTask;
    }

    public Task<UpdatedSalesOrderLine> UpdateSalesOrderLineQuantityAsync(
        string dataAreaId,
        string salesOrderNumber,
        string itemNumber,
        int orderedSalesQuantity,
        CancellationToken cancellationToken = default)
    {
        var idx = _lines.FindIndex(l =>
            l.SalesId == salesOrderNumber
            && string.Equals(l.DataArea, dataAreaId, StringComparison.OrdinalIgnoreCase)
            && string.Equals(l.ItemId.Trim(), itemNumber.Trim(), StringComparison.OrdinalIgnoreCase));
        if (idx < 0)
        {
            throw new AppException(
                $"Item {itemNumber.Trim()} is not on sales order {salesOrderNumber}.",
                404,
                "LINE_NOT_FOUND")
            {
                ItemNumber = itemNumber.Trim(),
                SalesId = salesOrderNumber,
            };
        }

        var current = _lines[idx];
        _lines[idx] = new SalesOrderLine
        {
            RecordId = current.RecordId,
            SalesId = current.SalesId,
            ItemId = current.ItemId,
            ProductName = current.ProductName,
            SalesQty = orderedSalesQuantity,
            SalesUnit = current.SalesUnit,
            LineNum = current.LineNum,
            DataArea = current.DataArea,
            InventoryLotId = current.InventoryLotId ?? $"LOT-{current.RecordId}",
        };

        return Task.FromResult(new UpdatedSalesOrderLine
        {
            SalesOrderNumber = salesOrderNumber,
            ItemNumber = current.ItemId,
            Quantity = orderedSalesQuantity,
            InventoryLotId = _lines[idx].InventoryLotId,
            RecordId = current.RecordId,
        });
    }
}
