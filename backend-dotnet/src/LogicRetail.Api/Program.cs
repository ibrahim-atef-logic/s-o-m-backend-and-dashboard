using LogicRetail.Application.Common;
using LogicRetail.Application.Options;
using LogicRetail.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

// Map familiar env vars from Node .env style
MapEnv(builder.Configuration);

builder.WebHost.UseUrls("http://0.0.0.0:3000");

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddLogicRetailInfrastructure(builder.Configuration);
builder.Services.AddCors(o =>
{
    o.AddDefaultPolicy(p => p.AllowAnyHeader().AllowAnyMethod().AllowAnyOrigin());
});

var app = builder.Build();

app.UseExceptionHandler(errApp =>
{
    errApp.Run(async context =>
    {
        var feature = context.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>();
        var ex = feature?.Error;
        context.Response.ContentType = "application/json";
        if (ex is AppException appEx)
        {
            context.Response.StatusCode = appEx.StatusCode;
            await context.Response.WriteAsJsonAsync(ApiEnvelope.Fail(appEx.Code, appEx.Message));
            return;
        }

        context.Response.StatusCode = 500;
        await context.Response.WriteAsJsonAsync(ApiEnvelope.Fail("INTERNAL", ex?.Message ?? "Unexpected error"));
    });
});

if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
}

app.UseCors();
app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

app.Run();

static void MapEnv(ConfigurationManager config)
{
    void Set(string key, string? value)
    {
        if (!string.IsNullOrWhiteSpace(value))
        {
            config[key] = value;
        }
    }

    Set("Dynamics:Mode", Environment.GetEnvironmentVariable("DYNAMICS_MODE"));
    Set("Dynamics:FinOpsBaseUrl", Environment.GetEnvironmentVariable("FINOPS_BASE_URL"));
    Set("Dynamics:TenantId", Environment.GetEnvironmentVariable("AZURE_TENANT_ID"));
    Set("Dynamics:ClientId", Environment.GetEnvironmentVariable("AZURE_CLIENT_ID"));
    Set("Dynamics:ClientSecret", Environment.GetEnvironmentVariable("AZURE_CLIENT_SECRET"));
    Set("Jwt:Secret", Environment.GetEnvironmentVariable("JWT_SECRET"));
    Set("Jwt:ExpiresIn", Environment.GetEnvironmentVariable("JWT_EXPIRES_IN"));
    Set("Jwt:RefreshExpiresIn", Environment.GetEnvironmentVariable("JWT_REFRESH_EXPIRES_IN"));
    Set("Store:Path", Environment.GetEnvironmentVariable("STORE_PATH"));
}

public partial class Program;
