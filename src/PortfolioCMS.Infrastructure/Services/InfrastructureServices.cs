using PortfolioCMS.Infrastructure.Persistence;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.SignalR;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.Tokens;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using PortfolioCMS.Application.Common.Interfaces;
using PortfolioCMS.Domain.Entities;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace PortfolioCMS.Infrastructure.Services;

// ─── JWT Token Service ────────────────────────────────────────────────────────

public sealed class TokenService : ITokenService
{
    private readonly IConfiguration _config;

    public TokenService(IConfiguration config) => _config = config;

    public string GenerateAccessToken(AppUser user)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));

        var claims = new[]
        {
            new Claim(ClaimTypes.Name, user.Username),
            new Claim(ClaimTypes.Role, user.Role),
            new Claim(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new Claim(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString())
        };

        var token = new JwtSecurityToken(
            issuer: _config["Jwt:Issuer"],
            audience: _config["Jwt:Audience"],
            claims: claims,
            expires: DateTime.UtcNow.AddMinutes(
                double.Parse(_config["Jwt:ExpiryMinutes"] ?? "60")),
            signingCredentials: new SigningCredentials(key, SecurityAlgorithms.HmacSha256));

        return new JwtSecurityTokenHandler().WriteToken(token);
    }

    public string GenerateRefreshToken()
    {
        var bytes = new byte[64];
        using var rng = RandomNumberGenerator.Create();
        rng.GetBytes(bytes);
        return Convert.ToBase64String(bytes);
    }

    public ClaimsPrincipal? GetPrincipalFromExpiredToken(string token)
    {
        var key = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(_config["Jwt:Secret"]!));

        var parameters = new TokenValidationParameters
        {
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = key,
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateLifetime = false  // allow expired tokens for refresh flow
        };

        try
        {
            return new JwtSecurityTokenHandler()
                .ValidateToken(token, parameters, out _);
        }
        catch { return null; }
    }
}

// ─── Current User Service ─────────────────────────────────────────────────────

public sealed class CurrentUserService : ICurrentUserService
{
    private readonly IHttpContextAccessor _http;

    public CurrentUserService(IHttpContextAccessor http) => _http = http;

    public string? Username =>
        _http.HttpContext?.User?.Identity?.Name;

    public string? IpAddress =>
        _http.HttpContext?.Connection.RemoteIpAddress?.ToString();

    public bool IsAdmin =>
        _http.HttpContext?.User?.IsInRole("Admin") ?? false;

    // The token carries the id as "sub". ASP.NET's default inbound claim mapping
    // rewrites that to NameIdentifier, so accept either spelling.
    public int? UserId
    {
        get
        {
            var user = _http.HttpContext?.User;
            var raw = user?.FindFirst(ClaimTypes.NameIdentifier)?.Value
                   ?? user?.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;

            return int.TryParse(raw, out var id) ? id : null;
        }
    }
}

// ─── Audit Service ────────────────────────────────────────────────────────────

public sealed class AuditService : IAuditService
{
    private readonly AppDbContext _db;
    private readonly ICurrentUserService _currentUser;

    public AuditService(AppDbContext db, ICurrentUserService currentUser)
    { _db = db; _currentUser = currentUser; }

    public async Task LogAsync(string action, string entityName, int? entityId,
        object? oldValue, object? newValue, CancellationToken ct = default)
    {
        _db.AuditLogs.Add(new AuditLog
        {
            Action = action,
            EntityName = entityName,
            EntityId = entityId,
            OldValue = oldValue is string s ? s : JsonSerializer.Serialize(oldValue),
            NewValue = newValue is string ns ? ns : JsonSerializer.Serialize(newValue),
            PerformedBy = _currentUser.Username ?? "system",
            IpAddress = _currentUser.IpAddress ?? "unknown",
            CreatedAt = DateTime.UtcNow
        });

        await _db.SaveChangesAsync(ct);
    }
}

// ─── Azure Blob Storage Service ───────────────────────────────────────────────

public sealed class AzureBlobStorageService : IImageStorageService
{
    private readonly BlobServiceClient _blobClient;
    private readonly string _containerName;

    public AzureBlobStorageService(IConfiguration config)
    {
        _blobClient = new BlobServiceClient(config["Azure:StorageConnectionString"]);
        _containerName = config["Azure:BlobContainerName"] ?? "portfolio-images";
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName,
        string contentType, CancellationToken ct = default)
    {
        var container = _blobClient.GetBlobContainerClient(_containerName);
        await container.CreateIfNotExistsAsync(PublicAccessType.Blob, cancellationToken: ct);

        var uniqueName = $"{Guid.NewGuid()}-{fileName}";
        var blob = container.GetBlobClient(uniqueName);

        await blob.UploadAsync(fileStream,
            new BlobHttpHeaders { ContentType = contentType }, cancellationToken: ct);

        return blob.Uri.ToString();
    }

