using LogicRetail.Api;
using LogicRetail.Application.Common;
using LogicRetail.Application.Contracts;
using LogicRetail.Application.Options;
using LogicRetail.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace LogicRetail.Api.Controllers;

[ApiController]
[Route("api/v1/auth")]
public sealed class AuthController : ControllerBase
{
    private readonly AuthService _auth;

    public AuthController(AuthService auth) => _auth = auth;

    public sealed record LoginBody(string Company, string PersonnelNumber, string Password);
    public sealed record RefreshBody(string RefreshToken);
    public sealed record LogoutBody(string? RefreshToken);
    public sealed record ChangePasswordBody(string OldPassword, string NewPassword);

    [HttpPost("login")]
    [AllowAnonymous]
    public async Task<IActionResult> Login([FromBody] LoginBody body, CancellationToken ct)
    {
        var data = await _auth.LoginAsync(body.Company, body.PersonnelNumber, body.Password, ct);
        return Ok(ApiEnvelope.Ok(data));
    }

    [HttpPost("refresh")]
    [AllowAnonymous]
    public async Task<IActionResult> Refresh([FromBody] RefreshBody body, CancellationToken ct)
    {
        var data = await _auth.RefreshAsync(body.RefreshToken, ct);
        return Ok(ApiEnvelope.Ok(data));
    }

    [HttpPost("logout")]
    [AllowAnonymous]
    public IActionResult Logout([FromBody] LogoutBody body) =>
        Ok(ApiEnvelope.Ok(_auth.Logout(body.RefreshToken)));

    [HttpPost("change-password")]
    [Authorize]
    public async Task<IActionResult> ChangePassword([FromBody] ChangePasswordBody body, CancellationToken ct)
    {
        var user = User.GetUser();
        var data = await _auth.ChangePasswordAsync(
            user.PersonnelNumber,
            body.OldPassword,
            body.NewPassword,
            ct);
        return Ok(ApiEnvelope.Ok(data));
    }

    [HttpGet("me")]
    [Authorize]
    public IActionResult Me()
    {
        var user = User.GetUser();
        return Ok(ApiEnvelope.Ok(_auth.Describe(user)));
    }
}

[ApiController]
[Route("api/v1")]
[Authorize]
public sealed class CatalogController : ControllerBase
{
    private readonly IDynamicsClient _dynamics;

    public CatalogController(IDynamicsClient dynamics) => _dynamics = dynamics;

    [HttpGet("sales-orders")]
    public async Task<IActionResult> ListOrders([FromQuery] string company, CancellationToken ct)
    {
        var user = User.GetUser();
        user.AssertCompanyAccess(company);
        var rows = await _dynamics.GetSalesOrderHeadersAsync(user.WorkerRecId, company, null, true, ct);
        var data = rows.Select(MapHeader);
        return Ok(ApiEnvelope.Ok(data));
    }

    [HttpGet("sales-orders/{salesId}")]
    public async Task<IActionResult> GetOrder(string salesId, [FromQuery] string company, CancellationToken ct)
    {
        var user = User.GetUser();
        user.AssertCompanyAccess(company);
        var rows = await _dynamics.GetSalesOrderHeadersAsync(user.WorkerRecId, company, salesId, true, ct);
        if (rows.Count == 0)
        {
            throw new AppException("Sales order not found", 404, "NOT_FOUND");
        }

        return Ok(ApiEnvelope.Ok(MapHeader(rows[0])));
    }

    [HttpGet("sales-orders/{salesId}/lines")]
    public async Task<IActionResult> GetLines(string salesId, [FromQuery] string company, CancellationToken ct)
    {
        var user = User.GetUser();
        user.AssertCompanyAccess(company);
        var headers = await _dynamics.GetSalesOrderHeadersAsync(user.WorkerRecId, company, salesId, true, ct);
        if (headers.Count == 0)
        {
            throw new AppException(LineJobMessages.SoNotOpen.En, 404, "SO_NOT_OPEN");
        }

        var lines = await _dynamics.GetSalesOrderLinesAsync(salesId, company, cancellationToken: ct);
        var data = lines.Select(l => new
        {
            recordId = l.RecordId,
            salesId = l.SalesId,
            itemId = l.ItemId,
            productName = l.ProductName,
            salesQty = l.SalesQty,
            salesUnit = l.SalesUnit,
            lineNum = l.LineNum,
            dataArea = l.DataArea,
        });
        return Ok(ApiEnvelope.Ok(data));
    }

