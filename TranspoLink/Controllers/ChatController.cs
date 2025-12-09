using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace TranspoLink.Controllers;

[Authorize]
public class ChatController(DB db, Helper hp) : Controller
{

    public IActionResult Support()
    {
        // Get unique users who have messaged Admin
        var userIds = db.ChatMessages
            .Where(m => m.ReceiverId == "Admin")
            .Select(m => m.SenderId)
            .Distinct()
            .ToList();

        // Fetch details to display names in the sidebar
        var users = db.Users
            .Where(u => userIds.Contains(u.Email) || userIds.Contains(u.Phone))
            .Select(u => new { Id = u.Email ?? u.Phone, u.Name, Role = u.Role })
            .ToList();

        ViewBag.ActiveUsers = users;
        return View();
    }

    // ======================================================================
    // SHARED ACTIONS (User & Admin)
    // ======================================================================

    [HttpPost]
    public IActionResult UploadPhoto(IFormFile file)
    {
        if (file == null || file.Length == 0)
            return Json(new { success = false });

        // Ensure "images" folder exists in wwwroot
        string savedFileName = hp.SavePhoto(file, "images");
        return Json(new { success = true, url = savedFileName });
    }

    // ======================================================================
    // USER ACTIONS
    // ======================================================================

    [HttpGet]
    public IActionResult GetMyChatHistory()
    {
        var currentUserId = User.Identity.Name;

        var messages = db.ChatMessages
            .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
            .OrderBy(m => m.Timestamp)
            .ToList()
            .Select(m => new
            {
                // If Sender is Admin, use their real name (e.g. "Sarah"), else "Support"
                // If "System", it remains "System"
                sender = m.SenderId == currentUserId ? "Me" : (m.SenderName == "System" ? "System" : "Support"),
                name = m.SenderId == "Admin" ? m.SenderName : "You",
                text = m.Message,
                photo = m.PhotoUrl,
                time = m.Timestamp.ToString("HH:mm"),
                fullDate = m.Timestamp.ToString("dd MMM, HH:mm")
            })
            .ToList();

        return Json(messages);
    }

    // ======================================================================
    // ADMIN ACTIONS
    // ======================================================================

    [HttpGet]
    [Authorize(Roles = "Admin")]
    public IActionResult GetChatHistory(string userId)
    {
        var messages = db.ChatMessages
            .Where(m => (m.SenderId == userId && m.ReceiverId == "Admin") ||
                        (m.SenderId == "Admin" && m.ReceiverId == userId))
            .OrderBy(m => m.Timestamp)
            .ToList();

        var result = messages.Select(m => new
        {
            sender = m.SenderId == "Admin" ? "Me" : "User",
            name = m.SenderName,
            text = m.Message,
            photo = m.PhotoUrl,
            time = m.Timestamp.ToString("HH:mm"),
            // Check if the sender is another admin (not the current logged-in one viewing this)
            isAdminSender = db.Admins.Any(a => a.Email == m.SenderId || a.Phone == m.SenderId),
            fullDate = m.Timestamp.ToString("dd MMM, HH:mm")
        });

        return Json(result);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult EndChatSession(string userId)
    {
        var currentAdmin = db.Users.FirstOrDefault(u => u.Email == User.Identity.Name || u.Phone == User.Identity.Name);
        string adminName = currentAdmin?.Name ?? "Admin";

        var endMsg = new ChatMessage
        {
            SenderId = "Admin",
            ReceiverId = userId,
            SenderName = "System",
            Message = $"--- Session Ended by {adminName} ---",
            Timestamp = DateTime.Now,
            IsRead = true
        };

        db.ChatMessages.Add(endMsg);
        db.SaveChanges();

        return Json(new { success = true, endedBy = adminName, time = DateTime.Now.ToString("dd MMM, HH:mm") });
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public IActionResult DeleteConversation(string userId)
    {
        var messages = db.ChatMessages
            .Where(m => (m.SenderId == userId && m.ReceiverId == "Admin") ||
                        (m.SenderId == "Admin" && m.ReceiverId == userId));

        db.ChatMessages.RemoveRange(messages);
        db.SaveChanges();

        return Json(new { success = true });
    }
}