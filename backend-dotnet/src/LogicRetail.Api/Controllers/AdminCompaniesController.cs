using LogicRetail.Application.Common;
using LogicRetail.Infrastructure.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LogicRetail.Api.Controllers;

[ApiController]
[Route("api/v1/admin/companies")]
[AllowAnonymous] // MVP: open for Super Admin dashboard; lock down with admin auth later
public sealed class AdminCompaniesController : ControllerBase
{
    private readonly CompanyAdminService _companies;

    public AdminCompaniesController(CompanyAdminService companies) => _companies = companies;

    public sealed record CompanyBody(
        string Code,
        string Name,
        string TenantId,
        string ClientId,
        string ClientSecret,
        string FinOpsBaseUrl,
        bool IsActive = true);

    [HttpGet]
    public IActionResult List() => Ok(ApiEnvelope.Ok(_companies.List()));

    [HttpGet("{code}")]
    public IActionResult Get(string code) => Ok(ApiEnvelope.Ok(_companies.Get(code)));

    [HttpPost]
    public IActionResult Create([FromBody] CompanyBody body)
    {
        var data = _companies.Upsert(
            body.Code,
            body.Name,
            body.TenantId,
            body.ClientId,
            body.ClientSecret,
            body.FinOpsBaseUrl,
            body.IsActive);
        return StatusCode(201, ApiEnvelope.Ok(data));
    }

    [HttpPut("{code}")]
    public IActionResult Update(string code, [FromBody] CompanyBody body)
    {
        var data = _companies.Upsert(
            code,
            body.Name,
            body.TenantId,
            body.ClientId,
            body.ClientSecret,
            body.FinOpsBaseUrl,
            body.IsActive);
        return Ok(ApiEnvelope.Ok(data));
    }

    [HttpDelete("{code}")]
    public IActionResult Delete(string code)
    {
        _companies.Delete(code);
        return Ok(ApiEnvelope.Ok(new { ok = true }));
    }
}