    [HttpGet("barcodes/{code}")]
    public async Task<IActionResult> GetBarcode(string code, [FromQuery] string company, CancellationToken ct)
    {
        User.GetUser().AssertCompanyAccess(company);
        var item = await _dynamics.GetBarcodeAsync(code, company, ct);
        if (item is null)
        {
            throw new AppException("Barcode not found", 404, "BARCODE_NOT_FOUND");
        }

        return Ok(ApiEnvelope.Ok(new
        {
            barcode = item.Barcode,
            itemNumber = item.ItemNumber,
            productName = item.ProductName,
            productDescription = item.ProductDescription,
            unitId = item.UnitId,
            dataArea = item.DataArea,
        }));
    }

    [HttpGet("pricing")]
    public async Task<IActionResult> Pricing(
        [FromQuery] string item,
        [FromQuery] string company,
        [FromQuery] string? custAccount,
        [FromQuery] string? priceGroup,
        [FromQuery] string? unitId,
        CancellationToken ct)
    {
        User.GetUser().AssertCompanyAccess(company);
        var price = await _dynamics.ResolvePriceAsync(item, company, custAccount, priceGroup, unitId, ct);
        if (price is null)
        {
            throw new AppException("No price found", 404, "NO_PRICE");
        }

        return Ok(ApiEnvelope.Ok(new
        {
            itemNumber = price.ItemNumber,
            price = price.Price,
            unitId = price.UnitId,
            customerAccountNumber = price.CustomerAccountNumber,
            priceCustomerGroupCode = price.PriceCustomerGroupCode,
            dataArea = price.DataArea,
        }));
    }

    [HttpGet("inventory")]
    public async Task<IActionResult> Inventory(
        [FromQuery] string item,
        [FromQuery] string warehouse,
        [FromQuery] string company,
        CancellationToken ct)
    {
        User.GetUser().AssertCompanyAccess(company);
        var onHand = await _dynamics.GetWarehouseOnHandAsync(item, warehouse, ct);
        if (onHand is null)
        {
            throw new AppException("No stock found", 404, "NO_STOCK");
        }

        return Ok(ApiEnvelope.Ok(new
        {
            itemNumber = onHand.ItemNumber,
            warehouseId = onHand.WarehouseId,
            availableSalesQuantity = onHand.AvailableSalesQuantity,
            availableOnHandQuantity = onHand.AvailableOnHandQuantity,
            unit = onHand.Unit,
            productName = onHand.ProductName,
        }));
    }

    [HttpGet("warehouses")]
    public async Task<IActionResult> Warehouses([FromQuery] string company, CancellationToken ct)
    {
        User.GetUser().AssertCompanyAccess(company);
        var rows = await _dynamics.GetStandardWarehousesAsync(company, ct);
        var data = rows.Select(w => new
        {
            dataAreaId = w.DataAreaId,
            inventLocationId = w.InventLocationId,
            name = w.Name,
            inventSiteId = w.InventSiteId,
            inventLocationType = w.InventLocationType,
        });
        return Ok(ApiEnvelope.Ok(data));
    }

    [HttpGet("customers")]
    public async Task<IActionResult> Customers(
        [FromQuery] string company,
        [FromQuery] string? search,
        [FromQuery] int? top,
        CancellationToken ct)
    {
        User.GetUser().AssertCompanyAccess(company);
        var rows = await _dynamics.GetCustomersAsync(company, search, Math.Clamp(top ?? 50, 1, 200), ct);
        var data = rows.Select(c => new
        {
            dataAreaId = c.DataAreaId,
            customerAccount = c.CustomerAccount,
            name = c.Name,
            customerGroupId = c.CustomerGroupId,
            salesCurrencyCode = c.SalesCurrencyCode,
            primaryPhone = c.PrimaryPhone,
            addressCity = c.AddressCity,
        });
        return Ok(ApiEnvelope.Ok(data));
    }

    public sealed record CreateSalesOrderBody(
        string? Company,
        string? CustAccount,
        string? InventLocationId,
        string? InventSiteId,
        string? CurrencyCode);

