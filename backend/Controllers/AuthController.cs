using JwtDemo.Core;
using JwtDemo.Core.Auth;
using JwtDemo.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JwtDemo.Controllers;

[ApiController]
[AllowAnonymous]
// all auth endpoints are rate limited, so attackers cannot try thousands of passwords or refresh tokens
[EnableRateLimiting(Setup.AuthRateLimitPolicyName)]
[Route("api/auth")]
public sealed class AuthController(IAuthService authService, ITransactionProvider transaction) : ControllerBase
{
    // Unhandled exceptions don't need a try/catch here: the transaction is rolled back automatically
    // (see UnitOfWork) and the exception handler middleware logs them and returns a 500 response.

    [HttpPost("logins")]
    public async ValueTask<ActionResult<TokenResponse>> Login([FromBody] LoginRequest request)
    {
        await transaction.BeginTransactionAsync();

        var loginResult = await authService.AttemptLogin(request.Username, request.Password);

        return await ToTokenResponse(loginResult);
    }

    // we don't need to send the (probably already expired) access token here, the refresh token is the credential
    [HttpPost("token-refreshes")]
    public async ValueTask<ActionResult<TokenResponse>> Refresh([FromBody] TokenRefreshRequest request)
    {
        await transaction.BeginTransactionAsync();

        var refreshResult = await authService.AttemptTokenRefresh(request.Username, request.RefreshToken);

        return await ToTokenResponse(refreshResult);
    }

    [HttpPost("logouts")]
    public async ValueTask<IActionResult> Logout([FromBody] LogoutRequest request)
    {
        await transaction.BeginTransactionAsync();

        var logoutResult = await authService.AttemptLogout(request.Username, request.RefreshToken);

        return logoutResult switch
        {
            Success => await transaction.CommitAndReturn(NoContent()),
            InvalidCredentials => Unauthorized()
        };
    }

    private async ValueTask<ActionResult<TokenResponse>> ToTokenResponse(LoginResult result) =>
        // the compiler checks that we handle every case of the union - no default branch required
        result switch
        {
            NewToken token => await transaction.CommitAndReturn(Ok(token.ToResponse())),
            // Always the same 401 - no 404 for unknown users etc. - so we don't leak which usernames exist
            InvalidCredentials => Unauthorized()
        };
}
