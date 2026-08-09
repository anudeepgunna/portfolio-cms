using PortfolioCMS.Domain.Entities;
using PortfolioCMS.Domain.Enums;

namespace PortfolioCMS.Application.Common;

/// <summary>
/// Starter content handed to every new account so a freshly registered user
/// lands on a complete, publishable portfolio instead of an empty page.
///
/// Shared by the registration handler and the first-boot seeder so both paths
/// produce identical portfolios.
/// </summary>
public static class PortfolioDefaults
{
    public static List<PortfolioSection> SectionsFor(int ownerId, string username) => new()
    {
        new()
        {
            OwnerId = ownerId,
            Type = SectionType.Hero,
            Title = $"Hi, I'm {username}",
            SubTitle = "Add your title here",
            Content = "Introduce yourself in a sentence or two. Edit everything on this page from the admin dashboard.",
            DisplayOrder = 1,
            BackgroundColor = "#0f172a",
            TextColor = "#f8fafc",
            IsVisible = true
        },
        new()
        {
            OwnerId = ownerId,
            Type = SectionType.About,
            Title = "About Me",
            Content = "Tell visitors about your background, what you work on, and what you're looking for.",
            DisplayOrder = 2,
            BackgroundColor = "#1e293b",
            TextColor = "#f8fafc",
            IsVisible = true
        },
        new()
        {
            OwnerId = ownerId,
            Type = SectionType.Skills,
            // Rendered as tags — comma-separated.
            Content = "C#,.NET,Blazor,SQL,Docker",
            Title = "Skills",
            DisplayOrder = 3,
            BackgroundColor = "#0f172a",
            TextColor = "#f8fafc",
            IsVisible = true
        },
        new()
        {
            OwnerId = ownerId,
            Type = SectionType.Projects,
            Title = "Projects",
            SubTitle = "Things I've built",
            Content = "A showcase of the work you want to highlight.",
            DisplayOrder = 4,
            BackgroundColor = "#1e293b",
            TextColor = "#f8fafc",
            IsVisible = true
        },
        new()
        {
            OwnerId = ownerId,
            Type = SectionType.Contact,
            Title = "Get In Touch",
            Content = "Add your email and links so people can reach you.",
            DisplayOrder = 5,
            BackgroundColor = "#0f172a",
            TextColor = "#f8fafc",
            IsVisible = true
        }
    };

    public static ThemeSettings ThemeFor(int ownerId) => new()
    {
        OwnerId = ownerId,
        PrimaryColor = "#6366f1",
        SecondaryColor = "#8b5cf6",
        AccentColor = "#06b6d4",
        BackgroundColor = "#0f172a",
        SurfaceColor = "#1e293b",
        TextColor = "#f8fafc",
        FontFamily = "Inter",
        HeadingFontFamily = "Inter"
    };
}
