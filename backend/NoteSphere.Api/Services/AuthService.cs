using BCrypt.Net;
using NoteSphere.Api.Data;
using NoteSphere.Api.Dtos;
using NoteSphere.Api.Models;
using NoteSphere.Api.Security;

namespace NoteSphere.Api.Services;

public sealed class AuthService
{
    private readonly UserRepository _users;
    private readonly JwtTokenService _jwt;

    public AuthService(UserRepository users, IConfiguration config)
    {
        _users = users;
        _jwt = new JwtTokenService(config);
    }

    public async Task<AuthResponse> RegisterAsync(RegisterRequest req)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(email) || !email.Contains('@'))
            throw new ArgumentException("Invalid email.");
        if (string.IsNullOrWhiteSpace(req.Password) || req.Password.Length < 8)
            throw new ArgumentException("Password must be at least 8 characters.");
        if (string.IsNullOrWhiteSpace(req.DisplayName))
            throw new ArgumentException("Display name is required.");

        var existing = await _users.GetByEmailAsync(email);
        if (existing is not null)
            throw new InvalidOperationException("Email is already registered.");

        var user = new User
        {
            Id = Guid.NewGuid(),
            Email = email,
            DisplayName = req.DisplayName.Trim(),
            PasswordHash = BCrypt.Net.BCrypt.HashPassword(req.Password),
            CreatedAt = DateTime.UtcNow
        };

        await _users.CreateAsync(user);

        var token = _jwt.CreateAccessToken(user);
        return new AuthResponse(token, new UserProfile(user.Id, user.Email, user.DisplayName));
    }

    public async Task<AuthResponse> LoginAsync(LoginRequest req)
    {
        var email = (req.Email ?? "").Trim().ToLowerInvariant();
        var user = await _users.GetByEmailAsync(email);
        if (user is null) throw new UnauthorizedAccessException("Invalid email or password.");

        var ok = BCrypt.Net.BCrypt.Verify(req.Password ?? "", user.PasswordHash);
        if (!ok) throw new UnauthorizedAccessException("Invalid email or password.");

        var token = _jwt.CreateAccessToken(user);
        return new AuthResponse(token, new UserProfile(user.Id, user.Email, user.DisplayName));
    }
}
