using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using LogicRetail.Application.Common;
using LogicRetail.Application.Contracts;
using LogicRetail.Application.Options;
using LogicRetail.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LogicRetail.Infrastructure.Services;

public sealed class AuthService
{
    private readonly IDynamicsClient _dynamics;
    private readonly IJsonStore _store;
    private readonly CompanyAdminService _companies;
    private readonly JwtOptions _jwt;

    public AuthService(
        IDynamicsClient dynamics,
        IJsonStore store,
        CompanyAdminService companies,
        IOptions<JwtOptions> jwt)
    {
        _dynamics = dynamics;
        _store = store;
        _companies = companies;
        _jwt = jwt.Value;
    }

    public async Task<object> LoginAsync(string company, string personnelNumber, string password, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(company)
            || string.IsNullOrWhiteSpace(personnelNumber)
            || string.IsNullOrWhiteSpace(password))
        {
            throw new AppException("company, personnelNumber and password are required", 400, "VALIDATION_ERROR");
        }

        // Admin registry key (e.g. logic-trial) unlocks Azure/D365 credentials.
        // It may not equal D365 GroupCompany / DataArea (e.g. mm, rest).
        var registryCode = company.Trim();
        _companies.RequireActiveCompany(registryCode);

        IReadOnlyList<RetailUserRow> rows;
        try
        {
            // Authenticate against D365 by personnel + password only, then map legal entities.
            rows = await _dynamics.GetUsersAsync(
                personnelNumber.Trim(),
                password,
                company: null,
                activatedOnly: true,
                ct);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new AppException(ex.Message, 502, "DYNAMICS_ERROR");
        }

        if (rows.Count == 0)
        {
            throw new AppException(
                "Invalid personnel number or password for this company",
                401,
                "AUTH_FAILED");
        }

        var legalEntities = rows
            .GroupBy(r => r.GroupCompany, StringComparer.OrdinalIgnoreCase)
            .Select(g => g.First())
            .ToList();

        var primary = legalEntities.FirstOrDefault(r =>
                string.Equals(r.GroupCompany, registryCode, StringComparison.OrdinalIgnoreCase))
            ?? legalEntities[0];

        var companies = new List<CompanyInfo>
        {
            new()
            {
                Code = primary.GroupCompany,
                Name = primary.GroupCompanyName ?? primary.GroupCompany,
                GroupId = primary.GroupId,
            },
        };
        foreach (var row in legalEntities)
        {
            if (string.Equals(row.GroupCompany, primary.GroupCompany, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            companies.Add(new CompanyInfo
            {
                Code = row.GroupCompany,
                Name = row.GroupCompanyName ?? row.GroupCompany,
                GroupId = row.GroupId,
            });
        }

        var user = new UserSession
        {
            PersonnelNumber = primary.PersonnelNumber,
            WorkerRecId = primary.HcmWorkerRecId,
            Name = primary.Name ?? primary.PersonnelNumber,
            Companies = companies,
        };

        var accessToken = SignAccessToken(user);
        var refreshToken = SignRefreshToken(user.PersonnelNumber, user.Companies[0].Code);
        StoreRefresh(user.PersonnelNumber, refreshToken);

        return new
        {
            accessToken,
            refreshToken,
            user = new
            {
                personnelNumber = user.PersonnelNumber,
                workerRecId = user.WorkerRecId,
                name = user.Name,
                companies = user.Companies.Select(c => new { code = c.Code, name = c.Name, groupId = c.GroupId }),
            },
        };
    }

    public async Task<object> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        ClaimsPrincipal principal;
        try
        {
            principal = ValidateToken(refreshToken);
        }
        catch
        {
            throw new AppException("Invalid refresh token", 401, "UNAUTHORIZED");
        }

        var hash = HashToken(refreshToken);
        var row = _store.FindRefreshToken(hash);
        if (row is null || row.ExpiresAt < DateTimeOffset.UtcNow)
        {
            throw new AppException("Refresh token expired or revoked", 401, "UNAUTHORIZED");
        }

        var personnelNumber = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub)
            ?? throw new AppException("Invalid refresh token", 401, "UNAUTHORIZED");

