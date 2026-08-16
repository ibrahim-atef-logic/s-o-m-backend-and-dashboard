using System.Net;
using FluentAssertions;

namespace LogicRetail.Api.Tests;

public sealed class AuthEndpointTests : IClassFixture<MockApiFactory>
{
    private readonly MockApiFactory _factory;

    public AuthEndpointTests(MockApiFactory factory) => _factory = factory;

    [Fact]
    public async Task Post_login_success_returns_tokens_scoped_to_company()
    {
        var client = _factory.CreateClient();
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/auth/login", new
        {
            company = "usmf",
            personnelNumber = "EMP001",
            password = "1234",
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        data.GetProperty("refreshToken").GetString().Should().NotBeNullOrWhiteSpace();
        data.GetProperty("user").GetProperty("personnelNumber").GetString().Should().Be("EMP001");
        data.GetProperty("user").GetProperty("companies")[0].GetProperty("code").GetString().Should().Be("usmf");
        data.GetProperty("user").GetProperty("activeCompany").GetString().Should().Be("usmf");
        data.GetProperty("user").GetProperty("activeWarehouse").GetString().Should().Be("11");
        data.GetProperty("user").GetProperty("needsWarehouseSelection").GetBoolean().Should().BeFalse();
    }

    /// <summary>
    /// Emulator login form values: company=logic-trial, personnel=1006, password=123.
    /// </summary>
    [Fact]
    public async Task Post_login_logic_trial_1006_succeeds()
    {
        var client = _factory.CreateClient();
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/auth/login", new
        {
            company = "logic-trial",
            personnelNumber = "1006",
            password = "123",
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        var data = doc.RootElement.GetProperty("data");
        data.GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
        data.GetProperty("user").GetProperty("personnelNumber").GetString().Should().Be("1006");
        data.GetProperty("user").GetProperty("name").GetString().Should().Be("محمد عفيف");
        data.GetProperty("user").GetProperty("userId").GetString().Should().Be("m.afif");
        data.GetProperty("user").GetProperty("activeCompany").GetString().Should().Be("mm");
        data.GetProperty("user").GetProperty("activeWarehouse").GetString().Should().Be("MMS000WH");
        data.GetProperty("user").GetProperty("retailChannelId").GetString().Should().Be("912");
        data.GetProperty("user").GetProperty("currency").GetString().Should().Be("SAR");
        data.GetProperty("user").GetProperty("defaultCustAccount").GetString().Should().Be("10-10002");
        data.GetProperty("user").GetProperty("needsWarehouseSelection").GetBoolean().Should().BeFalse();
        data.GetProperty("user").GetProperty("companies")[0].GetProperty("code").GetString()
            .Should().Be("mm");
    }

    [Fact]
    public async Task Post_login_12344_needs_warehouse_selection()
    {
        var client = _factory.CreateClient();
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/auth/login", new
        {
            company = "logic-trial",
            personnelNumber = "12344",
            password = "123",
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        var user = doc.RootElement.GetProperty("data").GetProperty("user");
        user.GetProperty("personnelNumber").GetString().Should().Be("12344");
        user.GetProperty("activeCompany").GetString().Should().Be("PLTR");
        user.GetProperty("needsWarehouseSelection").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Post_login_disabled_returns_account_disabled()
    {
        var client = _factory.CreateClient();
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/auth/login", new
        {
            company = "usmf",
            personnelNumber = "DISABLED",
            password = "123",
        });

        res.StatusCode.Should().Be(HttpStatusCode.Forbidden);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "ACCOUNT_DISABLED");
    }

    [Fact]
    public async Task Post_login_wrong_password_returns_auth_failed()
    {
        var client = _factory.CreateClient();
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/auth/login", new
        {
            company = "usmf",
            personnelNumber = "EMP001",
            password = "wrong",
        });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "AUTH_FAILED");
    }

    [Fact]
    public async Task Post_login_wrong_company_returns_company_unknown()
    {
        var client = _factory.CreateClient();
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/auth/login", new
        {
            company = "xxzz",
            personnelNumber = "EMP001",
            password = "1234",
        });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "AUTH_COMPANY_UNKNOWN");
    }

    [Fact]
    public async Task Post_login_wrong_password_for_known_company_returns_auth_failed()
    {
        var client = _factory.CreateClient();
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/auth/login", new
        {
            company = "logic-trial",
            personnelNumber = "1006",
            password = "wrong",
        });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "AUTH_FAILED");
    }

    [Fact]
    public async Task Post_login_missing_fields_returns_validation_error()
    {
        var client = _factory.CreateClient();
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/auth/login", new
        {
            company = "",
            personnelNumber = "EMP001",
            password = "1234",
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "VALIDATION_ERROR");
    }

    [Fact]
    public async Task Post_refresh_returns_new_tokens()
    {
        var client = _factory.CreateClient();
        var (_, _, refresh) = await ApiTestClient.LoginAsync(client);

        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/auth/refresh", new
        {
            refreshToken = refresh,
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        doc.RootElement.GetProperty("data").GetProperty("accessToken").GetString().Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Post_refresh_invalid_token_returns_unauthorized()
    {
        var client = _factory.CreateClient();
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/auth/refresh", new
        {
            refreshToken = "not-a-real-token",
        });

        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "UNAUTHORIZED");
    }

    [Fact]
    public async Task Post_logout_revokes_refresh_token()
    {
        var client = _factory.CreateClient();
        var (_, _, refresh) = await ApiTestClient.LoginAsync(client);

        var logout = await ApiTestClient.PostJsonAsync(client, "/api/v1/auth/logout", new
        {
            refreshToken = refresh,
        });
        logout.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(logout);

        var refreshRes = await ApiTestClient.PostJsonAsync(client, "/api/v1/auth/refresh", new
        {
            refreshToken = refresh,
        });
        refreshRes.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Get_me_returns_current_user()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await client.GetAsync("/api/v1/auth/me");

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        var me = doc.RootElement.GetProperty("data");
        me.GetProperty("personnelNumber").GetString().Should().Be("EMP001");
        me.GetProperty("activeCompany").GetString().Should().Be("usmf");
        me.GetProperty("activeWarehouse").GetString().Should().Be("11");
        me.GetProperty("defaultCustAccount").GetString().Should().Be("US-001");
    }

    [Fact]
    public async Task Post_change_password_wrong_old_returns_failed()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/auth/change-password", new
        {
            oldPassword = "wrong",
            newPassword = "9999",
        });

        res.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        await ApiTestClient.AssertEnvelopeErrorAsync(res, "PASSWORD_CHANGE_FAILED");
    }

    [Fact]
    public async Task Post_change_password_success()
    {
        var client = await ApiTestClient.CreateAuthedClientAsync(_factory);
        var res = await ApiTestClient.PostJsonAsync(client, "/api/v1/auth/change-password", new
        {
            oldPassword = "1234",
            newPassword = "9999",
        });

        res.StatusCode.Should().Be(HttpStatusCode.OK);
        await ApiTestClient.AssertEnvelopeSuccessAsync(res);
        using var doc = await ApiTestClient.ReadJsonAsync(res);
        doc.RootElement.GetProperty("data").GetProperty("isSuccess").GetBoolean().Should().BeTrue();
    }

    [Fact]
    public async Task Get_me_without_token_returns_unauthorized()
    {
        var client = _factory.CreateClient();
        var res = await client.GetAsync("/api/v1/auth/me");
        res.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}
