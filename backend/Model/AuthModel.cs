using System.ComponentModel.DataAnnotations;
using JwtDemo.Core.Auth;
using NodaTime;

namespace JwtDemo.Model;

// [ApiController] validates the data annotations automatically and returns 400 Bad Request if they are violated.
// The max lengths also protect us from absurdly large inputs (which would e.g. be expensive to hash).

public sealed class LoginRequest
{
    [StringLength(60, MinimumLength = 1)]
    public required string Username { get; init; }

    [StringLength(128, MinimumLength = 1)]
    public required string Password { get; init; }
}

public sealed class LogoutRequest
{
    [StringLength(60, MinimumLength = 1)]
    public required string Username { get; init; }

    [StringLength(100, MinimumLength = 1)]
    public required string RefreshToken { get; init; }
}

public sealed class TokenRefreshRequest
{
    [StringLength(60, MinimumLength = 1)]
    public required string Username { get; init; }

    [StringLength(100, MinimumLength = 1)]
    public required string RefreshToken { get; init; }
}

public sealed class TokenResponse
{
    public required TokenDataDto AccessToken { get; init; }
    public required TokenDataDto RefreshToken { get; init; }
}

public sealed class TokenDataDto
{
    public required string Token { get; init; }
    public required Instant Expiration { get; init; }
}

public static class AuthMappings
{
    extension(NewToken token)
    {
        public TokenResponse ToResponse() =>
            new()
            {
                AccessToken = token.AccessToken.ToDto(),
                RefreshToken = token.RefreshToken.ToDto()
            };
    }

    extension(TokenData tokenData)
    {
        public TokenDataDto ToDto() =>
            new()
            {
                Token = tokenData.Token,
                Expiration = tokenData.Expiration
            };
    }
}
