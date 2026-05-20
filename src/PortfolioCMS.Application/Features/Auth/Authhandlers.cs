using FluentValidation;
using MediatR;
using Microsoft.EntityFrameworkCore;
using PortfolioCMS.Application.Common.Interfaces;
using PortfolioCMS.Application.DTOs;

namespace PortfolioCMS.Application.Features.Auth;

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
        var principal = _tokens.GetPrincipalFromExpiredToken(request.Payload.RefreshToken)
            ?? throw new UnauthorizedAccessException("Invalid token");

        var username = principal.Identity?.Name
            ?? throw new UnauthorizedAccessException("Invalid token");

        var user = await _db.Users
            .FirstOrDefaultAsync(u => u.Username == username, ct)
            ?? throw new UnauthorizedAccessException("User not found");

        if (user.RefreshToken != request.Payload.RefreshToken
            || user.RefreshTokenExpiry < DateTime.UtcNow)
            throw new UnauthorizedAccessException("Refresh token expired");

        var newAccess  = _tokens.GenerateAccessToken(user);
        var newRefresh = _tokens.GenerateRefreshToken();

        user.RefreshToken       = newRefresh;
        user.RefreshTokenExpiry = DateTime.UtcNow.AddDays(7);
        await _db.SaveChangesAsync(ct);

        return new AuthResponse(newAccess, newRefresh, user.Username, user.Role);
    }
}