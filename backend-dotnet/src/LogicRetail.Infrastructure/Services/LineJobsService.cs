using LogicRetail.Application.Common;
using LogicRetail.Application.Contracts;
using LogicRetail.Domain;

namespace LogicRetail.Infrastructure.Services;

public static class LineJobMessages
{
    public static readonly (string Ar, string En) AlreadyExists = (
        "هذا السطر موجود مسبقاً في النظام أو تم إدخاله من قبل.",
        "This line already exists in the system or has been entered previously.");

    public static readonly (string Ar, string En) NoItem = (
        "لم يتم العثور على منتج مطابق لهذا الباركود.",
        "No matching product found for the provided barcode.");

    public static readonly (string Ar, string En) NoPrice = (
        "لا يوجد سعر محدد لهذا المنتج.",
        "No price is defined for this product.");

    public static readonly (string Ar, string En) QtyExceeds = (
        "الكمية المطلوبة أكبر من المخزون المتاح.",
        "Requested quantity exceeds available stock.");

    public static readonly (string Ar, string En) NoStock = (
        "لا يوجد مخزون متاح لهذا المنتج في المستودع.",
        "No available stock for this product in the warehouse.");

    public static readonly (string Ar, string En) InvalidQty = (
        "الكمية غير صحيحة.",
        "Quantity is invalid.");

    public static readonly (string Ar, string En) SoNotOpen = (
        "أمر المبيعات غير مفتوح للإضافة.",
        "Sales order is not open for line addition.");
}

public sealed class LineJobsService
{
    private readonly IDynamicsClient _dynamics;
    private readonly IJsonStore _store;

    public LineJobsService(IDynamicsClient dynamics, IJsonStore store)
    {
        _dynamics = dynamics;
        _store = store;
    }

    public static bool IsValidQty(int qty) => qty >= 1;

    public async Task<object> SubmitFullAsync(
        string salesId,
        string company,
        long workerRecId,
        string itemNumber,
        int quantity,
        CancellationToken ct)
    {
        if (!IsValidQty(quantity))
        {
            throw new AppException(LineJobMessages.InvalidQty.En, 400, "INVALID_QTY");
        }

        var header = await GetOpenSalesOrderAsync(salesId, company, workerRecId, ct);
        // Price from header context: CustAccount / PriceGroupId + DataArea
        var price = await _dynamics.ResolvePriceAsync(
            itemNumber,
            company,
            header.CustAccount,
            header.PriceGroupId,
            unitId: null,
            ct);
        if (price is null)
        {
            return PersistFailure(salesId, company, workerRecId, "full", itemNumber, null, quantity, LineJobMessages.NoPrice);
        }

        // Stock from header.InventLocationId → InventoryWarehouseId
        var onHand = await _dynamics.GetWarehouseOnHandAsync(itemNumber, header.InventLocationId ?? string.Empty, ct);
        if (onHand is null)
        {
            return PersistFailure(salesId, company, workerRecId, "full", itemNumber, null, quantity, LineJobMessages.NoStock);
        }

        if (quantity > onHand.AvailableSalesQuantity)
        {
            return PersistFailure(salesId, company, workerRecId, "full", itemNumber, null, quantity, LineJobMessages.QtyExceeds);
        }

        var existing = await _dynamics.GetSalesOrderLinesAsync(salesId, company, itemNumber, ct);
        if (existing.Count > 0)
        {
            return PersistFailure(salesId, company, workerRecId, "full", itemNumber, null, quantity, LineJobMessages.AlreadyExists);
        }

        await _dynamics.CreateSalesOrderLineAsync(company, salesId, itemNumber, quantity, ct);
        var jobId = Guid.NewGuid().ToString();
        var itemId = Guid.NewGuid().ToString();
        _store.InsertJob(new LineJobRow
        {
            Id = jobId,
            SalesId = salesId,
            Company = company,
            WorkerRecId = workerRecId,
            Mode = "full",
            Status = "completed",
            IsFailed = false,
        });
        _store.InsertJobItem(new LineJobItemRow
        {
            Id = itemId,
            JobId = jobId,
            ItemNumber = itemNumber,
            Quantity = quantity,
            Status = "synced",
        });

        return new
        {
            success = true,
            jobId,
            item = new
            {
                id = itemId,
                itemNumber,
                quantity,
                status = "synced",
                price = price.Price,
                unitId = price.UnitId,
                availableQty = onHand.AvailableSalesQuantity,
            },
        };
    }

