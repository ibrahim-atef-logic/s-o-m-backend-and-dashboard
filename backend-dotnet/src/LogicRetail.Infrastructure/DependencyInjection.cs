using System.Text;
using LogicRetail.Application.Contracts;
using LogicRetail.Application.Options;
using LogicRetail.Infrastructure.Persistence;
using LogicRetail.Infrastructure.Services;
using LogicRetail.Integrations.D365;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace LogicRetail.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddLogicRetailInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DynamicsOptions>(configuration.GetSection(DynamicsOptions.SectionName));
        services.Configure<JwtOptions>(configuration.GetSection(JwtOptions.SectionName));
        services.Configure<StoreOptions>(configuration.GetSection(StoreOptions.SectionName));

        var dynamics = configuration.GetSection(DynamicsOptions.SectionName).Get<DynamicsOptions>()
            ?? new DynamicsOptions();
        var jwt = configuration.GetSection(JwtOptions.SectionName).Get<JwtOptions>()
            ?? new JwtOptions();

        services.AddSingleton<IJsonStore, JsonFileStore>();
        services.AddSingleton<CompanyAdminService>();
        services.AddScoped<AuthService>();
        services.AddScoped<LineJobsService>();

        if (dynamics.IsLiveConfigured)
        {
            services.AddSingleton(new D365Authenticator(
                dynamics.FinOpsBaseUrl,
                dynamics.TenantId,
                dynamics.ClientId,
                dynamics.ClientSecret));
            services.AddSingleton<IDynamicsClient>(sp =>
            {
                var auth = sp.GetRequiredService<D365Authenticator>();
                var http = new HttpClient { Timeout = TimeSpan.FromSeconds(30) };
                var odata = new D365ODataClient(http, auth, dynamics.FinOpsBaseUrl);
                return new LiveDynamicsClient(odata);
            });
        }
        else
        {
            services.AddSingleton<IDynamicsClient, MockDynamicsClient>();
        }

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Secret)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(1),
                };
            });
        services.AddAuthorization();

        return services;
    }
}
