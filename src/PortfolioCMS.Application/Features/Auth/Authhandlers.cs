using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioCMS.Application.Common;
using PortfolioCMS.Application.Common.Interfaces;
using PortfolioCMS.Application.DTOs;
using PortfolioCMS.Domain.Entities;

namespace PortfolioCMS.Application.Features.Auth;

// ─── Register ─────────────────────────────────────────────────────────────────

public record RegisterCommand(RegisterRequest Payload) : IRequest<AuthResponse>;

public sealed class RegisterValidator : AbstractValidator<RegisterCommand>
{
    public RegisterValidator()
    {
        // The username doubles as the public URL slug (/p/{username}), so it is
        // restricted to characters that are safe and readable in a path segment.
        RuleFor(x => x.Payload.Username)
            .NotEmpty()
            .MinimumLength(3).MaximumLength(30)
            .Matches("^[a-zA-Z0-9_-]+$")
            .WithMessage("Username may only contain letters, numbers, hyphens and underscores");

        RuleFor(x => x.Payload.Password)
            .NotEmpty()
            .MinimumLength(8).WithMessage("Password must be at least 8 characters");
    }
}

public sealed class RegisterHandler : IRequestHandler<RegisterCommand, AuthResponse>
{
    // Route segments that must never be captured by a username, or the public
    // page would shadow a real application route.
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    { "admin", "api", "login", "register", "p", "health", "hubs", "swagger", "explore", "about", "settings" };

    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;

    public RegisterHandler(IAppDbContext db, ITokenService tokens)
    { _db = db; _tokens = tokens; }

    public async Task<AuthResponse> Handle(RegisterCommand request, CancellationToken ct)
    {
        var username = request.Payload.Username.Trim();

        if (Reserved.Contains(username))
            throw new InvalidOperationException($"'{username}' is reserved — please pick another username");

        // Case-insensitive: usernames become URLs, and /p/Alice and /p/alice
        // must not be two different people.
        var taken = await _db.Users
            .AnyAsync(u => u.Username.ToLower() == username.ToLower(), ct);

        if (taken)
            throw new InvalidOperationException($"Username '{username}' is already taken");

        var user = new AppUser
        {
            Username = username,
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(request.Payload.Password),
            // Every user administers their own portfolio. Isolation comes from
            // OwnerId scoping in the handlers, not from this role.
            Role = "Admin",
            CreatedAt = DateTime.UtcNow
        };

        _db.Users.Add(user);
        await _db.SaveChangesAsync(ct);   // assigns user.Id

        _db.Sections.AddRange(PortfolioDefaults.SectionsFor(user.Id, user.Username));
        _db.Themes.Add(PortfolioDefaults.ThemeFor(user.Id));
        await _db.SaveChangesAsync(ct);

        var accessToken  = _tokens.GenerateAccessToken(user);
        var refreshToken = _tokens.GenerateRefreshToken();

        user.RefreshToken       = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, refreshToken, user.Username, user.Role);
    }
}

// ─── Login ────────────────────────────────────────────────────────────────────

public record LoginCommand(LoginRequest Payload) : IRequest<AuthResponse>;

public sealed class LoginValidator : AbstractValidator<LoginCommand>
{
    public LoginValidator()
    {
        RuleFor(x => x.Payload.Username).NotEmpty();
        RuleFor(x => x.Payload.Password).NotEmpty().MinimumLength(6);
    }
}

public sealed class LoginHandler : IRequestHandler<LoginCommand, AuthResponse>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;

    public LoginHandler(IAppDbContext db, ITokenService tokens)
    { _db = db; _tokens = tokens; }

    public async Task<AuthResponse> Handle(LoginCommand request, CancellationToken ct)
    {
        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == request.Payload.Username, ct)
            ?? throw new UnauthorizedAccessException("Invalid credentials");

        if (!BCrypt.Net.BCrypt.Verify(request.Payload.Password, user.PasswordHash))
            throw new UnauthorizedAccessException("Invalid credentials");

        var accessToken  = _tokens.GenerateAccessToken(user);
        var refreshToken = _tokens.GenerateRefreshToken();

        user.RefreshToken       = refreshToken;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(accessToken, refreshToken, user.Username, user.Role);
    }
}

// ─── Refresh Token ────────────────────────────────────────────────────────────

public record RefreshTokenCommand(RefreshTokenRequest Payload) : IRequest<AuthResponse>;

public sealed class RefreshTokenHandler : IRequestHandler<RefreshTokenCommand, AuthResponse>
{
    private readonly IAppDbContext _db;
    private readonly ITokenService _tokens;

    public RefreshTokenHandler(IAppDbContext db, ITokenService tokens)
    { _db = db; _tokens = tokens; }

    public async Task<AuthResponse> Handle(RefreshTokenCommand request, CancellationToken ct)
    {
        // The refresh token is an opaque random string from GenerateRefreshToken,
        // not a JWT — it has no claims to read. Look the owner up by the stored
        // value instead of trying to parse it.
        var presented = request.Payload.RefreshToken;

        if (string.IsNullOrWhiteSpace(presented))
            throw new UnauthorizedAccessException("Invalid token");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.RefreshToken == presented, ct)
            ?? throw new UnauthorizedAccessException("Invalid token");

        if (user.RefreshTokenExpiry is null || user.RefreshTokenExpiry < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expired");

        var newAccess  = _tokens.GenerateAccessToken(user);
        var newRefresh = _tokens.GenerateRefreshToken();

        user.RefreshToken       = newRefresh;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(newAccess, newRefresh, user.Username, user.Role);
    }
}