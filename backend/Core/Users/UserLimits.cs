namespace JwtDemo.Core.Users;

/// <summary>
///     Limits shared by the request validation and the database mapping
/// </summary>
public static class UserLimits
{
    public const int UsernameMinLength = 3;
    public const int UsernameMaxLength = 60;

    // NIST recommends a minimum length of 8 - and rather long passphrases than complex composition rules
    public const int PasswordMinLength = 8;

    // A maximum protects us from absurdly large inputs, which would be expensive to hash
    public const int PasswordMaxLength = 128;

    // our refresh tokens are 43 characters long (32 random bytes, Base64Url encoded)
    public const int RefreshTokenMaxLength = 100;
}
