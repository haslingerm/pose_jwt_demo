using FluentValidation;
using JwtDemo.Core;
using JwtDemo.Core.Auth;
using JwtDemo.Core.Users;
using JwtDemo.Util;
using Microsoft.AspNetCore.Http.HttpResults;
using Microsoft.AspNetCore.Mvc;

namespace JwtDemo.Endpoints;

/// <summary>
///     The endpoints of the user resource
/// </summary>
public static class UserEndpoints
{
    extension(IEndpointRouteBuilder app)
    {
        public void MapUserEndpoints(bool isDevelopment)
        {
            var users = app.MapGroup("/api/users")
                           .WithTags("Users");

            #region Insecure Demo Code - Ignore!

            // !! This is obviously just a placeholder: anonymous users can register themselves - even as Admin !!
            // To limit the damage, the endpoint is only mapped during development - in production it does not exist.
            // A real application would e.g. let users register with the lowest role only (+ email verification)
            // and only allow admins to assign higher roles.
            if (isDevelopment)
            {
                users.MapPost("/registrations", RegisterAsync)
                     .AllowAnonymous()
                     .RequireRateLimiting(Setup.AuthRateLimitPolicyName);
            }

            #endregion
        }
    }

    private static async Task<Results<Created, ValidationProblem, Conflict>> RegisterAsync(
        [FromBody] RegisterRequest request,
        [FromServices] IUserService userService,
        [FromServices] ITransactionProvider transaction)
    {
        if (new RegisterRequest.Validator().Check(request) is { } validationProblem)
        {
            return validationProblem;
        }

        await transaction.BeginTransactionAsync();

        AddUserResult addUserResult = await userService.AddNewUser(request.Username, request.Password, request.Role);

        return addUserResult switch
        {
            User => await transaction.CommitAndReturnAsync(TypedResults.Created()),
            DuplicateUsername => TypedResults.Conflict()
        };
    }
}

public sealed class RegisterRequest
{
    public required string Username { get; init; }
    public required string Password { get; init; }
    public required UserRole Role { get; init; }

    public sealed class Validator : AbstractValidator<RegisterRequest>
    {
        public Validator()
        {
            RuleFor(x => x.Username)
                .Length(UserLimits.UsernameMinLength, UserLimits.UsernameMaxLength)
                .Matches("^[a-zA-Z0-9_.-]+$")
                .WithMessage("Only letters, digits, '_', '.' and '-' are allowed");
            RuleFor(x => x.Password)
                .Length(UserLimits.PasswordMinLength, UserLimits.PasswordMaxLength);
            RuleFor(x => x.Role).IsInEnum();
        }
    }
}
