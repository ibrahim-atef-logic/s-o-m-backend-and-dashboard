using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.Identity.Client;

namespace LogicRetail.Integrations.D365;

public sealed class D365Authenticator
{
    private readonly string _environmentUrl;
    private readonly string _tenantId;
    private readonly string _clientId;
    private readonly string _clientSecret;
    private readonly string[] _scopes;
    private readonly SemaphoreSlim _lock = new(1, 1);
    private string? _token;
    private DateTimeOffset _expiresOnUtc;

    public D365Authenticator(string environmentUrl, string tenantId, string clientId, string clientSecret)
    {
        _environmentUrl = environmentUrl.TrimEnd('/');
        _tenantId = tenantId;
        _clientId = clientId;
        _clientSecret = clientSecret;
        _scopes = [$"{_environmentUrl}/.default"];
    }

    public void Invalidate()
    {
        _lock.Wait();
        try
        {
            _token = null;
            _expiresOnUtc = DateTimeOffset.MinValue;
        }
        finally
        {
            _lock.Release();
        }
    }

    public async Task<string> GetAccessTokenAsync(CancellationToken cancellationToken = default)
    {
        await _lock.WaitAsync(cancellationToken);
        try
        {
            if (!string.IsNullOrWhiteSpace(_token) && DateTimeOffset.UtcNow < _expiresOnUtc.AddSeconds(-60))
            {
                return _token!;
            }
        }
        finally
        {
            _lock.Release();
        }

        var app = ConfidentialClientApplicationBuilder
            .Create(_clientId)
            .WithClientSecret(_clientSecret)
            .WithAuthority($"https://login.microsoftonline.com/{_tenantId}")
            .Build();

        var result = await app.AcquireTokenForClient(_scopes).ExecuteAsync(cancellationToken);

        await _lock.WaitAsync(cancellationToken);
        try
        {
            _token = result.AccessToken;
            _expiresOnUtc = result.ExpiresOn;
            return _token;
        }
        finally
        {
            _lock.Release();
        }
    }
}

public sealed class D365ODataException : Exception
{
    public D365ODataException(string message, int statusCode) : base(message)
    {
        StatusCode = statusCode;
    }

    public int StatusCode { get; }
}

public sealed class D365ODataClient
{
    private readonly HttpClient _http;
    private readonly D365Authenticator _auth;
    private readonly string _baseUrl;

    public D365ODataClient(HttpClient http, D365Authenticator auth, string environmentUrl)
    {
        _http = http;
        _auth = auth;
        _baseUrl = $"{environmentUrl.TrimEnd('/')}/data";
    }

    public async Task<IReadOnlyList<JsonElement>> QueryAsync(
        string entitySet,
        string? filter = null,
        CancellationToken cancellationToken = default,
        bool crossCompany = false)
    {
        return await SendWithRetryAsync<IReadOnlyList<JsonElement>>(async token =>
        {
            var url = new StringBuilder($"{_baseUrl}/{entitySet}");
            var query = new List<string>();
            if (crossCompany)
            {
                query.Add("cross-company=true");
            }

            if (!string.IsNullOrWhiteSpace(filter))
            {
                query.Add($"$filter={Uri.EscapeDataString(filter)}");
            }

            if (query.Count > 0)
            {
                url.Append('?').Append(string.Join('&', query));
            }

            using var req = new HttpRequestMessage(HttpMethod.Get, url.ToString());
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            req.Headers.TryAddWithoutValidation("Accept", "application/json");
            using var res = await _http.SendAsync(req, cancellationToken);
            var body = await res.Content.ReadAsStringAsync(cancellationToken);
            if (!res.IsSuccessStatusCode)
            {
                throw MapError(res.StatusCode, body);
            }

            using var doc = JsonDocument.Parse(body);
            if (!doc.RootElement.TryGetProperty("value", out var value) || value.ValueKind != JsonValueKind.Array)
            {
                return Array.Empty<JsonElement>();
            }

            return value.EnumerateArray().Select(e => e.Clone()).ToList();
        }, cancellationToken);
    }

    public async Task PostAsync(
        string entitySet,
        object payload,
        CancellationToken cancellationToken = default)
    {
        await SendWithRetryAsync(async token =>
        {
            var json = JsonSerializer.Serialize(payload);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/{entitySet}")
            {
                Content = new StringContent(json, Encoding.UTF8, "application/json"),
            };
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var res = await _http.SendAsync(req, cancellationToken);
            var body = await res.Content.ReadAsStringAsync(cancellationToken);
            if (!res.IsSuccessStatusCode)
            {
                throw MapError(res.StatusCode, body);
            }

            return 0;
        }, cancellationToken);
    }

    private async Task<T> SendWithRetryAsync<T>(Func<string, Task<T>> action, CancellationToken cancellationToken)
    {
        var token = await _auth.GetAccessTokenAsync(cancellationToken);
        try
        {
            return await action(token);
        }
        catch (D365ODataException ex) when (ex.StatusCode == 401)
        {
            _auth.Invalidate();
            token = await _auth.GetAccessTokenAsync(cancellationToken);
            return await action(token);
        }
    }

    private static D365ODataException MapError(HttpStatusCode status, string body)
    {
        var code = (int)status;
        var message = body;
        try
        {
            using var doc = JsonDocument.Parse(body);
            if (doc.RootElement.TryGetProperty("error", out var error))
            {
                if (error.TryGetProperty("innererror", out var inner)
                    && inner.TryGetProperty("message", out var innerMsg))
                {
                    message = innerMsg.GetString() ?? message;
                }
                else if (error.TryGetProperty("message", out var msg))
                {
                    message = msg.GetString() ?? message;
                }
            }
        }
        catch
        {
            // keep raw body
        }

        return new D365ODataException(message, code);
    }
}

public static class ODataEscaper
{
    public static string String(string value) => value.Replace("'", "''", StringComparison.Ordinal);
}
