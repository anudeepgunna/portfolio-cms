using MediatR;
using Microsoft.AspNetCore.Mvc;
using PortfolioCMS.API.Middleware;
using PortfolioCMS.Application;
using PortfolioCMS.Application.DTOs;
using PortfolioCMS.Application.Features.AI;
using PortfolioCMS.Application.Features.Projects;
using PortfolioCMS.Application.Features.Sections;
using PortfolioCMS.Application.Features.Theme;
using PortfolioCMS.Application.Common.Interfaces;
using PortfolioCMS.Infrastructure;
using PortfolioCMS.Infrastructure.Services;
using PortfolioCMS.Infrastructure.Persistence.Seeders;
using Microsoft.SemanticKernel.Services;
using Microsoft.EntityFrameworkCore;
using PortfolioCMS.Application.Features.Auth;

var builder = WebApplication.CreateBuilder(args);

// ── Services ──────────────────────────────────────────────────────────────────
builder.Services.AddApplicationServices();
builder.Services.AddInfrastructureServices(builder.Configuration);

builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new() { Title = "Portfolio CMS API", Version = "v1" });
    c.AddSecurityDefinition("Bearer", new Microsoft.OpenApi.Models.OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Enter: Bearer {token}",
        Name = "Authorization", In = Microsoft.OpenApi.Models.ParameterLocation.Header,
        Type = Microsoft.OpenApi.Models.SecuritySchemeType.ApiKey, Scheme = "Bearer"
    });
    c.AddSecurityRequirement(new Microsoft.OpenApi.Models.OpenApiSecurityRequirement
    {{
        new Microsoft.OpenApi.Models.OpenApiSecurityScheme
        {
            Reference = new Microsoft.OpenApi.Models.OpenApiReference
            { Type = Microsoft.OpenApi.Models.ReferenceType.SecurityScheme, Id = "Bearer" }
        }, Array.Empty<string>()
    }});
});

builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
builder.Services.AddProblemDetails();

// CORS — allow Blazor WASM dev server and production domain
builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorPolicy", policy =>
    {
        policy.WithOrigins(
                builder.Configuration["AllowedOrigins"] ?? "http://localhost:5001",
                "https://your-portfolio.azurestaticapps.net")
            .AllowAnyMethod()
            .AllowAnyHeader()
            .AllowCredentials();  // required for SignalR
    });
});


var app = builder.Build();

// ── Seed database on startup ──────────────────────────────────────────────────
await DatabaseSeeder.SeedAsync(app.Services);

// ── Middleware pipeline ───────────────────────────────────────────────────────
app.UseExceptionHandler();

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseHttpsRedirection();
app.UseCors("BlazorPolicy");
app.UseAuthentication();
app.UseAuthorization();

// ── SignalR hub ───────────────────────────────────────────────────────────────
app.MapHub<PortfolioHub>("/hubs/portfolio");

// ═══════════════════════════════════════════════════════════════════════════════
// MINIMAL API ENDPOINTS
// ═══════════════════════════════════════════════════════════════════════════════

// ── Auth ──────────────────────────────────────────────────────────────────────

var auth = app.MapGroup("/api/auth").WithTags("Auth");

auth.MapPost("/login", async (LoginRequest req, IMediator m) =>
    Results.Ok(await m.Send(new LoginCommand(req))))
    .AllowAnonymous();

auth.MapPost("/refresh", async (RefreshTokenRequest req, IMediator m) =>
    Results.Ok(await m.Send(new RefreshTokenCommand(req))))
    .AllowAnonymous();

// ── Sections (public read / admin write) ──────────────────────────────────────

var sections = app.MapGroup("/api/sections").WithTags("Sections");

sections.MapGet("/", async (IMediator m) =>
    Results.Ok(await m.Send(new GetAllSectionsQuery())))
    .AllowAnonymous();

sections.MapGet("/{type}", async (string type, IMediator m) =>
{
    var result = await m.Send(new GetSectionByTypeQuery(type));
    return result is null ? Results.NotFound() : Results.Ok(result);
}).AllowAnonymous();

sections.MapPut("/{id:int}", async (int id, UpdateSectionRequest req, IMediator m) =>
    Results.Ok(await m.Send(new UpdateSectionCommand(id, req))))
    .RequireAuthorization("AdminOnly");

sections.MapPost("/reorder", async ([FromBody] List<ReorderItem> items, IMediator m) =>
{
    var orders = items.Select(i => (i.Id, i.Order)).ToList();
    return Results.Ok(await m.Send(new ReorderSectionsCommand(orders)));
}).RequireAuthorization("AdminOnly");

// ── Projects (public read / admin write) ──────────────────────────────────────

var projects = app.MapGroup("/api/projects").WithTags("Projects");

