namespace JwtDemo.Core;

/// <summary>
///     Marker type for union cases that just signal "it worked" without carrying any data,
///     e.g. <c>union LogoutResult(Success, InvalidCredentials)</c>.
/// </summary>
public readonly record struct Success;
