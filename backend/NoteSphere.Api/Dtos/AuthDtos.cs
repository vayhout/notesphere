namespace NoteSphere.Api.Dtos;

public sealed record RegisterRequest(string Email, string Password, string DisplayName);
public sealed record LoginRequest(string Email, string Password);

public sealed record AuthResponse(string AccessToken, UserProfile User);
public sealed record UserProfile(Guid Id, string Email, string DisplayName);
