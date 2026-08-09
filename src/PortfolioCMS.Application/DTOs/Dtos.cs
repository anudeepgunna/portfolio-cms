namespace PortfolioCMS.Application.DTOs;

// ─── Auth ─────────────────────────────────────────────────────────────────────

public record LoginRequest(string Username, string Password);

public record RegisterRequest(string Username, string Password);

public record AuthResponse(string AccessToken, string RefreshToken, string Username, string Role);

public record RefreshTokenRequest(string RefreshToken);

// ─── Portfolio ────────────────────────────────────────────────────────────────

/// <summary>Everything the public page needs for one user, in a single round trip.</summary>
public record PublicPortfolioDto(
    string Username,
    ThemeDto? Theme,
    List<SectionDto> Sections,
    List<ProjectDto> Projects
);

// ─── Section ──────────────────────────────────────────────────────────────────

public record SectionDto(
    int Id,
    string Type,
    string Title,
    string? SubTitle,
    string Content,
    bool IsVisible,
    int DisplayOrder,
    string BackgroundColor,
    string TextColor
);

public record UpdateSectionRequest(
    string Title,
    string? SubTitle,
    string Content,
    bool IsVisible,
    int DisplayOrder,
    string BackgroundColor,
    string TextColor
);

// ─── Project ──────────────────────────────────────────────────────────────────

public record ProjectDto(
    int Id,
    string Title,
    string Description,
    string TechStack,
    string? GitHubUrl,
    string? LiveUrl,
    string? ImageUrl,
    int DisplayOrder,
    bool IsVisible
);

public record CreateProjectRequest(
    string Title,
    string Description,
    string TechStack,
    string? GitHubUrl,
    string? LiveUrl,
    int DisplayOrder
);

public record UpdateProjectRequest(
    string Title,
    string Description,
    string TechStack,
    string? GitHubUrl,
    string? LiveUrl,
    string? ImageUrl,
    int DisplayOrder,
    bool IsVisible
);

// ─── Theme ────────────────────────────────────────────────────────────────────

public record ThemeDto(
    int Id,
    string PrimaryColor,
    string SecondaryColor,
    string AccentColor,
    string BackgroundColor,
    string SurfaceColor,
    string TextColor,
    string FontFamily,
    string HeadingFontFamily
);

public record UpdateThemeRequest(
    string PrimaryColor,
    string SecondaryColor,
    string AccentColor,
    string BackgroundColor,
    string SurfaceColor,
    string TextColor,
    string FontFamily,
    string HeadingFontFamily
);

// ─── AI ───────────────────────────────────────────────────────────────────────

public record ImproveTextRequest(string Text, string Context);

public record ImproveTextResponse(string OriginalText, string ImprovedText);

public record GenerateProjectDescRequest(string ReadmeContent, string ProjectTitle);

public record GenerateProjectDescResponse(string Description, string TechStack);

// ─── Image Upload ─────────────────────────────────────────────────────────────

public record ImageUploadResponse(string Url, string FileName, long SizeBytes);

// ─── Audit Log ────────────────────────────────────────────────────────────────

public record AuditLogDto(
    int Id,
    string Action,
    string EntityName,
    int? EntityId,
    string? OldValue,
    string? NewValue,
    string PerformedBy,
    DateTime CreatedAt
);