    public async Task<object> SubmitQuickAsync(
        string salesId,
        string company,
        long workerRecId,
        IReadOnlyList<(string Barcode, int Quantity)> lines,
        CancellationToken ct)
    {
        if (lines.Count == 0)
        {
            throw new AppException("At least one line is required", 400, "VALIDATION_ERROR");
        }

        if (lines.Count > 10)
        {
            throw new AppException("Quick add allows max 10 lines", 400, "MAX_LINES");
        }

        var header = await GetOpenSalesOrderAsync(salesId, company, workerRecId, ct);
        var jobId = Guid.NewGuid().ToString();
        _store.InsertJob(new LineJobRow
        {
            Id = jobId,
            SalesId = salesId,
            Company = company,
            WorkerRecId = workerRecId,
            Mode = "quick",
            Status = "processing",
            IsFailed = false,
        });

        var results = new List<object>();
        var anyFailed = false;

        foreach (var line in lines)
        {
            var itemId = Guid.NewGuid().ToString();
            var barcode = line.Barcode ?? string.Empty;
            var qty = line.Quantity;

            if (!IsValidQty(qty))
            {
                anyFailed = true;
                InsertFailed(jobId, itemId, barcode, null, qty, LineJobMessages.InvalidQty);
                results.Add(Failed(itemId, barcode, null, qty, LineJobMessages.InvalidQty));
                continue;
            }

            var barcodeRow = await _dynamics.GetBarcodeAsync(barcode, company, ct);
            if (barcodeRow is null)
            {
                anyFailed = true;
                InsertFailed(jobId, itemId, barcode, null, qty, LineJobMessages.NoItem);
                results.Add(Failed(itemId, barcode, null, qty, LineJobMessages.NoItem));
                continue;
            }

            var itemNumber = barcodeRow.ItemNumber;
            var existing = await _dynamics.GetSalesOrderLinesAsync(salesId, company, itemNumber, ct);
            if (existing.Count > 0)
            {
                anyFailed = true;
                InsertFailed(jobId, itemId, barcode, itemNumber, qty, LineJobMessages.AlreadyExists);
                results.Add(Failed(itemId, barcode, itemNumber, qty, LineJobMessages.AlreadyExists));
                continue;
            }

            // Header-driven price: CustAccount OR PriceGroupId + barcode UnitID + DataArea
            var price = await _dynamics.ResolvePriceAsync(
                itemNumber,
                company,
                header.CustAccount,
                header.PriceGroupId,
                barcodeRow.UnitId,
                ct);
            if (price is null)
            {
                anyFailed = true;
                InsertFailed(jobId, itemId, barcode, itemNumber, qty, LineJobMessages.NoPrice);
                results.Add(Failed(itemId, barcode, itemNumber, qty, LineJobMessages.NoPrice));
                continue;
            }

            // Header-driven stock: InventLocationId → InventoryWarehouseId
            var onHand = await _dynamics.GetWarehouseOnHandAsync(itemNumber, header.InventLocationId ?? string.Empty, ct);
            if (onHand is null)
            {
                anyFailed = true;
                InsertFailed(jobId, itemId, barcode, itemNumber, qty, LineJobMessages.NoStock);
                results.Add(Failed(itemId, barcode, itemNumber, qty, LineJobMessages.NoStock));
                continue;
            }

            if (qty > onHand.AvailableSalesQuantity)
            {
                anyFailed = true;
                InsertFailed(jobId, itemId, barcode, itemNumber, qty, LineJobMessages.QtyExceeds);
                results.Add(Failed(itemId, barcode, itemNumber, qty, LineJobMessages.QtyExceeds));
                continue;
            }

            await _dynamics.CreateSalesOrderLineAsync(company, salesId, itemNumber, qty, ct);
            _store.InsertJobItem(new LineJobItemRow
            {
                Id = itemId,
                JobId = jobId,
                Barcode = barcode,
                ItemNumber = itemNumber,
                Quantity = qty,
                Status = "synced",
            });
            results.Add(new
            {
                id = itemId,
                barcode,
                itemNumber,
                quantity = qty,
                status = "synced",
                commentAr = (string?)null,
                commentEn = (string?)null,
            });
        }

        _store.UpdateJob(jobId, anyFailed ? "completed_with_errors" : "completed", anyFailed);
        return new { success = !anyFailed, jobId, items = results, isFailed = anyFailed };
    }

