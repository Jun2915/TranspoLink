using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace TranspoLink.Hubs;

[Authorize]
public class ChatHub(DB db) : Hub // Inject DB
{
    public override async Task OnConnectedAsync()
    {
        if (Context.User.IsInRole("Admin"))
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, "SupportTeam");
        }
        await base.OnConnectedAsync();
    }

    // USER sending to ADMIN
    public async Task SendMessageToSupport(string message)
    {
        var userIdentifier = Context.User.Identity.Name;

        // 1. Fetch User's Real Name
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == userIdentifier || u.Phone == userIdentifier);
        string displayName = user?.Name ?? "Guest"; // Uses Name (e.g., Li Jun)

        // 2. Save to DB
        var chatLog = new ChatMessage
        {
            SenderId = userIdentifier,
            ReceiverId = "Admin",
            SenderName = displayName,
            Message = message,
            Timestamp = DateTime.Now
        };
        db.ChatMessages.Add(chatLog);
        await db.SaveChangesAsync();

        // 3. Send to Admin with Display Name
        await Clients.Group("SupportTeam").SendAsync("ReceiveSupportMessage", userIdentifier, displayName, message, DateTime.Now.ToString("HH:mm"));

        // 4. Echo back to User
        await Clients.Caller.SendAsync("ReceiveMyMessage", message, DateTime.Now.ToString("HH:mm"));
    }

    // ADMIN replying to USER
    [Authorize(Roles = "Admin")]
    public async Task ReplyToUser(string targetUserId, string message)
    {
        // 1. Save to DB
        var chatLog = new ChatMessage
        {
            SenderId = "Admin",
            ReceiverId = targetUserId,
            SenderName = "Support Agent",
            Message = message,
            Timestamp = DateTime.Now
        };
        db.ChatMessages.Add(chatLog);
        await db.SaveChangesAsync();

        // 2. Send to User
        await Clients.User(targetUserId).SendAsync("ReceiveAdminReply", "Support", message, DateTime.Now.ToString("HH:mm"));

        // 3. Echo back to Admin
        await Clients.Caller.SendAsync("ReceiveMyReply", targetUserId, message, DateTime.Now.ToString("HH:mm"));
    }
}