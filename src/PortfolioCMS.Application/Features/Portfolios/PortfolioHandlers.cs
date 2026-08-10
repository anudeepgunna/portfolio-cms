using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioCMS.Application.Common.Interfaces;
using PortfolioCMS.Application.DTOs;
using PortfolioCMS.Application.Features.Projects;
using PortfolioCMS.Application.Features.Sections;
using PortfolioCMS.Application.Features.Theme;
using PortfolioCMS.Domain.Enums;

namespace PortfolioCMS.Application.Features.Portfolios;

// ─── Resolve a public username to its owner id ────────────────────────────────

public record ResolveOwnerQuery(string Username) : IRequest<int?>;

public sealed class ResolveOwnerHandler : IRequestHandler<ResolveOwnerQuery, int?>
{
    private readonly IAppDbContext _db;
    public ResolveOwnerHandler(IAppDbContext db) => _db = db;

    public async Task<int?> Handle(ResolveOwnerQuery request, CancellationToken ct)
    {
        var username = request.Username.Trim();

        var user = await _db.Users
            .Where(u => u.Username.ToLower() == username.ToLower())
            .Select(u => new { u.Id })
            .FirstOrDefaultAsync(ct);

        return user?.Id;
    }
}

// ─── Whole public portfolio in one request ────────────────────────────────────
// The public page needs theme + sections + projects together. Fetching them as
// three round trips is especially painful on Render's free tier, where the
// first call also pays the cold-start cost.

public record GetPublicPortfolioQuery(string Username) : IRequest<PublicPortfolioDto?>;

public sealed class GetPublicPortfolioHandler
    : IRequestHandler<GetPublicPortfolioQuery, PublicPortfolioDto?>
{
    private readonly IAppDbContext _db;
    public GetPublicPortfolioHandler(IAppDbContext db) => _db = db;

    public async Task<PublicPortfolioDto?> Handle(GetPublicPortfolioQuery request, CancellationToken ct)
    {
        var username = request.Username?.Trim() ?? string.Empty;

        // An empty username means "the site owner" — the first account created.
        // That keeps the root URL showing the original portfolio now that every
        // other user lives under /p/{username}.
        var user = username.Length == 0
            ? await _db.Users
                .OrderBy(u => u.Id)
                .Select(u => new { u.Id, u.Username })
                .FirstOrDefaultAsync(ct)
            : await _db.Users
                .Where(u => u.Username.ToLower() == username.ToLower())
                .Select(u => new { u.Id, u.Username })
                .FirstOrDefaultAsync(ct);

        if (user is null) return null;

        var sections = await _db.Sections
            .Where(s => s.OwnerId == user.Id && s.IsVisible)
            .OrderBy(s => s.DisplayOrder)
            .ToListAsync(ct);

        var projects = await _db.Projects
            .Where(p => p.OwnerId == user.Id && p.IsVisible)
            .OrderBy(p => p.DisplayOrder)
            .ToListAsync(ct);

        var theme = await _db.Themes.FirstOrDefaultAsync(t => t.OwnerId == user.Id, ct);

        return new PublicPortfolioDto(
            user.Username,
            theme is null ? null : GetThemeHandler.MapToDto(theme),
            sections.Select(GetAllSectionsHandler.MapToDto).ToList(),
            projects.Select(GetAllProjectsHandler.MapToDto).ToList());
    }
}

// ─── Public directory ─────────────────────────────────────────────────────────
// Lets visitors discover portfolios instead of needing to be handed a link,
// which is what makes the site read as a product rather than one person's page.

public record ExploreQuery(int Limit = 60) : IRequest<List<PortfolioSummaryDto>>;

public sealed class ExploreHandler : IRequestHandler<ExploreQuery, List<PortfolioSummaryDto>>
{
    private readonly IAppDbContext _db;
    public ExploreHandler(IAppDbContext db) => _db = db;

    public async Task<List<PortfolioSummaryDto>> Handle(ExploreQuery request, CancellationToken ct)
    {
        var take = Math.Clamp(request.Limit, 1, 200);

        // Projected in one query rather than loading users and fanning out per
        // row; the directory grows with every signup.
        return await _db.Users
            .OrderByDescending(u => u.Id)
            .Take(take)
            .Select(u => new PortfolioSummaryDto(
                u.Username,
                _db.Sections
                    .Where(s => s.OwnerId == u.Id && s.Type == SectionType.Hero)
                    .Select(s => s.Title)
                    .FirstOrDefault() ?? u.Username,
                _db.Sections
                    .Where(s => s.OwnerId == u.Id && s.Type == SectionType.Hero)
                    .Select(s => s.SubTitle)
                    .FirstOrDefault(),
                _db.Projects.Count(p => p.OwnerId == u.Id && p.IsVisible),
                _db.Themes.Where(t => t.OwnerId == u.Id).Select(t => t.PrimaryColor).FirstOrDefault() ?? "#6366f1",
                _db.Themes.Where(t => t.OwnerId == u.Id).Select(t => t.AccentColor).FirstOrDefault() ?? "#06b6d4",
                _db.Themes.Where(t => t.OwnerId == u.Id).Select(t => t.BackgroundColor).FirstOrDefault() ?? "#0f172a"))
            .ToListAsync(ct);
    }
}
