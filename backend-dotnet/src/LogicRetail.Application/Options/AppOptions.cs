namespace LogicRetail.Application.Options;

public sealed class DynamicsOptions
{
    public const string SectionName = "Dynamics";
    public string Mode { get; set; } = "Mock"; // Mock | Live
    public string FinOpsBaseUrl { get; set; } = string.Empty;
    public string TenantId { get; set; } = string.Empty;
    public string ClientId { get; set; } = string.Empty;
    public string ClientSecret { get; set; } = string.Empty;

    public bool IsLive => string.Equals(Mode, "Live", StringComparison.OrdinalIgnoreCase);
    public bool IsLiveConfigured =>
        IsLive
        && !string.IsNullOrWhiteSpace(FinOpsBaseUrl)
        && !string.IsNullOrWhiteSpace(TenantId)
        && !string.IsNullOrWhiteSpace(ClientId)
        && !string.IsNullOrWhiteSpace(ClientSecret);
}

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Secret { get; set; } = "dev-change-me-logic-retail-secret-min-32-chars!!";
    public string Issuer { get; set; } = "LogicRetail";
    public string Audience { get; set; } = "LogicRetail.Mobile";
    public string ExpiresIn { get; set; } = "8h";
    public string RefreshExpiresIn { get; set; } = "7d";
}

public sealed class StoreOptions
{
    public const string SectionName = "Store";
    public string Path { get; set; } = "data/store.json";
}
