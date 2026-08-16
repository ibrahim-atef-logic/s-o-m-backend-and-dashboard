using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using LogicRetail.Application.Common;
using LogicRetail.Application.Contracts;
using LogicRetail.Application.Options;
using LogicRetail.Domain;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace LogicRetail.Infrastructure.Services;

public sealed class AuthService
{
    internal static readonly JsonSerializerOptions ProfileJson = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
    };

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

        // Admin registry key (e.g. logic-trial) unlocks the environment.
        // Operating DataArea comes from AuthenticateUser (InventLocationDataAreaId / Company).
        _companies.RequireActiveCompany(company.Trim());

        MobileAuthPayload payload;
        try
        {
            payload = await _dynamics.AuthenticateUserAsync(personnelNumber.Trim(), password, ct);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new AppException(ex.Message, 502, "DYNAMICS_ERROR");
        }

        var user = SessionFromPayload(payload);
        var accessToken = SignAccessToken(user);
        var refreshToken = SignRefreshToken(user);
        StoreRefresh(user.PersonnelNumber, refreshToken);

        return new
        {
            accessToken,
            refreshToken,
            user = Describe(user),
        };
    }

    public async Task<object> RefreshAsync(string refreshToken, CancellationToken ct)
    {
        _ = ct;
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

        var user = ReadSession(principal)
            ?? throw new AppException("Invalid refresh token", 401, "UNAUTHORIZED");

        if (!user.IsActive || !user.UserInfoEnable)
        {
            throw new AppException("User no longer active", 401, "UNAUTHORIZED");
        }

        return new
        {
            accessToken = SignAccessToken(user),
            user = Describe(user),
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

    public async Task<object> ChangePasswordAsync(
        string personnelNumber,
        string oldPassword,
        string newPassword,
        CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(oldPassword) || string.IsNullOrWhiteSpace(newPassword))
        {
            throw new AppException("oldPassword and newPassword are required", 400, "VALIDATION_ERROR");
        }

        PasswordChangeResult result;
        try
        {
            result = await _dynamics.ChangePasswordAsync(personnelNumber, oldPassword, newPassword, ct);
        }
        catch (AppException)
        {
            throw;
        }
        catch (Exception ex)
        {
            throw new AppException(ex.Message, 502, "DYNAMICS_ERROR");
        }

        if (!result.IsSuccess)
        {
            throw new AppException(
                string.IsNullOrWhiteSpace(result.Message)
                    ? "Password change failed"
                    : result.Message,
                400,
                "PASSWORD_CHANGE_FAILED");
        }

        return new
        {
            isSuccess = result.IsSuccess,
            message = result.Message,
            activationRecId = result.ActivationRecId,
        };
    }

    public object Describe(UserSession user) => MapUser(user);

    internal static UserSession SessionFromPayload(MobileAuthPayload payload)
    {
        if (!payload.IsSuccess)
        {
            throw new AppException(
                string.IsNullOrWhiteSpace(payload.Message)
                    ? "Invalid personnel number or password"
                    : payload.Message,
                401,
                "AUTH_FAILED");
        }

        if (!payload.IsActive || !payload.UserInfoEnable)
        {
            throw new AppException(
                "Account is inactive or disabled in D365.",
                403,
                "ACCOUNT_DISABLED");
        }

        var activeCompany = payload.ActiveCompany;
        if (string.IsNullOrWhiteSpace(activeCompany))
        {
            throw new AppException("No company assigned to this account in D365.", 403, "NO_COMPANY");
        }

        return new UserSession
        {
            PersonnelNumber = payload.PersonnelNumber,
            WorkerRecId = payload.HcmWorkerRecId,
            Name = payload.WorkerName ?? payload.PersonnelNumber,
            UserId = payload.UserId,
            ActivationRecId = payload.ActivationRecId,
            IsActive = payload.IsActive,
            UserInfoEnable = payload.UserInfoEnable,
            RetailChannelTableRecId = payload.RetailChannelTableRecId,
            RetailChannelId = payload.RetailChannelId,
            ChannelType = payload.ChannelType,
            InventLocation = payload.InventLocation,
            InventLocationDataAreaId = payload.InventLocationDataAreaId,
            Currency = payload.Currency,
            DefaultCustAccount = payload.DefaultCustAccount,
            DefaultCustDataAreaId = payload.DefaultCustDataAreaId,
            ActiveCompany = activeCompany,
            ActiveWarehouse = payload.ActiveWarehouse,
            NeedsWarehouseSelection = payload.NeedsWarehouseSelection,
            Companies =
            [
                new CompanyInfo
                {
                    Code = activeCompany,
                    Name = payload.Company ?? activeCompany,
                },
            ],
        };
    }

    internal static object MapUser(UserSession user) => new
    {
        personnelNumber = user.PersonnelNumber,
        workerRecId = user.WorkerRecId,
        name = user.Name,
        userId = user.UserId,
        activationRecId = user.ActivationRecId,
        isActive = user.IsActive,
        userInfoEnable = user.UserInfoEnable,
        company = user.ActiveCompany,
        companies = user.Companies.Select(c => new { code = c.Code, name = c.Name, groupId = c.GroupId }),
        retailChannelTableRecId = user.RetailChannelTableRecId,
        retailChannelId = user.RetailChannelId,
        channelType = user.ChannelType,
        inventLocation = user.InventLocation,
        inventLocationDataAreaId = user.InventLocationDataAreaId,
        currency = user.Currency,
        defaultCustAccount = user.DefaultCustAccount,
        defaultCustDataAreaId = user.DefaultCustDataAreaId,
        activeCompany = user.ActiveCompany,
        activeWarehouse = user.ActiveWarehouse,
        needsWarehouseSelection = user.NeedsWarehouseSelection,
    };

    public static UserSession? ReadSession(ClaimsPrincipal principal)
    {
        var profileJson = principal.FindFirstValue("profile");
        if (!string.IsNullOrWhiteSpace(profileJson))
        {
            try
            {
                var fromProfile = JsonSerializer.Deserialize<UserSession>(profileJson, ProfileJson);
                if (fromProfile is not null && !string.IsNullOrWhiteSpace(fromProfile.PersonnelNumber))
                {
                    return fromProfile;
                }
            }
            catch (JsonException)
            {
                // fall through to legacy claims
            }
        }

        var personnel = principal.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? principal.FindFirstValue(JwtRegisteredClaimNames.Sub);
        if (string.IsNullOrWhiteSpace(personnel))
        {
            return null;
        }

        var worker = long.TryParse(principal.FindFirstValue("workerRecId"), out var w) ? w : 0;
        var name = principal.FindFirstValue("name") ?? personnel;
        var company = principal.FindFirstValue("company") ?? string.Empty;
        var companiesJson = principal.FindFirstValue("companies") ?? "[]";
        var companies = JsonSerializer.Deserialize<List<CompanyInfo>>(
            companiesJson,
            ProfileJson) ?? [];
        if (companies.Count == 0 && !string.IsNullOrWhiteSpace(company))
        {
            companies.Add(new CompanyInfo { Code = company, Name = company });
        }

        return new UserSession
        {
            PersonnelNumber = personnel,
            WorkerRecId = worker,
            Name = name,
            Companies = companies,
            ActiveCompany = company,
            IsActive = true,
            UserInfoEnable = true,
        };
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
        return CreateToken(BuildSessionClaims(user), ParseDuration(_jwt.ExpiresIn, TimeSpan.FromHours(8)));
    }

    private string SignRefreshToken(UserSession user)
    {
        var claims = BuildSessionClaims(user);
        claims.Add(new Claim("typ", "refresh"));
        return CreateToken(claims, ParseDuration(_jwt.RefreshExpiresIn, TimeSpan.FromDays(7)));
    }

    private static List<Claim> BuildSessionClaims(UserSession user)
    {
        return
        [
            new(JwtRegisteredClaimNames.Sub, user.PersonnelNumber),
            new("workerRecId", user.WorkerRecId.ToString()),
            new("name", user.Name),
            new("company", user.ActiveCompany),
            new("companies", JsonSerializer.Serialize(
                user.Companies.Select(c => new { code = c.Code, name = c.Name, groupId = c.GroupId }))),
            new("profile", JsonSerializer.Serialize(user, ProfileJson)),
        ];
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