    [HttpPost("sales-orders")]
    public async Task<IActionResult> CreateSalesOrder(
        [FromBody] CreateSalesOrderBody body,
        CancellationToken ct)
    {
        var user = User.GetUser();
        var company = string.IsNullOrWhiteSpace(body.Company) ? user.ActiveCompany : body.Company;
        if (string.IsNullOrWhiteSpace(company))
        {
            throw new AppException("company is required", 400, "VALIDATION_ERROR");
        }

        if (string.IsNullOrWhiteSpace(body.CustAccount))
        {
            throw new AppException("custAccount is required", 400, "VALIDATION_ERROR");
        }

        user.AssertCompanyAccess(company);

        var warehouse = string.IsNullOrWhiteSpace(body.InventLocationId)
            ? user.ActiveWarehouse
            : body.InventLocationId;
        if (string.IsNullOrWhiteSpace(warehouse))
        {
            throw new AppException(
                "inventLocationId is required when the user has no default warehouse",
                400,
                "WAREHOUSE_REQUIRED");
        }

        var created = await _dynamics.CreateSalesOrderHeaderAsync(
            company,
            body.CustAccount.Trim(),
            warehouse,
            body.InventSiteId,
            user.PersonnelNumber,
            string.IsNullOrWhiteSpace(body.CurrencyCode) ? user.Currency : body.CurrencyCode,
            ct);

        return Ok(ApiEnvelope.Ok(new
        {
            salesOrderNumber = created.SalesOrderNumber,
            dataAreaId = created.DataAreaId,
            custAccount = created.CustomerAccount,
            inventLocationId = created.WarehouseId,
            inventSiteId = created.SiteId,
            currencyCode = created.CurrencyCode,
            orderTakerPersonnelNumber = created.OrderTakerPersonnelNumber,
        }));
    }

    private static object MapHeader(Domain.SalesOrderHeader h) => new
    {
        salesId = h.SalesId,
        custAccount = h.CustAccount,
        salesName = h.SalesName,
        workerSalesTaker = h.WorkerSalesTaker,
        salesStatus = h.SalesStatus,
        documentStatus = h.DocumentStatus,
        dataArea = h.DataArea,
        priceGroupId = h.PriceGroupId,
        inventLocationId = h.InventLocationId,
        inventSiteId = h.InventSiteId,
        createdDateTime = h.CreatedDateTime,
    };
}

[ApiController]
[Route("api/v1/sales-orders/{salesId}")]
[Authorize]
public sealed class LineJobsController : ControllerBase
{
    private readonly LineJobsService _jobs;

    public LineJobsController(LineJobsService jobs) => _jobs = jobs;

    public sealed record FullLineBody(string Company, string ItemNumber, int Quantity);
    public sealed record QuickLine(string Barcode, int Quantity);
    public sealed record QuickBody(string Company, List<QuickLine> Lines);

    [HttpPost("lines/full")]
    public async Task<IActionResult> Full(string salesId, [FromBody] FullLineBody body, CancellationToken ct)
    {
        var user = User.GetUser();
        user.AssertCompanyAccess(body.Company);
        var result = await _jobs.SubmitFullAsync(salesId, body.Company, user.WorkerRecId, body.ItemNumber, body.Quantity, ct);
        var success = result.GetType().GetProperty("success")?.GetValue(result) as bool? ?? false;
        return StatusCode(success ? 201 : 422, ApiEnvelope.Ok(result));
    }

    [HttpPost("lines/quick")]
    public async Task<IActionResult> Quick(string salesId, [FromBody] QuickBody body, CancellationToken ct)
    {
        var user = User.GetUser();
        user.AssertCompanyAccess(body.Company);
        var lines = (body.Lines ?? []).Select(l => (l.Barcode, l.Quantity)).ToList();
        var result = await _jobs.SubmitQuickAsync(salesId, body.Company, user.WorkerRecId, lines, ct);
        var success = result.GetType().GetProperty("success")?.GetValue(result) as bool? ?? false;
        return StatusCode(success ? 201 : 422, ApiEnvelope.Ok(result));
    }

    [HttpGet("failed-lines")]
    public IActionResult Failed(string salesId, [FromQuery] string company, [FromQuery] string? mode)
    {
        var user = User.GetUser();
        user.AssertCompanyAccess(company);
        return Ok(ApiEnvelope.Ok(_jobs.GetFailedLines(salesId, company, mode)));
    }
}

[ApiController]
public sealed class HealthController : ControllerBase
{
    private readonly DynamicsOptions _dynamics;

    public HealthController(IOptions<DynamicsOptions> dynamics) => _dynamics = dynamics.Value;

    [HttpGet("/health")]
    [AllowAnonymous]
    public IActionResult Health() => Ok(new
    {
        ok = true,
        dynamicsMode = _dynamics.IsLiveConfigured ? "live" : "mock",
        liveConfigured = _dynamics.IsLiveConfigured,
        env = Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT") ?? "Production",
    });
}
