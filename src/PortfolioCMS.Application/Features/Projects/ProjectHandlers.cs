using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioCMS.Application.Common.Interfaces;
using PortfolioCMS.Application.DTOs;
using PortfolioCMS.Domain.Entities;
using System.Text.Json;

namespace PortfolioCMS.Application.Features.Projects;

// ═══════════════════════════════════════════════════════════════════════════════
// QUERIES
// ═══════════════════════════════════════════════════════════════════════════════

public record GetAllProjectsQuery(int OwnerId, bool VisibleOnly = false) : IRequest<List<ProjectDto>>;

public sealed class GetAllProjectsHandler : IRequestHandler<GetAllProjectsQuery, List<ProjectDto>>
{
    private readonly IAppDbContext _db;
    public GetAllProjectsHandler(IAppDbContext db) => _db = db;

    public async Task<List<ProjectDto>> Handle(GetAllProjectsQuery request, CancellationToken ct)
    {
        var query = _db.Projects.Where(p => p.OwnerId == request.OwnerId);
        if (request.VisibleOnly) query = query.Where(p => p.IsVisible);

        return await query
            .OrderBy(p => p.DisplayOrder)
            .Select(p => MapToDto(p))
            .ToListAsync(ct);
    }

    internal static ProjectDto MapToDto(ProjectCard p) => new(
        p.Id, p.Title, p.Description, p.TechStack,
        p.GitHubUrl, p.LiveUrl, p.ImageUrl, p.DisplayOrder, p.IsVisible);
}

// ═══════════════════════════════════════════════════════════════════════════════
// COMMANDS
// ═══════════════════════════════════════════════════════════════════════════════

// ─── Create project ───────────────────────────────────────────────────────────

public record CreateProjectCommand(CreateProjectRequest Payload) : IRequest<ProjectDto>;

public sealed class CreateProjectValidator : AbstractValidator<CreateProjectCommand>
{
    public CreateProjectValidator()
    {
        RuleFor(x => x.Payload.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Payload.Description).NotEmpty().MaximumLength(2000);
        RuleFor(x => x.Payload.TechStack).NotEmpty();
    }
}

public sealed class CreateProjectHandler : IRequestHandler<CreateProjectCommand, ProjectDto>
{
    private readonly IAppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public CreateProjectHandler(IAppDbContext db, IAuditService audit, ICurrentUserService currentUser)
    { _db = db; _audit = audit; _currentUser = currentUser; }

    public async Task<ProjectDto> Handle(CreateProjectCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("Not signed in");

        var p = request.Payload;
        var project = new ProjectCard
        {
            OwnerId = ownerId,
            Title = p.Title,
            Description = p.Description,
            TechStack = p.TechStack,
            GitHubUrl = p.GitHubUrl,
            LiveUrl = p.LiveUrl,
            DisplayOrder = p.DisplayOrder,
            IsVisible = true,
            CreatedAt = DateTime.UtcNow
        };

        _db.Projects.Add(project);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("CreateProject", "ProjectCard", project.Id, null, JsonSerializer.Serialize(project), ct);

        return GetAllProjectsHandler.MapToDto(project);
    }
}

// ─── Update project ───────────────────────────────────────────────────────────

public record UpdateProjectCommand(int ProjectId, UpdateProjectRequest Payload) : IRequest<ProjectDto>;

public sealed class UpdateProjectValidator : AbstractValidator<UpdateProjectCommand>
{
    public UpdateProjectValidator()
    {
        RuleFor(x => x.Payload.Title).NotEmpty().MaximumLength(200);
        RuleFor(x => x.Payload.Description).NotEmpty();
    }
}

public sealed class UpdateProjectHandler : IRequestHandler<UpdateProjectCommand, ProjectDto>
{
    private readonly IAppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public UpdateProjectHandler(IAppDbContext db, IAuditService audit, ICurrentUserService currentUser)
    { _db = db; _audit = audit; _currentUser = currentUser; }

    public async Task<ProjectDto> Handle(UpdateProjectCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("Not signed in");

        // Scoped by OwnerId so a guessed id cannot reach another user's project.
        var project = await _db.Projects.FirstOrDefaultAsync(
                x => x.Id == request.ProjectId && x.OwnerId == ownerId, ct)
            ?? throw new KeyNotFoundException($"Project {request.ProjectId} not found");

        var old = JsonSerializer.Serialize(project);
        var p = request.Payload;

        project.Title = p.Title;
        project.Description = p.Description;
        project.TechStack = p.TechStack;
        project.GitHubUrl = p.GitHubUrl;
        project.LiveUrl = p.LiveUrl;
        project.ImageUrl = p.ImageUrl;
        project.DisplayOrder = p.DisplayOrder;
        project.IsVisible = p.IsVisible;
        project.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("UpdateProject", "ProjectCard", project.Id, old, JsonSerializer.Serialize(project), ct);

        return GetAllProjectsHandler.MapToDto(project);
    }
}

// ─── Delete project ───────────────────────────────────────────────────────────

public record DeleteProjectCommand(int ProjectId) : IRequest<bool>;

public sealed class DeleteProjectHandler : IRequestHandler<DeleteProjectCommand, bool>
{
    private readonly IAppDbContext _db;
    private readonly IAuditService _audit;
    private readonly ICurrentUserService _currentUser;

    public DeleteProjectHandler(IAppDbContext db, IAuditService audit, ICurrentUserService currentUser)
    { _db = db; _audit = audit; _currentUser = currentUser; }

    public async Task<bool> Handle(DeleteProjectCommand request, CancellationToken ct)
    {
        var ownerId = _currentUser.UserId
            ?? throw new UnauthorizedAccessException("Not signed in");

        var project = await _db.Projects.FirstOrDefaultAsync(
                x => x.Id == request.ProjectId && x.OwnerId == ownerId, ct)
            ?? throw new KeyNotFoundException($"Project {request.ProjectId} not found");

        var old = JsonSerializer.Serialize(project);
        _db.Projects.Remove(project);
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("DeleteProject", "ProjectCard", request.ProjectId, old, null, ct);

        return true;
    }
}
