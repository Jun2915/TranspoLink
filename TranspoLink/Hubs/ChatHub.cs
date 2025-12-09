using Microsoft.AspNetCore.SignalR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;

namespace TranspoLink.Hubs;

[Authorize]
public class ChatHub(DB db) : Hub
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
    public async Task SendMessageToSupport(string message, string? photoUrl, string? replyContext)
    {
        var userIdentifier = Context.User.Identity.Name;

        // Find User Name
        var user = await db.Users.FirstOrDefaultAsync(u => u.Email == userIdentifier || u.Phone == userIdentifier || u.Id == userIdentifier);
        string displayName = user?.Name ?? "Guest";

        string finalMessage = message;
        if (!string.IsNullOrEmpty(replyContext))
        {
            finalMessage = $"[Replying to: \"{replyContext}\"]\n{message}";
        }

        var chatLog = new ChatMessage
        {
            SenderId = userIdentifier,
            ReceiverId = "Admin",
            SenderName = displayName,
            Message = finalMessage,
            PhotoUrl = photoUrl,
            Timestamp = DateTime.Now,
            IsRead = false
        };
        db.ChatMessages.Add(chatLog);
        await db.SaveChangesAsync();

        // Send to Admins
        await Clients.Group("SupportTeam").SendAsync("ReceiveSupportMessage", userIdentifier, displayName, finalMessage, photoUrl, DateTime.Now.ToString("HH:mm"));

        // Echo to User
        await Clients.Caller.SendAsync("ReceiveMyMessage", finalMessage, photoUrl, DateTime.Now.ToString("HH:mm"));
    }

    // ADMIN replying to USER (Fixes the Name Display)
    [Authorize(Roles = "Admin")]
    public async Task ReplyToUser(string targetUserId, string message)
    {
        var adminIdentifier = Context.User.Identity.Name;

        // 1. ROBUST LOOKUP: Check ID, Email, and Phone to find the specific Admin
        var adminUser = await db.Admins.FirstOrDefaultAsync(a => a.Id == adminIdentifier || a.Email == adminIdentifier || a.Phone == adminIdentifier);

        // 2. Get Real Name (e.g. "Sarah") or fallback to "Admin"
        string adminName = adminUser?.Name ?? "Admin";

        var chatLog = new ChatMessage
        {
            SenderId = "Admin",
            ReceiverId = targetUserId,
            SenderName = adminName,
            Message = message,
            Timestamp = DateTime.Now,
            IsRead = false
        };
        db.ChatMessages.Add(chatLog);
        await db.SaveChangesAsync();

        // 3. Send "Sarah" to the User
        await Clients.User(targetUserId).SendAsync("ReceiveAdminReply", adminName, message, null, DateTime.Now.ToString("HH:mm"));

        // Echo back to Admin
        await Clients.Caller.SendAsync("ReceiveMyReply", targetUserId, message, null, DateTime.Now.ToString("HH:mm"), adminName);
    }
}