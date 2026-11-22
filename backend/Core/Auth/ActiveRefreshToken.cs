using NodaTime;

namespace JwtDemo.Core.Auth;

public class ActiveRefreshToken
{
    public Guid Id { get; set; }
    public int UserId { get; set; }

    /// <summary>
    ///     SHA-256 hash of the refresh token - the token itself is never stored
    /// </summary>
    public required byte[] TokenHash { get; set; }

    public required Instant Expiration { get; set; }
}
