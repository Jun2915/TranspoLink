using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;

namespace TranspoLink.Hubs;

[Authorize]
public class ChatHub : Hub
{
    public override async Task OnConnectedAsync()
    {
        // Debugging Log: Check who connected
        Console.WriteLine($"[SignalR] Connected: {Context.User.Identity.Name} | IsAdmin: {Context.User.IsInRole("Admin")}");

        // If user is Admin, add to Support Group
        if (Context.User.IsInRole("Admin"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "SupportTeam");
            Console.WriteLine($"[SignalR] {Context.User.Identity.Name} joined SupportTeam.");
        }

        await base.OnConnectedAsync();
    }

    // USER calls this -> Sends to ADMINs
    public async Task SendMessageToSupport(string message)
    {
        var userEmail = Context.User.Identity.Name;
        Console.WriteLine($"[SignalR] Message from {userEmail}: {message}");

        // 1. Send to all Admins
        await Clients.Group("SupportTeam").SendAsync("ReceiveSupportMessage", userEmail, message, DateTime.Now.ToString("HH:mm"));

        // 2. Echo back to User (so they see it)
        await Clients.Caller.SendAsync("ReceiveMyMessage", message, DateTime.Now.ToString("HH:mm"));
    }

    // ADMIN calls this -> Sends to USER
    [Authorize(Roles = "Admin")]
    public async Task ReplyToUser(string targetUserId, string message)
    {
        var adminName = "Support"; // Or Context.User.Identity.Name

        // 1. Send to the specific User
        // Note: targetUserId must match the ClaimTypes.NameIdentifier we set in Helper.cs
        await Clients.User(targetUserId).SendAsync("ReceiveAdminReply", adminName, message, DateTime.Now.ToString("HH:mm"));

        // 2. Echo back to Admin
        await Clients.Caller.SendAsync("ReceiveMyReply", targetUserId, message, DateTime.Now.ToString("HH:mm"));
    }
}