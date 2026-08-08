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

// ─── Get all sections ─────────────────────────────────────────────────────────

public record GetAllSectionsQuery() : IRequest<List<SectionDto>>;

public sealed class GetAllSectionsHandler : IRequestHandler<GetAllSectionsQuery, List<SectionDto>>
{
    private readonly IAppDbContext _db;
    public GetAllSectionsHandler(IAppDbContext db) => _db = db;

    public async Task<List<SectionDto>> Handle(GetAllSectionsQuery request, CancellationToken ct)
        => await _db.Sections
            .OrderBy(s => s.DisplayOrder)
            .Select(s => MapToDto(s))
            .ToListAsync(ct);

    internal static SectionDto MapToDto(PortfolioSection s) => new(
        s.Id, s.Type.ToString(), s.Title, s.SubTitle,
        s.Content, s.IsVisible, s.DisplayOrder,
        s.BackgroundColor, s.TextColor);
}

// ─── Get section by type ──────────────────────────────────────────────────────

public record GetSectionByTypeQuery(string Type) : IRequest<SectionDto?>;

public sealed class GetSectionByTypeHandler : IRequestHandler<GetSectionByTypeQuery, SectionDto?>
{
    private readonly IAppDbContext _db;
    public GetSectionByTypeHandler(IAppDbContext db) => _db = db;

    public async Task<SectionDto?> Handle(GetSectionByTypeQuery request, CancellationToken ct)
    {
        if (!Enum.TryParse<SectionType>(request.Type, true, out var sectionType))
            return null;

        var section = await _db.Sections.FirstOrDefaultAsync(s => s.Type == sectionType, ct);
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

    public UpdateSectionHandler(IAppDbContext db, IAuditService audit, IPortfolioNotificationService notify)
    {
        _db = db; _audit = audit; _notify = notify;
    }

    public async Task<SectionDto> Handle(UpdateSectionCommand request, CancellationToken ct)
    {
        var section = await _db.Sections.FindAsync([request.SectionId], ct)
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
    public ReorderSectionsHandler(IAppDbContext db) => _db = db;

    public async Task<bool> Handle(ReorderSectionsCommand request, CancellationToken ct)
    {
        foreach (var (id, order) in request.Orders)
        {
            var section = await _db.Sections.FindAsync([id], ct);
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
