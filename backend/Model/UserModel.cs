using System.ComponentModel.DataAnnotations;
using JwtDemo.Core.Auth;

namespace JwtDemo.Model;

public sealed class RegisterRequest
{
    [StringLength(60, MinimumLength = 3)]
    [RegularExpression("^[a-zA-Z0-9_.-]+$", ErrorMessage = "Only letters, digits, '_', '.' and '-' are allowed")]
    public required string Username { get; init; }

    // NIST recommends a minimum length of 8 - and rather long passphrases than complex composition rules
    [StringLength(128, MinimumLength = 8)]
    public required string Password { get; init; }

    // otherwise e.g. { "role": 42 } would be accepted as well
    [EnumDataType(typeof(UserRole))]
    public required UserRole Role { get; init; }
}