projects.MapGet("/", async (IMediator m, bool visibleOnly = true) =>
    Results.Ok(await m.Send(new GetAllProjectsQuery(visibleOnly))))
    .AllowAnonymous();

projects.MapPost("/", async (CreateProjectRequest req, IMediator m) =>
    Results.Created("/api/projects", await m.Send(new CreateProjectCommand(req))))
    .RequireAuthorization("AdminOnly");

projects.MapPut("/{id:int}", async (int id, UpdateProjectRequest req, IMediator m) =>
    Results.Ok(await m.Send(new UpdateProjectCommand(id, req))))
    .RequireAuthorization("AdminOnly");

projects.MapDelete("/{id:int}", async (int id, IMediator m) =>
    Results.Ok(await m.Send(new DeleteProjectCommand(id))))
    .RequireAuthorization("AdminOnly");

// ── Theme (public read / admin write) ─────────────────────────────────────────

var theme = app.MapGroup("/api/theme").WithTags("Theme");

theme.MapGet("/", async (IMediator m) =>
{
    var t = await m.Send(new GetThemeQuery());
    return t is null ? Results.NotFound() : Results.Ok(t);
}).AllowAnonymous();

theme.MapPut("/", async (UpdateThemeRequest req, IMediator m) =>
    Results.Ok(await m.Send(new UpdateThemeCommand(req))))
    .RequireAuthorization("AdminOnly");

// ── Image Upload ──────────────────────────────────────────────────────────────

var images = app.MapGroup("/api/images").WithTags("Images");

images.MapPost("/upload", async (IFormFile file, IImageStorageService storage,
    CancellationToken ct) =>
{
    if (file.Length > 5 * 1024 * 1024)  // 5 MB limit
        return Results.BadRequest("File size exceeds 5 MB");

    var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif" };
    if (!allowed.Contains(file.ContentType))
        return Results.BadRequest("Only JPEG, PNG, WebP, and GIF are allowed");

    await using var stream = file.OpenReadStream();
    var url = await storage.UploadAsync(stream, file.FileName, file.ContentType, ct);

    return Results.Ok(new ImageUploadResponse(url, file.FileName, file.Length));
}).RequireAuthorization("AdminOnly").DisableAntiforgery();

// ── AI Endpoints ──────────────────────────────────────────────────────────────

var ai = app.MapGroup("/api/ai").WithTags("AI");

ai.MapPost("/improve-text", async (ImproveTextRequest req, IMediator m, IAiService? aiService) =>
{
    if (aiService is null) return Results.StatusCode(503);  // AI not configured
    return Results.Ok(await m.Send(new ImproveTextCommand(req)));
}).RequireAuthorization("AdminOnly");

ai.MapPost("/generate-project-desc", async (GenerateProjectDescRequest req,
    IMediator m, IAiService? aiService) =>
{
    if (aiService is null) return Results.StatusCode(503);
    return Results.Ok(await m.Send(new GenerateProjectDescCommand(req)));
}).RequireAuthorization("AdminOnly");

// TEMPORARY RESET — delete after fixing
app.MapPost("/api/dev/reset-admin", async (IAppDbContext db) =>
{
    var existing = await db.Users.FirstOrDefaultAsync(u => u.Username == "admin");
    if (existing != null)
    {
        existing.PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456");
        existing.Role = "Admin";
    }
    else
    {
        db.Users.Add(new PortfolioCMS.Domain.Entities.AppUser
        {
            Username = "admin",
            PasswordHash = BCrypt.Net.BCrypt.HashPassword("Admin@123456"),
            Role = "Admin",
            CreatedAt = DateTime.UtcNow
        });
    }
    await db.SaveChangesAsync();
    return Results.Ok("✅ Admin reset successfully");
}).AllowAnonymous();

// TEMPORARY — check users table
app.MapGet("/api/dev/check-users", async (IAppDbContext db) =>
{
    var users = await db.Users
        .Select(u => new { u.Username, u.Role, u.CreatedAt })
        .ToListAsync();
    return Results.Ok(users);
}).AllowAnonymous();
// ── Audit Log ─────────────────────────────────────────────────────────────────

// app.MapGet("/api/audit", async (IAppDbContext db, int page = 1, int pageSize = 20) =>
// {
//     var logs = await db.AuditLogs
//         .OrderByDescending(l => l.CreatedAt)
//         .Skip((page - 1) * pageSize)
//         .Take(pageSize)
//         .Select(l => new AuditLogDto(l.Id, l.Action, l.EntityName, l.EntityId,
//             l.OldValue, l.NewValue, l.PerformedBy, l.CreatedAt))
//         .ToListAsync();
//     return Results.Ok(logs);
// }).RequireAuthorization("AdminOnly").WithTags("Audit");

app.Run();

// ── Helper records ────────────────────────────────────────────────────────────
record ReorderItem(int Id, int Order);

// Needed for integration test project to reference this assembly
public partial class Program { }
