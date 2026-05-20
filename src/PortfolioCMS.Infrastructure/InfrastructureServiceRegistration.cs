using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.IdentityModel.Tokens;
using PortfolioCMS.Application.Common.Interfaces;

using PortfolioCMS.Infrastructure.Persistence;
using PortfolioCMS.Infrastructure.Services;
using System.Text;

namespace PortfolioCMS.Infrastructure;

public static class InfrastructureServiceRegistration
{
    public static IServiceCollection AddInfrastructureServices(
        this IServiceCollection services, IConfiguration config)
    {
        // ── Database ──────────────────────────────────────────────────────────
        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlite(config.GetConnectionString("DefaultConnection")));

        services.AddScoped<IAppDbContext>(sp => sp.GetRequiredService<AppDbContext>());

        // ── Auth services ─────────────────────────────────────────────────────
        services.AddScoped<ITokenService, TokenService>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IAuditService, AuditService>();

        // ── Storage: use Azure in production, local in development ─────────────
        var useAzureStorage = !string.IsNullOrEmpty(config["Azure:StorageConnectionString"]);
        if (useAzureStorage)
            services.AddScoped<IImageStorageService, AzureBlobStorageService>();
        else
            services.AddScoped<IImageStorageService, LocalImageStorageService>();

        // ── AI service ────────────────────────────────────────────────────────
        var hasAi = !string.IsNullOrEmpty(config["AzureOpenAI:ApiKey"])
                 || !string.IsNullOrEmpty(config["OpenAI:ApiKey"]);
        if (hasAi)
            services.AddScoped<IAiService, SemanticKernelAiService>();

        // ── SignalR ───────────────────────────────────────────────────────────
        services.AddSignalR();
        services.AddScoped<IPortfolioNotificationService, PortfolioNotificationService>();

        // ── JWT Authentication ─────────────────────────────────────────────────
        services
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(
                        Encoding.UTF8.GetBytes(config["Jwt:Secret"]!)),
                    ValidateIssuer = true,
                    ValidIssuer = config["Jwt:Issuer"],
                    ValidateAudience = true,
                    ValidAudience = config["Jwt:Audience"],
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.Zero
                };

                // Allow JWT from SignalR query string (for hub connections)
                options.Events = new JwtBearerEvents
                {
                    OnMessageReceived = ctx =>
                    {
                        var token = ctx.Request.Query["access_token"];
                        var path = ctx.HttpContext.Request.Path;
                        if (!string.IsNullOrEmpty(token) && path.StartsWithSegments("/hubs"))
                            ctx.Token = token;
                        return Task.CompletedTask;
                    }
                };
            });

        services.AddAuthorizationBuilder()
            .AddPolicy("AdminOnly", policy => policy.RequireRole("Admin"));

        return services;
    }
}
