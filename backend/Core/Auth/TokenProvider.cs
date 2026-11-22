using System.Buffers.Text;
using System.Collections.Frozen;
using System.Security.Claims;
using System.Security.Cryptography;
using JwtDemo.Core.Users;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using NodaTime;

namespace JwtDemo.Core.Auth;

public sealed record TokenData(string Token, Instant Expiration);

public sealed record NewToken(TokenData AccessToken, TokenData RefreshToken);

public interface ITokenProvider
{
    public NewToken CreateToken(User user);
}

internal sealed class TokenProvider(IOptions<AuthSettings> options, IClock clock) : ITokenProvider
{
    // 256 bit of cryptographically secure randomness - impossible to guess
    private const int RefreshTokenBytes = 32;
    private static readonly FrozenDictionary<UserRole, IReadOnlyList<Claim>> roleClaims = GetRoleClaims();
    private static readonly JsonWebTokenHandler tokenHandler = new();
    private readonly AuthSettings _settings = options.Value;

    // HS256 = HMAC-SHA256: a symmetric algorithm, the same key is used to sign and to validate tokens.
    // That is fine as long as only this backend creates and validates tokens. If other services had to validate
    // our tokens as well, an asymmetric algorithm (e.g. RS256 or ES256) would be the better choice,
    // because then those services only need the public key and cannot create tokens themselves.
    private readonly SigningCredentials _signingCredentials =
        new(options.Value.CreateSigningKey(), SecurityAlgorithms.HmacSha256);

    public NewToken CreateToken(User user)
    {
        var now = clock.GetCurrentInstant();

        var accessToken = CreateAccessToken(user, now);
        var refreshToken = CreateRefreshToken(now);

        return new NewToken(accessToken, refreshToken);
    }

    private TokenData CreateAccessToken(User user, Instant now)
    {
        var expiration = now.Plus(Duration.FromMinutes(_settings.AccessTokenLifetimeMinutes));

        // Keep in mind: a JWT is signed, NOT encrypted - everybody can read its content (try https://jwt.io)!
        // So never put secrets or sensitive personal data in there.
        Claim[] claims =
        [
            // 'sub' (subject) is the standard claim for a stable, unique user identifier
            new(JwtRegisteredClaimNames.Sub, user.Id.ToString()),
            new(JwtRegisteredClaimNames.Name, user.Username),
            ..roleClaims[user.Role],
            // unique token id - would allow us to revoke single access tokens via a blocklist
            new(JwtRegisteredClaimNames.Jti, Guid.CreateVersion7().ToString("N"))
        ];

        var descriptor = new SecurityTokenDescriptor
        {
            Subject = new ClaimsIdentity(claims),
            IssuedAt = now.ToDateTimeUtc(),
            NotBefore = now.ToDateTimeUtc(),
            Expires = expiration.ToDateTimeUtc(),
            Issuer = _settings.TokenIssuer,
            Audience = _settings.TokenAudience,
            SigningCredentials = _signingCredentials
        };

        var token = tokenHandler.CreateToken(descriptor);

        return new TokenData(token, expiration);
    }

    private TokenData CreateRefreshToken(Instant now)
    {
        var expiration = now.Plus(Duration.FromMinutes(_settings.RefreshTokenLifetimeMinutes));

        // The refresh token is NOT a JWT, just a random value: it does not have to carry any information,
        // because it is only ever checked by this backend (against the hash we store in the database).
        var tokenStr = Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(RefreshTokenBytes));

        return new TokenData(tokenStr, expiration);
    }

    private static FrozenDictionary<UserRole, IReadOnlyList<Claim>> GetRoleClaims()
    {
        // In this basic authorization model, we assume a hierarchy of roles.
        // So an Admin may do everything a User can, but not vice versa.
        // A real world application might want to constrain more finely - that would then require more
        // complex logic when matching policies.

        IReadOnlyList<Claim> guest = [Create(UserRole.Guest)];
        IReadOnlyList<Claim> user = [..guest, Create(UserRole.User)];
        IReadOnlyList<Claim> admin = [..user, Create(UserRole.Admin)];
        var claims = new Dictionary<UserRole, IReadOnlyList<Claim>>
        {
            [UserRole.Guest] = guest,
            [UserRole.User] = user,
            [UserRole.Admin] = admin
        };

        return claims.ToFrozenDictionary();

        static Claim Create(UserRole r)
        {
            var enumStr = Enum.GetName(r)
                          ?? throw new ArgumentOutOfRangeException(nameof(r), r, "Unknown role");

            return new Claim(AuthClaimTypes.Role, enumStr);
        }
    }
}

public static class AuthClaimTypes
{
    // short claim names as used in the JWT itself - we disable the mapping to the long
    // 'http://schemas.xmlsoap.org/...' claim types of the .NET framework (see Setup.AddAuth)
    public const string Role = "role";
    public const string Name = JwtRegisteredClaimNames.Name;
}
