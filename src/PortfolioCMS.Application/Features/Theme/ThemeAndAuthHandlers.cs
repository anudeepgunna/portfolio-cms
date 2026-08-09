using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioCMS.Application.Common.Interfaces;
using PortfolioCMS.Application.DTOs;
using PortfolioCMS.Domain.Entities;
using System.Text.Json;

namespace PortfolioCMS.Application.Features.Theme;

// ─── Get theme ────────────────────────────────────────────────────────────────

public record GetThemeQuery(int OwnerId) : IRequest<ThemeDto?>;

public sealed class GetThemeHandler : IRequestHandler<GetThemeQuery, ThemeDto?>
{
    private readonly IAppDbContext _db;
    public GetThemeHandler(IAppDbContext db) => _db = db;

    public async Task<ThemeDto?> Handle(GetThemeQuery request, CancellationToken ct)
    {
        var t = await _db.Themes.FirstOrDefaultAsync(x => x.OwnerId == request.OwnerId, ct);
        return t is null ? null : MapToDto(t);
    }

    internal static ThemeDto MapToDto(ThemeSettings t) => new(
        t.Id, t.PrimaryColor, t.SecondaryColor, t.AccentColor,
        t.BackgroundColor, t.SurfaceColor, t.TextColor,
        t.FontFamily, t.HeadingFontFamily);
}

// ─── Update theme ─────────────────────────────────────────────────────────────

public record UpdateThemeCommand(UpdateThemeRequest Payload) : IRequest<ThemeDto>;

public sealed class UpdateThemeValidator : AbstractValidator<UpdateThemeCommand>
{
    private static bool IsHex(string? v) =>
        v is not null && System.Text.RegularExpressions.Regex.IsMatch(v, "^#[0-9a-fA-F]{6}$");

    public UpdateThemeValidator()
    {
        RuleFor(x => x.Payload.PrimaryColor).Must(IsHex).WithMessage("Invalid hex color");
        RuleFor(x => x.Payload.SecondaryColor).Must(IsHex).WithMessage("Invalid hex color");
        RuleFor(x => x.Payload.AccentColor).Must(IsHex).WithMessage("Invalid hex color");
        RuleFor(x => x.Payload.BackgroundColor).Must(IsHex).WithMessage("Invalid hex color");
        RuleFor(x => x.Payload.SurfaceColor).Must(IsHex).WithMessage("Invalid hex color");
        RuleFor(x => x.Payload.TextColor).Must(IsHex).WithMessage("Invalid hex color");
        RuleFor(x => x.Payload.FontFamily).NotEmpty();
    }
}

public sealed class UpdateThemeHandler : IRequestHandler<UpdateThemeCommand, ThemeDto>
{
    private readonly IAppDbContext _db;
    private readonly IAuditService _audit;
    private readonly IPortfolioNotificationService _notify;
    private readonly ICurrentUserService _currentUser;

    public UpdateThemeHandler(IAppDbContext db, IAuditService audit,
        IPortfolioNotificationService notify, ICurrentUserService currentUser)
    { _db = db; _audit = audit; _notify = notify; _currentUser = currentUser; }

    public async Task<ThemeDto> Handle(UpdateThemeCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("Not signed in");

        var theme = await _db.Themes.FirstOrDefaultAsync(x => x.OwnerId == ownerId, ct)
            ?? throw new InvalidOperationException("Theme not provisioned for this account");

        var old = JsonSerializer.Serialize(theme);
        var p = request.Payload;

        theme.PrimaryColor = p.PrimaryColor;
        theme.SecondaryColor = p.SecondaryColor;
        theme.AccentColor = p.AccentColor;
        theme.BackgroundColor = p.BackgroundColor;
        theme.SurfaceColor = p.SurfaceColor;
        theme.TextColor = p.TextColor;
        theme.FontFamily = p.FontFamily;
        theme.HeadingFontFamily = p.HeadingFontFamily;
        theme.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("UpdateTheme", "ThemeSettings", theme.Id,
            old, JsonSerializer.Serialize(theme), ct);

        var dto = GetThemeHandler.MapToDto(theme);
        await _notify.NotifyThemeUpdatedAsync(dto);
        return dto;
    }
}