using System.Text;
using Microsoft.IdentityModel.Tokens;

namespace JwtDemo.Core;

public sealed class Settings
{
    public const string SectionKey = "AppSettings";

    public required string Timezone { get; set; }
    public required string ClientOrigin { get; set; }
}

public sealed class AuthSettings
{
    public const string SectionKey = "AuthSettings";

    private const int MinSigningKeyBytes = 32;

    public required string TokenSigningKey { get; set; }
    public required string TokenIssuer { get; set; }
    public required string TokenAudience { get; set; }
    public required int AccessTokenLifetimeMinutes { get; set; }
    public required int RefreshTokenLifetimeMinutes { get; set; }

    public SymmetricSecurityKey CreateSigningKey() => new(Encoding.UTF8.GetBytes(TokenSigningKey));

    public void Validate()
    {
        if (string.IsNullOrWhiteSpace(TokenIssuer) || string.IsNullOrWhiteSpace(TokenAudience))
        {
            throw new InvalidOperationException("JWT issuer and audience have to be configured");
        }

        // The signing key is a secret: it must never be committed to source control!
        // During development, it is stored in the user secrets (see backend Readme), in production
        // it would come from an environment variable or a key vault.
        if (string.IsNullOrWhiteSpace(TokenSigningKey)
            || Encoding.UTF8.GetByteCount(TokenSigningKey) < MinSigningKeyBytes)
        {
            throw new InvalidOperationException(
                $"""
                 The JWT signing key is missing or too short (min. {MinSigningKeyBytes} bytes).
                 During development, set it as a user secret (see backend Readme), e.g.:
                   dotnet user-secrets set "{SectionKey}:{nameof(TokenSigningKey)}" "$(openssl rand -base64 48)"
                 When deployed (user secrets are only loaded in the Development environment), provide it e.g. as
                 environment variable {SectionKey}__{nameof(TokenSigningKey)} from your secret store.
                 """);
        }

        if (AccessTokenLifetimeMinutes <= 0 || RefreshTokenLifetimeMinutes <= AccessTokenLifetimeMinutes)
        {
            throw new InvalidOperationException(
                "Token lifetimes are invalid: the refresh token has to live longer than the access token");
        }
    }
}
