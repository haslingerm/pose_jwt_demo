using JwtDemo.Model;
using Microsoft.AspNetCore.SignalR;

namespace JwtDemo.Hubs;

public interface ITimeClient
{
    public Task ReceiveTime(TimeUpdate update);
}

public sealed class TimeHub : Hub<ITimeClient>
{
    public const string AuthenticatedGroupName = "AuthenticatedUsers";
    public const string UnauthenticatedGroupName = "UnauthenticatedUsers";

    public override async Task OnConnectedAsync()
    {
        // Context.User is set by the JWT bearer authentication, based on the token sent with the connection request
        var isAuthenticated = Context.User?.Identity?.IsAuthenticated ?? false;
        var group = isAuthenticated ? AuthenticatedGroupName : UnauthenticatedGroupName;
        await Groups.AddToGroupAsync(Context.ConnectionId, group);

        await base.OnConnectedAsync();
    }

    // no need to override OnDisconnectedAsync: SignalR removes closed connections from all groups automatically
}