    public async Task DeleteAsync(string fileName, CancellationToken ct = default)
    {
        var container = _blobClient.GetBlobContainerClient(_containerName);
        var blob = container.GetBlobClient(fileName);
        await blob.DeleteIfExistsAsync(cancellationToken: ct);
    }
}

// ─── Local Storage Service (dev fallback when no Azure) ──────────────────────

public sealed class LocalImageStorageService : IImageStorageService
{
    private readonly string _uploadPath;
    private readonly string _baseUrl;

    public LocalImageStorageService(IConfiguration config)
    {
        _uploadPath = config["Storage:LocalPath"] ?? Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
        _baseUrl = config["Storage:BaseUrl"] ?? "http://localhost:5000/uploads";
        Directory.CreateDirectory(_uploadPath);
    }

    public async Task<string> UploadAsync(Stream fileStream, string fileName,
        string contentType, CancellationToken ct = default)
    {
        var uniqueName = $"{Guid.NewGuid()}-{fileName}";
        var filePath = Path.Combine(_uploadPath, uniqueName);

        await using var fs = File.Create(filePath);
        await fileStream.CopyToAsync(fs, ct);

        return $"{_baseUrl}/{uniqueName}";
    }

    public Task DeleteAsync(string fileName, CancellationToken ct = default)
    {
        var filePath = Path.Combine(_uploadPath, fileName);
        if (File.Exists(filePath)) File.Delete(filePath);
        return Task.CompletedTask;
    }
}

// ─── AI Service (Azure OpenAI via Semantic Kernel) ────────────────────────────

public sealed class SemanticKernelAiService : IAiService
{
    private readonly Kernel _kernel;

    public SemanticKernelAiService(IConfiguration config)
    {
        var builder = Kernel.CreateBuilder();

        // Supports both Azure OpenAI and OpenAI directly
        var useAzure = !string.IsNullOrEmpty(config["AzureOpenAI:Endpoint"]);

        if (useAzure)
        {
            builder.AddAzureOpenAIChatCompletion(
                deploymentName: config["AzureOpenAI:DeploymentName"] ?? "gpt-4",
                endpoint: config["AzureOpenAI:Endpoint"]!,
                apiKey: config["AzureOpenAI:ApiKey"]!);
        }
        else
        {
            builder.AddOpenAIChatCompletion(
                modelId: "gpt-4o-mini",
                apiKey: config["OpenAI:ApiKey"]!);
        }

        _kernel = builder.Build();
    }

    public async Task<string> ImproveTextAsync(string text, string context, CancellationToken ct = default)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();

        history.AddSystemMessage(
            "You are a professional copywriter helping a developer improve their portfolio content. " +
            "Return only the improved text, no explanations or formatting.");

        history.AddUserMessage(
            $"Context: {context}\n\nImprove this portfolio text to sound more professional and engaging:\n\n{text}");

        var result = await chat.GetChatMessageContentAsync(history, cancellationToken: ct);
        return result.Content ?? text;
    }

    public async Task<(string description, string techStack)> GenerateProjectDescriptionAsync(
        string readmeContent, string projectTitle, CancellationToken ct = default)
    {
        var chat = _kernel.GetRequiredService<IChatCompletionService>();
        var history = new ChatHistory();

        history.AddSystemMessage(
            "You are a technical writer. Given a README, extract a concise 2-3 sentence project " +
            "description and comma-separated tech stack. Respond ONLY as JSON: " +
            "{\"description\": \"...\", \"techStack\": \"tech1,tech2,...\"}");

        history.AddUserMessage(
            $"Project: {projectTitle}\n\nREADME:\n{readmeContent[..Math.Min(3000, readmeContent.Length)]}");

        var result = await chat.GetChatMessageContentAsync(history, cancellationToken: ct);
        var json = result.Content ?? "{}";

        try
        {
            var parsed = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
            return (
                parsed?.GetValueOrDefault("description") ?? "A software project.",
                parsed?.GetValueOrDefault("techStack") ?? "Unknown"
            );
        }
        catch
        {
            return ("A software project.", "Unknown");
        }
    }
}

// ─── SignalR Hub ──────────────────────────────────────────────────────────────


public sealed class PortfolioHub : Hub
{
    // Clients join "viewers" group to receive live updates
    public async Task JoinViewerGroup()
        => await Groups.AddToGroupAsync(Context.ConnectionId, "viewers");
}

// ─── SignalR Notification Service ────────────────────────────────────────────


public sealed class PortfolioNotificationService : IPortfolioNotificationService
{
    private readonly IHubContext<PortfolioHub> _hub;

    public PortfolioNotificationService(IHubContext<PortfolioHub> hub) => _hub = hub;

    public async Task NotifyContentUpdatedAsync(string sectionType, object payload)
        => await _hub.Clients.Group("viewers")
            .SendAsync("ContentUpdated", new { sectionType, payload });

    public async Task NotifyThemeUpdatedAsync(object themePayload)
        => await _hub.Clients.Group("viewers")
            .SendAsync("ThemeUpdated", themePayload);
}