        var company = principal.FindFirst("company")?.Value;
        var rows = await _dynamics.GetUsersAsync(personnelNumber, null, company, true, ct);
        if (rows.Count == 0)
        {
            throw new AppException("User no longer active", 401, "UNAUTHORIZED");
        }

        var first = rows[0];
        var user = new UserSession
        {
            PersonnelNumber = first.PersonnelNumber,
            WorkerRecId = first.HcmWorkerRecId,
            Name = first.Name ?? first.PersonnelNumber,
            Companies =
            [
                new CompanyInfo
                {
                    Code = first.GroupCompany,
                    Name = first.GroupCompanyName ?? first.GroupCompany,
                    GroupId = first.GroupId,
                },
            ],
        };

        return new
        {
            accessToken = SignAccessToken(user),
            user = new
            {
                personnelNumber = user.PersonnelNumber,
                workerRecId = user.WorkerRecId,
                name = user.Name,
                companies = user.Companies.Select(c => new { code = c.Code, name = c.Name, groupId = c.GroupId }),
            },
        };
    }

    public object Logout(string? refreshToken)
    {
        if (!string.IsNullOrWhiteSpace(refreshToken))
        {
            _store.DeleteRefreshToken(HashToken(refreshToken));
        }

        return new { ok = true };
    }

    private void StoreRefresh(string personnelNumber, string refreshToken)
    {
        _store.InsertRefreshToken(
            Guid.NewGuid().ToString(),
            personnelNumber,
            HashToken(refreshToken),
            DateTimeOffset.UtcNow.Add(ParseDuration(_jwt.RefreshExpiresIn, TimeSpan.FromDays(7))));
    }

    private string SignAccessToken(UserSession user)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.PersonnelNumber),
            new("workerRecId", user.WorkerRecId.ToString()),
            new("name", user.Name),
            new("company", user.Companies[0].Code),
            new("companies", System.Text.Json.JsonSerializer.Serialize(
                user.Companies.Select(c => new { code = c.Code, name = c.Name, groupId = c.GroupId }))),
        };
        return CreateToken(claims, ParseDuration(_jwt.ExpiresIn, TimeSpan.FromHours(8)));
    }

    private string SignRefreshToken(string personnelNumber, string company)
    {
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, personnelNumber),
            new("typ", "refresh"),
            new("company", company),
        };
        return CreateToken(claims, ParseDuration(_jwt.RefreshExpiresIn, TimeSpan.FromDays(7)));
    }

    private string CreateToken(IEnumerable<Claim> claims, TimeSpan lifetime)
    {
        var key = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret));
        var creds = new SigningCredentials(key, SecurityAlgorithms.HmacSha256);
        var token = new JwtSecurityToken(
            issuer: _jwt.Issuer,
            audience: _jwt.Audience,
            claims: claims,
            expires: DateTime.UtcNow.Add(lifetime),
            signingCredentials: creds);
        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    private ClaimsPrincipal ValidateToken(string token)
    {
        var handler = new JwtSecurityTokenHandler();
        return handler.ValidateToken(token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = _jwt.Issuer,
            ValidateAudience = true,
            ValidAudience = _jwt.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(_jwt.Secret)),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.FromMinutes(1),
        }, out _);
    }

    private static string HashToken(string token)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(token));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }

    private static TimeSpan ParseDuration(string value, TimeSpan fallback)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return fallback;
        }

        value = value.Trim().ToLowerInvariant();
        if (value.EndsWith('h') && int.TryParse(value[..^1], out var hours))
        {
            return TimeSpan.FromHours(hours);
        }

        if (value.EndsWith('d') && int.TryParse(value[..^1], out var days))
        {
            return TimeSpan.FromDays(days);
        }

        if (TimeSpan.TryParse(value, out var ts))
        {
            return ts;
        }

        return fallback;
    }
}
