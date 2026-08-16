using FluentAssertions;
using LogicRetail.Application.Common;
using LogicRetail.Application.Options;
using LogicRetail.Infrastructure.Persistence;
using LogicRetail.Infrastructure.Services;
using LogicRetail.Integrations.D365;
using Microsoft.Extensions.Options;

namespace LogicRetail.Unit.Tests;

public sealed class AuthServiceTests : IDisposable
{
    private readonly string _storePath = Path.Combine(Path.GetTempPath(), $"auth-store-{Guid.NewGuid():N}.json");
    private readonly AuthService _sut;

    public AuthServiceTests()
    {
        var store = new JsonFileStore(Options.Create(new StoreOptions { Path = _storePath }));
        var companies = new CompanyAdminService(store);
        var jwt = Options.Create(new JwtOptions
        {
            Secret = "unit-test-secret-logic-retail-min-32-chars!!",
            Issuer = "LogicRetail",
            Audience = "LogicRetail.Mobile",
            ExpiresIn = "1h",
            RefreshExpiresIn = "7d",
        });
        _sut = new AuthService(new MockDynamicsClient(), store, companies, jwt);
    }

    public void Dispose()
    {
        try
        {
            if (File.Exists(_storePath))
            {
                File.Delete(_storePath);
            }
        }
        catch
        {
            // ignore
        }
    }

    [Fact]
    public async Task Login_logic_trial_1006_returns_tokens()
    {
        var result = await _sut.LoginAsync("logic-trial", "1006", "123", CancellationToken.None);
        result.Should().NotBeNull();
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        json.Should().Contain("accessToken");
        json.Should().Contain("1006");
        json.Should().Contain("MMS000WH");
        json.Should().Contain("\"activeCompany\":\"mm\"");
    }

    [Fact]
    public async Task Login_12344_succeeds_with_warehouse_selection_flag()
    {
        var result = await _sut.LoginAsync("logic-trial", "12344", "123", CancellationToken.None);
        using var doc = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(result));
        var user = doc.RootElement.GetProperty("user");
        user.GetProperty("personnelNumber").GetString().Should().Be("12344");
        user.GetProperty("activeCompany").GetString().Should().Be("PLTR");
        user.GetProperty("needsWarehouseSelection").GetBoolean().Should().BeTrue();
        user.GetProperty("activeWarehouse").ValueKind.Should().Be(System.Text.Json.JsonValueKind.Null);
    }

    [Fact]
    public async Task Login_disabled_account_throws_ACCOUNT_DISABLED()
    {
        var act = () => _sut.LoginAsync("usmf", "DISABLED", "123", CancellationToken.None);
        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.Code.Should().Be("ACCOUNT_DISABLED");
        ex.Which.StatusCode.Should().Be(403);
    }

    [Fact]
    public async Task ChangePassword_wrong_old_throws_PASSWORD_CHANGE_FAILED()
    {
        var act = () => _sut.ChangePasswordAsync("1006", "bad", "1234", CancellationToken.None);
        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.Code.Should().Be("PASSWORD_CHANGE_FAILED");
        ex.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task ChangePassword_success_returns_message()
    {
        var result = await _sut.ChangePasswordAsync("1006", "123", "1234", CancellationToken.None);
        var json = System.Text.Json.JsonSerializer.Serialize(result);
        json.Should().Contain("Password changed successfully");
        json.Should().Contain("\"isSuccess\":true");
    }

    [Fact]
    public async Task Login_unknown_company_throws_AUTH_COMPANY_UNKNOWN()
    {
        var act = () => _sut.LoginAsync("no-such-co", "1006", "123", CancellationToken.None);
        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.Code.Should().Be("AUTH_COMPANY_UNKNOWN");
        ex.Which.StatusCode.Should().Be(401);
    }

    [Fact]
    public async Task Login_wrong_password_throws_AUTH_FAILED()
    {
        var act = () => _sut.LoginAsync("logic-trial", "1006", "bad", CancellationToken.None);
        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.Code.Should().Be("AUTH_FAILED");
    }

    [Fact]
    public async Task Login_missing_fields_throws_VALIDATION_ERROR()
    {
        var act = () => _sut.LoginAsync("", "1006", "123", CancellationToken.None);
        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.Code.Should().Be("VALIDATION_ERROR");
        ex.Which.StatusCode.Should().Be(400);
    }

    [Fact]
    public async Task Refresh_after_login_returns_new_access_token()
    {
        var loginObj = await _sut.LoginAsync("usmf", "EMP001", "1234", CancellationToken.None);
        var loginJson = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(loginObj));
        var refresh = loginJson.RootElement.GetProperty("refreshToken").GetString()!;

        var refreshed = await _sut.RefreshAsync(refresh, CancellationToken.None);
        var refreshJson = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(refreshed));
        refreshJson.RootElement.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Logout_revokes_refresh_token()
    {
        var loginObj = await _sut.LoginAsync("usmf", "EMP001", "1234", CancellationToken.None);
        var loginJson = System.Text.Json.JsonDocument.Parse(System.Text.Json.JsonSerializer.Serialize(loginObj));
        var refresh = loginJson.RootElement.GetProperty("refreshToken").GetString()!;

        _sut.Logout(refresh);

        var act = () => _sut.RefreshAsync(refresh, CancellationToken.None);
        var ex = await act.Should().ThrowAsync<AppException>();
        ex.Which.Code.Should().Be("UNAUTHORIZED");
    }
}
