using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace PortfolioCMS.Infrastructure.Persistence;

public class AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>
{
    // Used only by the EF tools (migrations add / database update). Honour
    // DATABASE_URL when it is set so migrations can be applied to a scratch or
    // hosted database, and fall back to the local dev server otherwise.
    private const string LocalDev =
        "Host=localhost;Port=5432;Database=PortfolioCMS_Dev;Username=postgres;Password=postgres";

    public AppDbContext CreateDbContext(string[] args)
    {
        var optionsBuilder = new DbContextOptionsBuilder<AppDbContext>();
        optionsBuilder.UseNpgsql(FromEnvironment() ?? LocalDev);
        return new AppDbContext(optionsBuilder.Options);
    }

    private static string? FromEnvironment()
    {
        var url = Environment.GetEnvironmentVariable("DATABASE_URL");

        if (string.IsNullOrEmpty(url)) return null;

        // Already a keyword connection string rather than a URL.
        if (!url.StartsWith("postgres://") && !url.StartsWith("postgresql://")) return url;

        var uri = new Uri(url);
        var userInfo = uri.UserInfo.Split(':', 2);
        var username = Uri.UnescapeDataString(userInfo[0]);
        var password = userInfo.Length > 1 ? Uri.UnescapeDataString(userInfo[1]) : string.Empty;
        var port = uri.Port > 0 ? uri.Port : 5432;

        var sslMode = uri.Host is "localhost" or "127.0.0.1"
            ? "Disable"
            : "Require;Trust Server Certificate=true";

        return $"Host={uri.Host};Port={port};Database={uri.AbsolutePath.TrimStart('/')};"
             + $"Username={username};Password={password};SSL Mode={sslMode}";
    }
}
