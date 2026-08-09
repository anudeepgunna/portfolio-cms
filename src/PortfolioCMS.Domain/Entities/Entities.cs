using PortfolioCMS.Domain.Common;
using PortfolioCMS.Domain.Enums;

namespace PortfolioCMS.Domain.Entities;

// ─── Portfolio Section ───────────────────────────────────────────────────────
// Represents a visible section on the public portfolio page (Hero, About, etc.)

public class PortfolioSection : BaseEntity
{
    // The user whose portfolio this section belongs to. Every content row is
    // owned; queries must filter on it or one user would see another's page.
    public int OwnerId { get; set; }
    public AppUser? Owner { get; set; }

    public SectionType Type { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Content { get; set; } = string.Empty;   // rich text / markdown
    public string? SubTitle { get; set; }
    public bool IsVisible { get; set; } = true;
    public int DisplayOrder { get; set; }
    public string BackgroundColor { get; set; } = "#ffffff";
    public string TextColor { get; set; } = "#111111";
}

// ─── Project Card ─────────────────────────────────────────────────────────────
// Each card shown in the Projects section

public class ProjectCard : BaseEntity
{
    public int OwnerId { get; set; }
    public AppUser? Owner { get; set; }

    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string TechStack { get; set; } = string.Empty;  // comma-separated tags
    public string? GitHubUrl { get; set; }
    public string? LiveUrl { get; set; }
    public string? ImageUrl { get; set; }
    public int DisplayOrder { get; set; }
    public bool IsVisible { get; set; } = true;
}

// ─── Theme Settings ───────────────────────────────────────────────────────────
// One row per user — the colour/font theme for that user's portfolio

public class ThemeSettings : BaseEntity
{
    public int OwnerId { get; set; }
    public AppUser? Owner { get; set; }

    public string PrimaryColor { get; set; } = "#6366f1";
    public string SecondaryColor { get; set; } = "#8b5cf6";
    public string AccentColor { get; set; } = "#06b6d4";
    public string BackgroundColor { get; set; } = "#0f172a";
    public string SurfaceColor { get; set; } = "#1e293b";
    public string TextColor { get; set; } = "#f8fafc";
    public string FontFamily { get; set; } = "Inter";
    public string HeadingFontFamily { get; set; } = "Inter";
}

// ─── Audit Log ────────────────────────────────────────────────────────────────
// Immutable record of every admin write operation

public class AuditLog : BaseEntity
{
    public string Action { get; set; } = string.Empty;         // e.g. "UpdateSection"
    public string EntityName { get; set; } = string.Empty;     // e.g. "PortfolioSection"
    public int? EntityId { get; set; }
    public string? OldValue { get; set; }                       // JSON snapshot before
    public string? NewValue { get; set; }                       // JSON snapshot after
    public string PerformedBy { get; set; } = string.Empty;    // username
    public string IpAddress { get; set; } = string.Empty;
}

// ─── Application User ─────────────────────────────────────────────────────────
// Each user owns exactly one portfolio, published at /p/{Username}.

public class AppUser : BaseEntity
{
    public string Username { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public string Role { get; set; } = "Viewer";               // "Admin" | "Viewer"
    public string? RefreshToken { get; set; }
    public DateTime? RefreshTokenExpiry { get; set; }

    // Owned content. Cascade-deleted with the user.
    public ICollection<PortfolioSection> Sections { get; set; } = new List<PortfolioSection>();
    public ICollection<ProjectCard> Projects { get; set; } = new List<ProjectCard>();
}
