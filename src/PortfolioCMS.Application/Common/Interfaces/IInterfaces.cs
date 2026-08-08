using Microsoft.EntityFrameworkCore;
using PortfolioCMS.Domain.Entities;

namespace PortfolioCMS.Application.Common.Interfaces;

// ─── Database context ─────────────────────────────────────────────────────────

public interface IAppDbContext
{
    DbSet<PortfolioSection> Sections { get; }
    DbSet<ProjectCard> Projects { get; }
    DbSet<ThemeSettings> Themes { get; }
    DbSet<AuditLog> AuditLogs { get; }
    DbSet<AppUser> Users { get; }
    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}

// ─── Token service ────────────────────────────────────────────────────────────

public interface ITokenService
{
    string GenerateAccessToken(AppUser user);
    string GenerateRefreshToken();
    System.Security.Claims.ClaimsPrincipal? GetPrincipalFromExpiredToken(string token);
}

// ─── Current user context ─────────────────────────────────────────────────────

public interface ICurrentUserService
{
    string? Username { get; }
    string? IpAddress { get; }
    bool IsAdmin { get; }
}

// ─── Audit service ────────────────────────────────────────────────────────────

public interface IAuditService
{
    Task LogAsync(string action, string entityName, int? entityId,
        object? oldValue, object? newValue, CancellationToken ct = default);
}

// ─── Image storage ────────────────────────────────────────────────────────────

public interface IImageStorageService
{
    Task<string> UploadAsync(Stream fileStream, string fileName, string contentType, CancellationToken ct = default);
    Task DeleteAsync(string fileName, CancellationToken ct = default);
}

// ─── AI service ───────────────────────────────────────────────────────────────

public interface IAiService
{
    Task<string> ImproveTextAsync(string text, string context, CancellationToken ct = default);
    Task<(string description, string techStack)> GenerateProjectDescriptionAsync(string readmeContent, string projectTitle, CancellationToken ct = default);
}

// ─── SignalR notification ─────────────────────────────────────────────────────

public interface IPortfolioNotificationService
{
    Task NotifyContentUpdatedAsync(string sectionType, object payload);
    Task NotifyThemeUpdatedAsync(object themePayload);
}