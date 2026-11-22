using Microsoft.EntityFrameworkCore;

namespace JwtDemo.Core.Users;

public readonly record struct UserNotFound;

public union GetUserResult(User, UserNotFound);

public interface IUserRepository
{
    public void Add(User newUser);
    public ValueTask<GetUserResult> GetByUsername(string username);
}

public sealed class UserRepository(DbSet<User> users) : IUserRepository
{
    public void Add(User newUser)
    {
        users.Add(newUser);
    }

    public async ValueTask<GetUserResult> GetByUsername(string username)
    {
        // the username column uses a case-insensitive collation (see DatabaseContext), so 'admin' finds 'Admin'
        var user = await users
                         .Include(u => u.ActiveRefreshTokens)
                         .FirstOrDefaultAsync(u => u.Username == username);

        return user is not null ? user : new UserNotFound();
    }
}
