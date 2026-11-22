using JwtDemo.Core.Auth;

namespace JwtDemo.Core.Users;

public readonly record struct DuplicateUsername;

public union AddUserResult(User, DuplicateUsername);

public interface IUserService
{
    public ValueTask<AddUserResult> AddNewUser(string username, string password, UserRole role);
}

public sealed class UserService(IUnitOfWork unitOfWork) : IUserService
{
    public async ValueTask<AddUserResult> AddNewUser(string username, string password, UserRole role)
    {
        // basic length checks are done by the request model validation (see RegisterRequest),
        // a real application might add more password rules or check against known breached passwords

        var repo = unitOfWork.UserRepository;
        if (await repo.GetByUsername(username) is User)
        {
            return new DuplicateUsername();
        }

        var hashedPassword = PasswordHasher.Hash(password);
        var newUser = new User
        {
            Role = role,
            Username = username,
            PasswordHash = hashedPassword.Hash,
            PasswordSalt = hashedPassword.Salt
        };

        repo.Add(newUser);

        return newUser;
    }
}