    public object GetFailedLines(string salesId, string company, string? mode)
    {
        var jobs = _store.FindFailedJobs(salesId, company, mode);
        if (jobs.Count == 0)
        {
            return Array.Empty<object>();
        }

        var items = _store.FindFailedItems(jobs.Select(j => j.Id));
        return items.Select(i => new
        {
            id = i.Id,
            jobId = i.JobId,
            barcode = i.Barcode,
            itemNumber = i.ItemNumber,
            quantity = i.Quantity,
            status = i.Status,
            commentAr = i.CommentAr,
            commentEn = i.CommentEn,
            createdAt = i.CreatedAt,
        }).ToList();
    }

    private async Task<SalesOrderHeader> GetOpenSalesOrderAsync(
        string salesId,
        string company,
        long workerRecId,
        CancellationToken ct)
    {
        var headers = await _dynamics.GetSalesOrderHeadersAsync(workerRecId, company, salesId, true, ct);
        if (headers.Count == 0)
        {
            throw new AppException(LineJobMessages.SoNotOpen.En, 404, "SO_NOT_OPEN");
        }

        return headers[0];
    }

    private object PersistFailure(
        string salesId,
        string company,
        long workerRecId,
        string mode,
        string? itemNumber,
        string? barcode,
        int quantity,
        (string Ar, string En) messages)
    {
        var jobId = Guid.NewGuid().ToString();
        var itemId = Guid.NewGuid().ToString();
        _store.InsertJob(new LineJobRow
        {
            Id = jobId,
            SalesId = salesId,
            Company = company,
            WorkerRecId = workerRecId,
            Mode = mode,
            Status = "completed_with_errors",
            IsFailed = true,
        });
        InsertFailed(jobId, itemId, barcode, itemNumber, quantity, messages);
        return new
        {
            success = false,
            jobId,
            item = new
            {
                id = itemId,
                itemNumber,
                barcode,
                quantity,
                status = "failed",
                commentAr = messages.Ar,
                commentEn = messages.En,
            },
        };
    }

    private void InsertFailed(
        string jobId,
        string id,
        string? barcode,
        string? itemNumber,
        decimal quantity,
        (string Ar, string En) messages) =>
        _store.InsertJobItem(new LineJobItemRow
        {
            Id = id,
            JobId = jobId,
            Barcode = barcode,
            ItemNumber = itemNumber,
            Quantity = quantity,
            Status = "failed",
            CommentAr = messages.Ar,
            CommentEn = messages.En,
        });

    private static object Failed(
        string id,
        string? barcode,
        string? itemNumber,
        int quantity,
        (string Ar, string En) messages) =>
        new
        {
            id,
            barcode,
            itemNumber,
            quantity,
            status = "failed",
            commentAr = messages.Ar,
            commentEn = messages.En,
        };
}
