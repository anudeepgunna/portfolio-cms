using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PortfolioCMS.Domain.Entities;
using PortfolioCMS.Domain.Enums;
using PortfolioCMS.Application.Common;

namespace PortfolioCMS.Infrastructure.Persistence.Seeders;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();
        var config = scope.ServiceProvider.GetRequiredService<IConfiguration>();

        try
        {
            // Run pending migrations automatically on startup
            await db.Database.MigrateAsync();

            await SeedOwnerAsync(db, config);

            logger.LogInformation("✅ Database seeded successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error seeding database");
            throw;
        }
    }

    /// <summary>
    /// Creates the first account on a blank database and provisions its
    /// portfolio. Content is owned per-user now, so seeding is a property of
    /// the account rather than of the database as a whole.
    /// </summary>
    private static async Task SeedOwnerAsync(AppDbContext db, IConfiguration config)
    {
        var username = config["Seed:AdminUsername"] ?? "admin";

        var owner = await db.Users.FirstOrDefaultAsync(u => u.Username == username);

        if (owner is null)
        {
            // The initial admin password must be supplied out-of-band via
            // Seed__AdminPassword. Never fall back to a literal: this repo is public
            // and the seeded account has full Admin rights on a public URL.
            var password = config["Seed:AdminPassword"];
            if (string.IsNullOrWhiteSpace(password))
                throw new InvalidOperationException(
                    "Seed__AdminPassword is not set — refusing to create the admin account " +
                    "with a default password. Set it in the hosting environment and redeploy.");

            owner = new AppUser
            {
                Username = username,
                PasswordHash = BCrypt.Net.BCrypt.HashPassword(password),
                Role = "Admin",
                CreatedAt = DateTime.UtcNow
            };

            db.Users.Add(owner);
            await db.SaveChangesAsync();
        }

        if (!await db.Sections.AnyAsync(x => x.OwnerId == owner.Id))
            db.Sections.AddRange(PortfolioDefaults.SectionsFor(owner.Id, owner.Username));

        if (!await db.Themes.AnyAsync(x => x.OwnerId == owner.Id))
            db.Themes.Add(PortfolioDefaults.ThemeFor(owner.Id));

        await db.SaveChangesAsync();
    }
}
