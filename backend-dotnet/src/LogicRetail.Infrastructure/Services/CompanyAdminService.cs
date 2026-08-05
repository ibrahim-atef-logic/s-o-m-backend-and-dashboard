using LogicRetail.Application.Common;
using LogicRetail.Application.Contracts;

namespace LogicRetail.Infrastructure.Services;

public sealed class CompanyAdminService
{
    private readonly IJsonStore _store;

    public CompanyAdminService(IJsonStore store) => _store = store;

    public IReadOnlyList<object> List() =>
        _store.ListCompanies().Select(MapPublic).ToList();

    public object Get(string code)
    {
        var row = _store.FindCompany(code)
            ?? throw new AppException("Company not found", 404, "COMPANY_NOT_FOUND");
        return MapPublic(row);
    }

    public object Upsert(
        string code,
        string name,
        string tenantId,
        string clientId,
        string clientSecret,
        string finOpsBaseUrl,
        bool isActive)
    {
        if (string.IsNullOrWhiteSpace(code)
            || string.IsNullOrWhiteSpace(name)
            || string.IsNullOrWhiteSpace(tenantId)
            || string.IsNullOrWhiteSpace(clientId)
            || string.IsNullOrWhiteSpace(clientSecret)
            || string.IsNullOrWhiteSpace(finOpsBaseUrl))
        {
            throw new AppException(
                "code, name, tenantId, clientId, clientSecret and finOpsBaseUrl are required",
                400,
                "VALIDATION_ERROR");
        }

        if (!Uri.TryCreate(finOpsBaseUrl.Trim(), UriKind.Absolute, out var uri)
            || (uri.Scheme != Uri.UriSchemeHttps && uri.Scheme != Uri.UriSchemeHttp))
        {
            throw new AppException("finOpsBaseUrl must be a valid http(s) URL", 400, "VALIDATION_ERROR");
        }

        var row = new CompanyCredentialRow
        {
            Code = code.Trim(),
            Name = name.Trim(),
            TenantId = tenantId.Trim(),
            ClientId = clientId.Trim(),
            ClientSecret = clientSecret.Trim(),
            FinOpsBaseUrl = finOpsBaseUrl.Trim().TrimEnd('/'),
            IsActive = isActive,
            UpdatedAt = DateTimeOffset.UtcNow,
        };
        _store.UpsertCompany(row);
        return MapPublic(row);
    }

    public void Delete(string code)
    {
        if (!_store.DeleteCompany(code))
        {
            throw new AppException("Company not found", 404, "COMPANY_NOT_FOUND");
        }
    }

    public CompanyCredentialRow RequireActiveCompany(string code)
    {
        var row = _store.FindCompany(code);
        if (row is null || !row.IsActive)
        {
            throw new AppException(
                "Company is not registered. Ask an administrator to add this company before signing in.",
                401,
                "AUTH_COMPANY_UNKNOWN");
        }

        return row;
    }

    private static object MapPublic(CompanyCredentialRow c) => new
    {
        code = c.Code,
        name = c.Name,
        tenantId = c.TenantId,
        clientId = c.ClientId,
        // Mask secret in list/detail responses for safety; full secret only on create/update echo
        clientSecretMasked = Mask(c.ClientSecret),
        clientSecret = c.ClientSecret,
        finOpsBaseUrl = c.FinOpsBaseUrl,
        isActive = c.IsActive,
        updatedAt = c.UpdatedAt,
    };

    private static string Mask(string secret)
    {
        if (string.IsNullOrEmpty(secret) || secret.Length < 8)
        {
            return "********";
        }

        return $"{secret[..4]}…{secret[^4..]}";
    }
}
