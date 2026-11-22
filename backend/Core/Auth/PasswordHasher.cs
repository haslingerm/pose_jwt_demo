using System.Security.Cryptography;
using System.Text;

namespace JwtDemo.Core.Auth;

public sealed record HashedPassword(byte[] Hash, byte[] Salt);

/// <summary>
///     Hashes passwords with PBKDF2 and a random salt per password.
///     In a real application you could also use the (well tested) <c>PasswordHasher&lt;TUser&gt;</c>
///     from ASP.NET Core Identity - here we do it ourselves to see what is going on.
/// </summary>
public static class PasswordHasher
{
    private const int SaltSize = 16; // 128 bit - recommended minimum by NIST
    private const int HashSize = 64; // 512 bit - output size of SHA-512

    // A deliberately slow hash function makes brute forcing leaked hashes expensive.
    // OWASP currently recommends at least 210,000 iterations for PBKDF2-HMAC-SHA512.
    // A real application would store the iteration count next to the hash, so it can be raised later on
    // (rehashing the password on the next successful login).
    private const int Iterations = 200_000;
    private static readonly HashAlgorithmName algorithm = HashAlgorithmName.SHA512;

    // used to spend the same amount of time on logins with an unknown username, see SimulateVerify
    private static readonly HashedPassword dummyPassword = Hash("not-a-real-password");

    public static HashedPassword Hash(string password)
    {
        var salt = RandomNumberGenerator.GetBytes(SaltSize);

        return new HashedPassword(DeriveHash(password, salt), salt);
    }

    public static bool Verify(string password, HashedPassword storedPassword)
    {
        var hash = DeriveHash(password, storedPassword.Salt);

        // A 'normal' comparison (e.g. SequenceEqual) stops at the first differing byte - the time it takes
        // leaks information about the stored value. FixedTimeEquals always compares all bytes.
        return CryptographicOperations.FixedTimeEquals(hash, storedPassword.Hash);
    }

    /// <summary>
    ///     Performs a password verification with the same cost as <see cref="Verify" />, but against a dummy hash.
    ///     Use it when the user does not exist: otherwise an attacker could find out which usernames are valid,
    ///     just by measuring how long the login request takes (no hashing = fast response = unknown user).
    /// </summary>
    public static void SimulateVerify(string password) => Verify(password, dummyPassword);

    private static byte[] DeriveHash(string password, byte[] salt) =>
        Rfc2898DeriveBytes.Pbkdf2(Encoding.UTF8.GetBytes(password), salt, Iterations, algorithm, HashSize);
}
