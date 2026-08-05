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
        CancellationToken cancellationToken = default) =>
        Task.FromResult<PriceInfo?>(new PriceInfo
        {
            ItemNumber = itemNumber,
            Price = 25.5m,
            UnitId = "ea",
            CustomerAccountNumber = custAccount,
            PriceCustomerGroupCode = priceGroupId,
            DataArea = dataArea,
        });

    public Task<IReadOnlyList<PriceInfo>> GetPriceAgreementsAsync(
        string itemNumber,
        string dataArea,
        string? priceGroup,
        CancellationToken cancellationToken = default)
    {
        IReadOnlyList<PriceInfo> list =
        [
            new PriceInfo
            {
                ItemNumber = itemNumber,
                Price = 25.5m,
                UnitId = "ea",
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
}
