using FluentValidation;
using JwtDemo.Core;
using JwtDemo.Core.Auth;
using JwtDemo.Core.Users;
using JwtDemo.Util;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;
using NodaTime;

namespace JwtDemo.Endpoints;

/// <summary>
///     Login, token refresh and logout
/// </summary>
public static class AuthEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapAuthEndpoints()
        {
            var auth = app.MapGroup("/api/auth")
                          .WithTags("Auth")
                          // no access token required (or possible) - the credentials are in the request body
                          .AllowAnonymous()
                          // all auth endpoints are rate limited, so attackers cannot try thousands of passwords
                          // or refresh tokens
                          .RequireRateLimiting(Setup.AuthRateLimitPolicyName);

            auth.MapPost("/logins", LoginAsync);
            // we don't need to send the (probably already expired) access token, the refresh token is the credential
            auth.MapPost("/token-refreshes", RefreshAsync);
            auth.MapPost("/logouts", LogoutAsync);
        }
    }

    // No try/catch required: an uncommitted transaction is rolled back automatically (see UnitOfWork) and
    // unexpected exceptions are logged and turned into a generic 500 response by the GlobalExceptionHandler.

    private static async Task<Results<Ok<TokenResponse>, ValidationProblem, UnauthorizedHttpResult>>
        LoginAsync([FromBody] LoginRequest request,
                   [FromServices] IAuthService authService,
                   [FromServices] ITransactionProvider transaction)
    {
        if (new LoginRequest.Validator().Check(request) is { } validationProblem)
        {
            return validationProblem;
        }

        await transaction.BeginTransactionAsync();

        LoginResult loginResult = await authService.AttemptLogin(request.Username, request.Password);

        return await ToTokenResponseAsync(loginResult, transaction);
    }

    private static async Task<Results<Ok<TokenResponse>, ValidationProblem, UnauthorizedHttpResult>>
        RefreshAsync([FromBody] TokenRequest request,
                     [FromServices] IAuthService authService,
                     [FromServices] ITransactionProvider transaction)
    {
        if (new TokenRequest.Validator().Check(request) is { } validationProblem)
        {
            return validationProblem;
        }

        await transaction.BeginTransactionAsync();

        LoginResult refreshResult = await authService.AttemptTokenRefresh(request.Username, request.RefreshToken);

        return await ToTokenResponseAsync(refreshResult, transaction);
    }

    private static async Task<Results<NoContent, ValidationProblem, UnauthorizedHttpResult>>
        LogoutAsync([FromBody] TokenRequest request,
                    [FromServices] IAuthService authService,
                    [FromServices] ITransactionProvider transaction)
    {
        if (new TokenRequest.Validator().Check(request) is { } validationProblem)
        {
            return validationProblem;
        }

        await transaction.BeginTransactionAsync();

        LogoutResult logoutResult = await authService.AttemptLogout(request.Username, request.RefreshToken);

        return logoutResult switch
        {
            Success => await transaction.CommitAndReturnAsync(TypedResults.NoContent()),
            InvalidCredentials => TypedResults.Unauthorized()
        };
    }

    private static async Task<Results<Ok<TokenResponse>, ValidationProblem, UnauthorizedHttpResult>>
        ToTokenResponseAsync(LoginResult result, ITransactionProvider transaction) =>
        // the compiler checks that we handle every case of the union - no default branch required
        result switch
        {
            NewToken token => await transaction.CommitAndReturnAsync(TypedResults.Ok(TokenResponse.FromToken(token))),
            // Always the same 401 - no 404 for unknown users etc. - so we don't leak which usernames exist
            InvalidCredentials => TypedResults.Unauthorized()
        };
}

public sealed class LoginRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }

    public sealed class Validator : AbstractValidator<LoginRequest>
    {
        public Validator()
        {
            // only length limits here - the password rules are checked when the password is set (registration),
            // checking them again during login would only reveal them to an attacker
            RuleFor(x => x.Username).NotEmpty().MaximumLength(UserLimits.UsernameMaxLength);
            RuleFor(x => x.Password).NotEmpty().MaximumLength(UserLimits.PasswordMaxLength);
        }
    }
}

/// <summary>
///     Used for token refresh and logout - both require the refresh token as a proof
/// </summary>
public sealed class TokenRequest
{
    public required string Username { get; init; }
    public required string RefreshToken { get; init; }

    public sealed class Validator : AbstractValidator<TokenRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Username).NotEmpty().MaximumLength(UserLimits.UsernameMaxLength);
            RuleFor(x => x.RefreshToken).NotEmpty().MaximumLength(UserLimits.RefreshTokenMaxLength);
        }
    }
}

public sealed class TokenResponse
{
    public required TokenDataDto AccessToken { get; init; }
    public required TokenDataDto RefreshToken { get; init; }

    public static TokenResponse FromToken(NewToken token) =>
        new()
        {
            AccessToken = TokenDataDto.FromTokenData(token.AccessToken),
            RefreshToken = TokenDataDto.FromTokenData(token.RefreshToken)
        };
}

public sealed class TokenDataDto
{
    public required string Token { get; init; }
    public required Instant Expiration { get; init; }

    public static TokenDataDto FromTokenData(TokenData tokenData) =>
        new()
        {
            Token = tokenData.Token,
            Expiration = tokenData.Expiration
        };
}
