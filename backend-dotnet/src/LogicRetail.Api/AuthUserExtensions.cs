using System.Security.Claims;
using System.Text.Json;
using LogicRetail.Application.Common;
using LogicRetail.Domain;

namespace LogicRetail.Api;

public static class AuthUserExtensions
{
    public static UserSession GetUser(this ClaimsPrincipal user)
    {
        var personnel = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub")
            ?? throw new AppException("Unauthorized", 401, "UNAUTHORIZED");
        var worker = long.TryParse(user.FindFirstValue("workerRecId"), out var w) ? w : 0;
        var name = user.FindFirstValue("name") ?? personnel;
        var companiesJson = user.FindFirstValue("companies") ?? "[]";
        var companies = JsonSerializer.Deserialize<List<CompanyDto>>(
            companiesJson,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true }) ?? [];
        var company = user.FindFirstValue("company");
        if (companies.Count == 0 && !string.IsNullOrWhiteSpace(company))
        {
            companies.Add(new CompanyDto { Code = company, Name = company });
        }

        return new UserSession
        {
            PersonnelNumber = personnel,
            WorkerRecId = worker,
            Name = name,
            Companies = companies.Select(c => new CompanyInfo
            {
                Code = c.Code ?? string.Empty,
                Name = c.Name ?? c.Code ?? string.Empty,
                GroupId = c.GroupId,
            }).ToList(),
        };
    }

    public static void AssertCompanyAccess(this UserSession session, string? company)
    {
        if (string.IsNullOrWhiteSpace(company))
        {
            throw new AppException("company is required", 400, "VALIDATION_ERROR");
        }

        var allowed = session.Companies.Any(c =>
            string.Equals(c.Code, company, StringComparison.OrdinalIgnoreCase));
        if (!allowed)
        {
            throw new AppException("Company not allowed for this user", 403, "FORBIDDEN_COMPANY");
        }
    }

    private sealed class CompanyDto
    {
        public string? Code { get; set; }
        public string? Name { get; set; }
        public string? GroupId { get; set; }
    }
}
