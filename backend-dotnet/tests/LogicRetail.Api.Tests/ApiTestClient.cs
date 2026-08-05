using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using FluentAssertions;

namespace LogicRetail.Api.Tests;

internal static class ApiTestClient
{
    public static async Task<HttpClient> CreateAuthedClientAsync(
        MockApiFactory factory,
        string company = "usmf",
        string personnelNumber = "EMP001",
        string password = "1234")
    {
        var client = factory.CreateClient();
        var (_, _, _) = await LoginAsync(client, company, personnelNumber, password);
        return client;
    }

    public static async Task<(HttpClient Client, string AccessToken, string RefreshToken)> LoginAsync(
        HttpClient client,
        string company = "usmf",
        string personnelNumber = "EMP001",
        string password = "1234")
    {
        var login = await PostJsonAsync(client, "/api/v1/auth/login", new
        {
            company,
            personnelNumber,
            password,
        });
        login.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await login.Content.ReadAsStringAsync());
        var data = doc.RootElement.GetProperty("data");
        var access = data.GetProperty("accessToken").GetString()!;
        var refresh = data.GetProperty("refreshToken").GetString()!;
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", access);
        return (client, access, refresh);
    }

    public static Task<HttpResponseMessage> PostJsonAsync(HttpClient client, string url, object body)
    {
        var json = JsonSerializer.Serialize(body);
        return client.PostAsync(url, new StringContent(json, Encoding.UTF8, "application/json"));
    }

    public static async Task<JsonDocument> ReadJsonAsync(HttpResponseMessage response) =>
        JsonDocument.Parse(await response.Content.ReadAsStringAsync());

    public static async Task AssertEnvelopeSuccessAsync(HttpResponseMessage response)
    {
        using var doc = await ReadJsonAsync(response);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeTrue();
    }

    public static async Task AssertEnvelopeErrorAsync(HttpResponseMessage response, string expectedCode)
    {
        using var doc = await ReadJsonAsync(response);
        doc.RootElement.GetProperty("success").GetBoolean().Should().BeFalse();
        doc.RootElement.GetProperty("error").GetProperty("code").GetString().Should().Be(expectedCode);
    }
}
