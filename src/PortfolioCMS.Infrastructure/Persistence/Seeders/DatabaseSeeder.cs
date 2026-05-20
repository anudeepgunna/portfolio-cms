using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using PortfolioCMS.Domain.Entities;
using PortfolioCMS.Domain.Enums;

namespace PortfolioCMS.Infrastructure.Persistence.Seeders;

public static class DatabaseSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        var logger = scope.ServiceProvider.GetRequiredService<ILogger<AppDbContext>>();

        try
        {
            // Run pending migrations automatically on startup
            await db.Database.MigrateAsync();

            await SeedUsersAsync(db);
            await SeedSectionsAsync(db);
            await SeedThemeAsync(db);
            await SeedProjectsAsync(db);

            logger.LogInformation("✅ Database seeded successfully");
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "❌ Error seeding database");
            throw;
        }
    }

    private static async Task SeedUsersAsync(AppDbContext db)
    {
        if (await db.Users.AnyAsync()) return;

        // Default admin — CHANGE THIS PASSWORD before deploying!
        db.Users.Add(new AppUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
            Role = "Admin",
            CreatedAt = DateTime.UtcNow
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedSectionsAsync(AppDbContext db)
    {
        if (await db.Sections.AnyAsync()) return;

        var sections = new List<PortfolioSection>
        {
            new()
            {
                Type = SectionType.Hero,
                Title = "Hi, I'm Your Name",
                SubTitle = "Full-Stack .NET Developer",
                Content = "I build modern web applications with C#, .NET, and Blazor. Passionate about clean architecture, AI integration, and delivering great user experiences.",
                DisplayOrder = 1,
                BackgroundColor = "#0f172a",
                TextColor = "#f8fafc",
                IsVisible = true
            },
            new()
            {
                Type = SectionType.About,
                Title = "About Me",
                Content = "I'm a software developer with 1-2 years of experience working with C# and .NET. I love solving complex problems and continuously learning new technologies.",
                DisplayOrder = 2,
                BackgroundColor = "#1e293b",
                TextColor = "#f8fafc",
                IsVisible = true
            },
            new()
            {
                Type = SectionType.Skills,
                Title = "Skills",
                Content = "C#,.NET 9,ASP.NET Core,Blazor,Entity Framework Core,SQL Server,Azure,Docker,GitHub Actions,REST APIs,CQRS,MediatR",
                DisplayOrder = 3,
                BackgroundColor = "#0f172a",
                TextColor = "#f8fafc",
                IsVisible = true
            },
            new()
            {
                Type = SectionType.Projects,
                Title = "Projects",
                SubTitle = "Things I've built",
                Content = "A showcase of projects demonstrating my full-stack development skills.",
                DisplayOrder = 4,
                BackgroundColor = "#1e293b",
                TextColor = "#f8fafc",
                IsVisible = true
            },
            new()
            {
                Type = SectionType.Contact,
                Title = "Get In Touch",
                Content = "I'm open to new opportunities. Feel free to reach out!\nyour.email@gmail.com\nhttps://github.com/yourusername\nhttps://linkedin.com/in/yourprofile",
                DisplayOrder = 5,
                BackgroundColor = "#0f172a",
                TextColor = "#f8fafc",
                IsVisible = true
            }
        };

        db.Sections.AddRange(sections);
        await db.SaveChangesAsync();
    }

    private static async Task SeedThemeAsync(AppDbContext db)
    {
        if (await db.Themes.AnyAsync()) return;

        db.Themes.Add(new ThemeSettings
        {
            PrimaryColor = "#6366f1",
            SecondaryColor = "#8b5cf6",
            AccentColor = "#06b6d4",
            BackgroundColor = "#0f172a",
            SurfaceColor = "#1e293b",
            TextColor = "#f8fafc",
            FontFamily = "Inter",
            HeadingFontFamily = "Inter"
        });

        await db.SaveChangesAsync();
    }

    private static async Task SeedProjectsAsync(AppDbContext db)
    {
        if (await db.Projects.AnyAsync()) return;

        db.Projects.AddRange(new List<ProjectCard>
        {
            new()
            {
                Title = "Portfolio CMS",
                Description = "A self-hosted portfolio website with a live admin editor, role-based JWT auth, Azure OpenAI integration, and automated CI/CD deployment to Azure.",
                TechStack = "C#,.NET 9,Blazor WASM,ASP.NET Core,EF Core,Azure OpenAI,SignalR,Docker,Azure",
                GitHubUrl = "https://github.com/yourusername/portfolio-cms",
                DisplayOrder = 1,
                IsVisible = true
            }
        });

        await db.SaveChangesAsync();
    }
}
