using NodaTime;

namespace JwtDemo.Core.Auth;

public class ActiveRefreshToken
{
    public Guid Id { get; set; }
    public int UserId { get; set; }
    public required byte[] TokenHash { get; set; }
    public required Instant Expiration { get; set; }
}
