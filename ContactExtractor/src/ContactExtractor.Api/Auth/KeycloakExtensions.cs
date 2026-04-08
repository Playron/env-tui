using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;

namespace ContactExtractor.Api.Auth;

public static class KeycloakExtensions
{
    public static IServiceCollection AddKeycloakAuth(
        this IServiceCollection services, IConfiguration config)
    {
        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.Authority = config["Keycloak:Authority"];
                options.Audience  = config["Keycloak:Audience"];
                options.RequireHttpsMetadata = false; // Dev only
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    ValidateAudience = true,
                    ValidateLifetime = true,
                    NameClaimType = "preferred_username",
                    RoleClaimType = "realm_access.roles"
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOnly",     p => p.RequireRole("admin"))
            .AddPolicy("Authenticated", p => p.RequireAuthenticatedUser());

        services.AddHttpContextAccessor();
        services.AddScoped<CurrentUserService>();
        return services;
    }
}
