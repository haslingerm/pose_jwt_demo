using System.Security.Cryptography;
using System.Text;
using JwtDemo.Core.Users;
using NodaTime;

namespace JwtDemo.Core.Auth;

/// <summary>
///     Returned for a wrong username, password or refresh token - on purpose, we do not tell the caller which
///     one was wrong, so attackers cannot find out which usernames exist.
/// </summary>
public readonly record struct InvalidCredentials;

// C# 15 union types: the result is exactly one of the listed types, and the compiler checks
// that a switch expression handles all of them
public union LoginResult(NewToken, InvalidCredentials);

public union LogoutResult(Success, InvalidCredentials);

public interface IAuthService
{
    public ValueTask<LoginResult> AttemptLogin(string username, string password);
    public ValueTask<LoginResult> AttemptTokenRefresh(string username, string refreshToken);
    public ValueTask<LogoutResult> AttemptLogout(string username, string refreshToken);
}

internal sealed class AuthService(ITokenProvider tokenProvider, IClock clock, IUnitOfWork unitOfWork) : IAuthService
{
    public async ValueTask<LoginResult> AttemptLogin(string username, string password)
    {
        if (await unitOfWork.UserRepository.GetByUsername(username) is not User user)
        {
            // still spend the time to hash the password, so the response time does not reveal that the user does not exist
            PasswordHasher.SimulateVerify(password);

            return new InvalidCredentials();
        }

        if (!PasswordHasher.Verify(password, new HashedPassword(user.PasswordHash, user.PasswordSalt)))
        {
            return new InvalidCredentials();
        }

        RemoveExpiredRefreshTokens(user);

        return IssueNewToken(user);
    }

    public async ValueTask<LoginResult> AttemptTokenRefresh(string username, string refreshToken)
    {
        if (await unitOfWork.UserRepository.GetByUsername(username) is not User user)
        {
            return new InvalidCredentials();
        }

        RemoveExpiredRefreshTokens(user);

        if (FindRefreshToken(user, refreshToken) is not { } activeRefreshToken)
        {
            // A real application should also detect the reuse of an already used (rotated) refresh token:
            // that is a strong sign the token was stolen, so all refresh tokens of the user should be revoked.
            return new InvalidCredentials();
        }

        // Refresh token rotation: every refresh token can be used exactly once, we hand out a new one each time.
        // If an attacker steals a refresh token, it becomes useless as soon as the real user refreshes.
        user.ActiveRefreshTokens.Remove(activeRefreshToken);

        // the new access token is created based on the current user data, so role changes are applied now
        return IssueNewToken(user);
    }

    public async ValueTask<LogoutResult> AttemptLogout(string username, string refreshToken)
    {
        // We only revoke the refresh token. The access token stays valid until it expires - we could add
        // a blocklist (based on the 'jti' claim) to revoke it immediately, but given the short lifetime of
        // access tokens we won't bother with that added complexity in this demo.
        if (await unitOfWork.UserRepository.GetByUsername(username) is not User user
            || FindRefreshToken(user, refreshToken) is not { } activeRefreshToken)
        {
            return new InvalidCredentials();
        }

        user.ActiveRefreshTokens.Remove(activeRefreshToken);

        return new Success();
    }

    private NewToken IssueNewToken(User user)
    {
        var token = tokenProvider.CreateToken(user);
        user.ActiveRefreshTokens.Add(new ActiveRefreshToken
        {
            // like passwords, refresh tokens are only stored as a hash: a leaked database does not leak usable tokens
            TokenHash = HashRefreshToken(token.RefreshToken.Token),
            Expiration = token.RefreshToken.Expiration
        });

        return token;
    }

    private static ActiveRefreshToken? FindRefreshToken(User user, string refreshToken)
    {
        var tokenHash = HashRefreshToken(refreshToken);

        return user.ActiveRefreshTokens.Find(t => CryptographicOperations.FixedTimeEquals(t.TokenHash, tokenHash));
    }

    private void RemoveExpiredRefreshTokens(User user)
    {
        var now = clock.GetCurrentInstant();
        user.ActiveRefreshTokens.RemoveAll(token => token.Expiration < now);
    }

    // Why no slow, salted hash (like for passwords) here? Passwords are chosen by humans, are often weak and reused,
    // so we have to make guessing them expensive. A refresh token consists of 256 random bits - guessing it is
    // impossible anyway, so a fast cryptographic hash is sufficient (and keeps token refreshes cheap).
    private static byte[] HashRefreshToken(string refreshToken) => SHA256.HashData(Encoding.UTF8.GetBytes(refreshToken));
}
