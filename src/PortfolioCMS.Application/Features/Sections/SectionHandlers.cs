using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioCMS.Application.Common.Interfaces;
using PortfolioCMS.Application.DTOs;
using PortfolioCMS.Domain.Entities;
using PortfolioCMS.Domain.Enums;
using System.Text.Json;

namespace PortfolioCMS.Application.Features.Sections;

// ═══════════════════════════════════════════════════════════════════════════════
// QUERIES
// ═══════════════════════════════════════════════════════════════════════════════

// ─── Get all sections for one portfolio ───────────────────────────────────────

public record GetAllSectionsQuery(int OwnerId) : IRequest<List<SectionDto>>;

public sealed class GetAllSectionsHandler : IRequestHandler<GetAllSectionsQuery, List<SectionDto>>
{
    private readonly IAppDbContext _db;
    public GetAllSectionsHandler(IAppDbContext db) => _db = db;

    public async Task<List<SectionDto>> Handle(GetAllSectionsQuery request, CancellationToken ct)
        => await _db.Sections
            .Where(s => s.OwnerId == request.OwnerId)
            .OrderBy(s => s.DisplayOrder)
            .Select(s => MapToDto(s))
            .ToListAsync(ct);

    internal static SectionDto MapToDto(PortfolioSection s) => new(
        s.Id, s.Type.ToString(), s.Title, s.SubTitle,
        s.Content, s.IsVisible, s.DisplayOrder,
        s.BackgroundColor, s.TextColor);
}

// ─── Get section by type ──────────────────────────────────────────────────────

public record GetSectionByTypeQuery(int OwnerId, string Type) : IRequest<SectionDto?>;

public sealed class GetSectionByTypeHandler : IRequestHandler<GetSectionByTypeQuery, SectionDto?>
{
    private readonly IAppDbContext _db;
    public GetSectionByTypeHandler(IAppDbContext db) => _db = db;

    public async Task<SectionDto?> Handle(GetSectionByTypeQuery request, CancellationToken ct)
    {
        if (!Enum.TryParse<SectionType>(request.Type, true, out var sectionType))
            return null;

        var section = await _db.Sections.FirstOrDefaultAsync(
            s => s.OwnerId == request.OwnerId && s.Type == sectionType, ct);

        return section is null ? null : GetAllSectionsHandler.MapToDto(section);
    }
}

// ═══════════════════════════════════════════════════════════════════════════════
// COMMANDS
// ═══════════════════════════════════════════════════════════════════════════════

// ─── Update section ───────────────────────────────────────────────────────────

public record UpdateSectionCommand(int SectionId, UpdateSectionRequest Payload) : IRequest<SectionDto>;

public sealed class UpdateSectionValidator : AbstractValidator<UpdateSectionCommand>
{
    public UpdateSectionValidator()
    {
        RuleFor(x => x.Payload.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Payload.Content).NotEmpty();
        RuleFor(x => x.Payload.BackgroundColor).Matches("^#[0-9a-fA-F]{6}$").WithMessage("Invalid hex color");
        RuleFor(x => x.Payload.TextColor).Matches("^#[0-9a-fA-F]{6}$").WithMessage("Invalid hex color");
    }
}

public sealed class UpdateSectionHandler : IRequestHandler<UpdateSectionCommand, SectionDto>
{
    private readonly IAppDbContext _db;
    private readonly IAuditService _audit;
    private readonly IPortfolioNotificationService _notify;
    private readonly ICurrentUserService _currentUser;

    public UpdateSectionHandler(IAppDbContext db, IAuditService audit,
        IPortfolioNotificationService notify, ICurrentUserService currentUser)
    {
        _db = db; _audit = audit; _notify = notify; _currentUser = currentUser;
    }

    public async Task<SectionDto> Handle(UpdateSectionCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("Not signed in");

        // Filtering on OwnerId as well as Id is what stops one user editing
        // another's section by guessing its id — a bare FindAsync would not.
        var section = await _db.Sections.FirstOrDefaultAsync(
                s => s.Id == request.SectionId && s.OwnerId == ownerId, ct)
            ?? throw new KeyNotFoundException($"Section {request.SectionId} not found");

        var oldSnapshot = JsonSerializer.Serialize(section);
        var p = request.Payload;

        section.Title = p.Title;
        section.SubTitle = p.SubTitle;
        section.Content = p.Content;
        section.IsVisible = p.IsVisible;
        section.DisplayOrder = p.DisplayOrder;
        section.BackgroundColor = p.BackgroundColor;
        section.TextColor = p.TextColor;
        section.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        await _audit.LogAsync("UpdateSection", "PortfolioSection", section.Id,
            oldSnapshot, JsonSerializer.Serialize(section), ct);

        var dto = GetAllSectionsHandler.MapToDto(section);
        await _notify.NotifyContentUpdatedAsync(section.Type.ToString(), dto);

        return dto;
    }
}

// ─── Reorder sections ─────────────────────────────────────────────────────────

public record ReorderSectionsCommand(List<(int Id, int Order)> Orders) : IRequest<bool>;

public sealed class ReorderSectionsHandler : IRequestHandler<ReorderSectionsCommand, bool>
{
    private readonly IAppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public ReorderSectionsHandler(IAppDbContext db, ICurrentUserService currentUser)
    { _db = db; _currentUser = currentUser; }

    public async Task<bool> Handle(ReorderSectionsCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("Not signed in");

        var ids = request.Orders.Select(o => o.Id).ToList();

        // Load only the caller's own sections, then apply the orders that match.
        var sections = await _db.Sections
            .Where(s => s.OwnerId == ownerId && ids.Contains(s.Id))
            .ToListAsync(ct);

        foreach (var (id, order) in request.Orders)
        {
            var section = sections.FirstOrDefault(s => s.Id == id);
            if (section is not null)
            {
                section.DisplayOrder = order;
                section.UpdatedAt = DateTime.UtcNow;
            }
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }
}
