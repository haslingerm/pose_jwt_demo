using JwtDemo.Core;
using JwtDemo.Core.Users;
using JwtDemo.Model;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace JwtDemo.Controllers;

[ApiController]
[Route("api/users")]
public sealed class UserController(
    IUserService userService,
    ITransactionProvider transaction,
    IHostEnvironment environment) : ControllerBase
{
    #region Insecure Demo Code - Ignore!

    // !! This is obviously just a placeholder: anonymous users can register themselves - even as Admin !!
    // To limit the damage, the endpoint is only available during development.
    // A real application would e.g. let users register with the lowest role only (+ email verification)
    // and only allow admins to assign higher roles.
    [HttpPost("registrations")]
    [AllowAnonymous]
    [EnableRateLimiting(Setup.AuthRateLimitPolicyName)]
    public async ValueTask<IActionResult> Register([FromBody] RegisterRequest request)
    {
        if (!environment.IsDevelopment())
        {
            return NotFound();
        }

        await transaction.BeginTransactionAsync();

        var addNewResult = await userService.AddNewUser(request.Username, request.Password, request.Role);

        return addNewResult switch
        {
            // 'User _' instead of just 'User', because the latter would refer to the ControllerBase.User property
            User _ => await transaction.CommitAndReturn(Created()),
            DuplicateUsername => Conflict()
        };
    }

    #endregion
}
