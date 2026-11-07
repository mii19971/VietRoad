namespace VietRoad.Api.Auth;

// Du lieu app gui len (JSON body).
public record RegisterRequest(string? Email, string? Password, string? DisplayName);
public record LoginRequest(string? Email, string? Password);
public record RefreshRequest(string? RefreshToken);

// Du lieu tra ve cho app.
public record UserProfile(Guid Id, string Email, string DisplayName, string Role, DateTimeOffset CreatedAt);

public record AuthResponse(
    string AccessToken,                    // gui kem moi request: "Authorization: Bearer <token>"
    DateTimeOffset AccessTokenExpiresAt,
    string RefreshToken,                   // app luu an toan, dung de lay access token moi
    DateTimeOffset RefreshTokenExpiresAt,
    UserProfile User);
