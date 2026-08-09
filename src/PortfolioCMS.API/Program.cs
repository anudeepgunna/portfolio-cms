using MediatR;
using Microsoft.AspNetCore.HttpOverrides;
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
using PortfolioCMS.Application.Features.Portfolios;

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

// CORS — origins come from AllowedOrigins (comma-separated) so the Netlify
// domain can be set per-environment without a code change.
var allowedOrigins = (builder.Configuration["AllowedOrigins"] ?? "http://localhost:5001")
    .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);

builder.Services.AddCors(options =>
{
    options.AddPolicy("BlazorPolicy", policy =>
    {
        policy.WithOrigins(allowedOrigins)
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

// Render terminates TLS at its edge proxy and forwards plain HTTP to the
// container, so honour X-Forwarded-* to recover the caller's real scheme/IP.
// HTTPS is enforced at that edge; redirecting again in-container would loop.
var forwardedHeaders = new ForwardedHeadersOptions
{
    ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto
};
forwardedHeaders.KnownNetworks.Clear();   // the proxy is not on a loopback network
forwardedHeaders.KnownProxies.Clear();
app.UseForwardedHeaders(forwardedHeaders);

if (app.Environment.IsDevelopment())
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

auth.MapPost("/register", async (RegisterRequest req, IMediator m) =>
    Results.Ok(await m.Send(new RegisterCommand(req))))
    .AllowAnonymous();

// ── Public portfolios (anonymous read) ────────────────────────────────────────
// One request returns theme + sections + projects for a single user, so a
// shared link renders without a waterfall of calls.

var portfolios = app.MapGroup("/api/portfolios").WithTags("Portfolios");

// The site owner's portfolio — what the root URL renders.
portfolios.MapGet("/", async (IMediator m) =>
{
    var result = await m.Send(new GetPublicPortfolioQuery(string.Empty));
    return result is null ? Results.NotFound() : Results.Ok(result);
}).AllowAnonymous();

portfolios.MapGet("/{username}", async (string username, IMediator m) =>
{
    var result = await m.Send(new GetPublicPortfolioQuery(username));
    return result is null ? Results.NotFound() : Results.Ok(result);
}).AllowAnonymous();

// ── The signed-in user's own portfolio (includes hidden items) ────────────────

app.MapGet("/api/me/portfolio", async (IMediator m, ICurrentUserService me) =>
{
    if (me.UserId is null) return Results.Unauthorized();

    var sections = await m.Send(new GetAllSectionsQuery(me.UserId.Value));
    var projects = await m.Send(new GetAllProjectsQuery(me.UserId.Value, false));
    var theme    = await m.Send(new GetThemeQuery(me.UserId.Value));

    return Results.Ok(new PublicPortfolioDto(me.Username ?? "", theme, sections, projects));
}).RequireAuthorization().WithTags("Me");

// ── Sections (public read / admin write) ──────────────────────────────────────

var sections = app.MapGroup("/api/sections").WithTags("Sections");

sections.MapGet("/", async (IMediator m, ICurrentUserService me) =>
    me.UserId is null
        ? Results.Unauthorized()
        : Results.Ok(await m.Send(new GetAllSectionsQuery(me.UserId.Value))))
    .RequireAuthorization();

sections.MapGet("/{type}", async (string type, IMediator m, ICurrentUserService me) =>
{
    if (me.UserId is null) return Results.Unauthorized();
    var result = await m.Send(new GetSectionByTypeQuery(me.UserId.Value, type));
    return result is null ? Results.NotFound() : Results.Ok(result);
}).RequireAuthorization();

sections.MapPut("/{id:int}", async (int id, UpdateSectionRequest req, IMediator m) =>
    Results.Ok(await m.Send(new UpdateSectionCommand(id, req))))
    .RequireAuthorization();

sections.MapPost("/reorder", async ([FromBody] List<ReorderItem> items, IMediator m) =>
{
    var orders = items.Select(i => (i.Id, i.Order)).ToList();
    return Results.Ok(await m.Send(new ReorderSectionsCommand(orders)));
}).RequireAuthorization();

// ── Projects (public read / admin write) ──────────────────────────────────────

var projects = app.MapGroup("/api/projects").WithTags("Projects");

projects.MapGet("/", async (IMediator m, ICurrentUserService me, bool visibleOnly = false) =>
    me.UserId is null
        ? Results.Unauthorized()
        : Results.Ok(await m.Send(new GetAllProjectsQuery(me.UserId.Value, visibleOnly))))
    .RequireAuthorization();

projects.MapPost("/", async (CreateProjectRequest req, IMediator m) =>
    Results.Created("/api/projects", await m.Send(new CreateProjectCommand(req))))
    .RequireAuthorization();

projects.MapPut("/{id:int}", async (int id, UpdateProjectRequest req, IMediator m) =>
    Results.Ok(await m.Send(new UpdateProjectCommand(id, req))))
    .RequireAuthorization();

projects.MapDelete("/{id:int}", async (int id, IMediator m) =>
    Results.Ok(await m.Send(new DeleteProjectCommand(id))))
    .RequireAuthorization();

// ── Theme (public read / admin write) ─────────────────────────────────────────

var theme = app.MapGroup("/api/theme").WithTags("Theme");

theme.MapGet("/", async (IMediator m, ICurrentUserService me) =>
{
    if (me.UserId is null) return Results.Unauthorized();
    var t = await m.Send(new GetThemeQuery(me.UserId.Value));
    return t is null ? Results.NotFound() : Results.Ok(t);
}).RequireAuthorization();

theme.MapPut("/", async (UpdateThemeRequest req, IMediator m) =>
    Results.Ok(await m.Send(new UpdateThemeCommand(req))))
    .RequireAuthorization();

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
}).RequireAuthorization().DisableAntiforgery();

// ── AI Endpoints ──────────────────────────────────────────────────────────────

var ai = app.MapGroup("/api/ai").WithTags("AI");

ai.MapPost("/improve-text", async (ImproveTextRequest req, IMediator m, IAiService? aiService) =>
{
    if (aiService is null) return Results.StatusCode(503);  // AI not configured
    return Results.Ok(await m.Send(new ImproveTextCommand(req)));
}).RequireAuthorization();

ai.MapPost("/generate-project-desc", async (GenerateProjectDescRequest req,
    IMediator m, IAiService? aiService) =>
{
    if (aiService is null) return Results.StatusCode(503);
    return Results.Ok(await m.Send(new GenerateProjectDescCommand(req)));
}).RequireAuthorization();

// ── Health check ──────────────────────────────────────────────────────────────
// Render polls this to decide whether a deploy came up successfully.

app.MapGet("/health", () => Results.Ok(new { status = "healthy" }))
    .AllowAnonymous()
    .WithTags("Health");
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
// }).RequireAuthorization().WithTags("Audit");

app.Run();

// ── Helper records ────────────────────────────────────────────────────────────
record ReorderItem(int Id, int Order);

// Needed for integration test project to reference this assembly
public partial class Program { }
