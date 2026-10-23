using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;

namespace Verx.Web.Security;

public static class AuthenticationExtensions
{
    public static IServiceCollection AddVerxSecurity(this IServiceCollection services, IConfiguration configuration)
    {
        var keycloak = configuration.GetSection("Keycloak");

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = keycloak["Authority"];
                options.RequireHttpsMetadata = keycloak.GetValue("RequireHttpsMetadata", true);
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = keycloak["Issuer"] ?? keycloak["Authority"],
                    ValidateAudience = true,
                    ValidAudience = keycloak["Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30)
                };
            });

        services.AddSingleton<IAuthorizationHandler, ContaAuthorizationHandler>();
        services.AddAuthorizationBuilder()
            .AddPolicy(ContaClaims.PolicyAcessoConta, policy => policy.AddRequirements(new ContaRequirement()));

        return services;
    }
}
