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
        // Handle Railway's DATABASE_URL format (postgresql://user:pass@host:port/db)
        // AND standard Npgsql connection string format
        var connectionString = BuildConnectionString(config);

        services.AddDbContext<AppDbContext>(options =>
            options.UseNpgsql(connectionString));

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

    private static string BuildConnectionString(IConfiguration config)
    {
        // Priority 1: DATABASE_URL environment variable, as supplied by Render and
        // Railway. Render uses the "postgres://" scheme, Railway "postgresql://",
        // and Render's internal URLs often omit the port entirely.
        var databaseUrl = config["DATABASE_URL"];
        if (!string.IsNullOrEmpty(databaseUrl)
            && (databaseUrl.StartsWith("postgres://") || databaseUrl.StartsWith("postgresql://")))
        {
            var uri = new Uri(databaseUrl);

            // Credentials are percent-encoded in a URL; Npgsql wants them raw.
            var userInfo = uri.UserInfo.Split(':', 2);
            var username = Uri.UnescapeDataString(userInfo[0]);
            var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;

            // Uri.Port is -1 when the URL carries no explicit port.
            var port = uri.Port > 0 ? uri.Port : 5432;

            return $"Host={uri.Host};Port={port};Database={uri.AbsolutePath.TrimStart('/')};"
                 + $"Username={username};Password={password};SSL Mode=Require;Trust Server Certificate=true";
        }

        // Priority 2: Standard connection string from appsettings
        var connStr = config.GetConnectionString("DefaultConnection");
        if (!string.IsNullOrEmpty(connStr))
            return connStr;

        throw new InvalidOperationException(
            "No database connection string found. Set DATABASE_URL or ConnectionStrings__DefaultConnection.");
    }
}