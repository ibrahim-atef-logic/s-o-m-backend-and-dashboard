using System.Security.Claims;
using LogicRetail.Application.Common;
using LogicRetail.Domain;
using LogicRetail.Infrastructure.Services;

namespace LogicRetail.Api;

public static class AuthUserExtensions
{
    public static UserSession GetUser(this ClaimsPrincipal user) =>
        AuthService.ReadSession(user)
        ?? throw new AppException("Unauthorized", 401, "UNAUTHORIZED");

    public static void AssertCompanyAccess(this UserSession session, string? company)
    {
        if (string.IsNullOrWhiteSpace(company))
        {
            throw new AppException("company is required", 400, "VALIDATION_ERROR");
        }

        var allowed = session.Companies.Any(c =>
            string.Equals(c.Code, company, StringComparison.OrdinalIgnoreCase))
            || string.Equals(session.ActiveCompany, company, StringComparison.OrdinalIgnoreCase);
        if (!allowed)
        {
            throw new AppException("Company not allowed for this user", 403, "FORBIDDEN_COMPANY");
        }
    }
}
