using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Processing;
using System.Net;
using System.Net.Mail;
using System.Security.Claims;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace TranspoLink;

public class Helper(IWebHostEnvironment en,
                    IHttpContextAccessor ct,
                    IConfiguration cf)
{
    // --- ID Generator Helpers ---
    public string GetNextId(DB db, string role)
    {
        string prefix = role == "Admin" ? "A" : "C";
        var existingIds = db.Users
            .Where(u => u.Id.StartsWith(prefix))
            .Select(u => u.Id).ToList()
            .Select(id => int.TryParse(id.Substring(1), out int n) ? n : 0)
            .OrderBy(n => n).ToList();

        return prefix + GetNextNumber(existingIds).ToString("D3");
    }

    public string GetNextRouteId(DB db)
    {
        var ids = db.Routes.Select(r => r.Id).ToList()
                    .Where(id => id.StartsWith("R"))
                    .Select(id => int.TryParse(id.Substring(1), out int n) ? n : 0)
                    .OrderBy(n => n).ToList();
        return "R" + GetNextNumber(ids).ToString("D3");
    }

    public string GetNextTripId(DB db)
    {
        var ids = db.Trips.Select(t => t.Id).ToList()
                    .Where(id => id.StartsWith("T"))
                    .Select(id => int.TryParse(id.Substring(1), out int n) ? n : 0)
                    .OrderBy(n => n).ToList();
        return "T" + GetNextNumber(ids).ToString("D3");
    }

    public string GetNextRouteStopId(DB db)
    {
        var ids = db.RouteStops.Select(s => s.Id).ToList()
                    .Where(id => id.StartsWith("RS"))
                    .Select(id => int.TryParse(id.Substring(2), out int n) ? n : 0)
                    .OrderBy(n => n).ToList();
        return "RS" + GetNextNumber(ids).ToString("D3");
    }

    public string GetNextTripStopId(DB db)
    {
        var ids = db.TripStops.Select(s => s.Id).ToList()
                    .Where(id => id.StartsWith("TS"))
                    .Select(id => int.TryParse(id.Substring(2), out int n) ? n : 0)
                    .OrderBy(n => n).ToList();
        return "TS" + GetNextNumber(ids).ToString("D3");
    }

    public string GetNextVehicleNumber(DB db)
    {
        var existingIds = db.Vehicles
            .Where(v => v.VehicleNumber.StartsWith("V"))
            .Select(v => v.VehicleNumber).ToList()
            .Select(id => int.TryParse(id.Substring(1), out int n) ? n : 0)
            .OrderBy(n => n).ToList();

        return "V" + GetNextNumber(existingIds).ToString("D3");
    }

    private int GetNextNumber(List<int> existingNums)
    {
        int next = 1;
        foreach (var num in existingNums)
        {
            if (num == next)
                next++;
            else if (num > next)
                break;
        }
        return next;
    }

    // --- Photo Helpers ---
    public string ValidatePhoto(IFormFile f)
    {
        if (f == null || f.Length == 0)
            return "Please select a photo.";
        var reType = new Regex(@"^image\/(jpeg|png)$", RegexOptions.IgnoreCase);
        var reName = new Regex(@"^.+\.(jpeg|jpg|png)$", RegexOptions.IgnoreCase);

        if (!reType.IsMatch(f.ContentType) || !reName.IsMatch(f.FileName))
            return "Only JPG and PNG photos are allowed.";
        if (f.Length > 1 * 1024 * 1024)
            return "Photo size cannot exceed 1MB.";
        return "";
    }

    public string SavePhoto(IFormFile f, string folder)
    {
        var file = Guid.NewGuid().ToString("n") + ".jpg";
        var path = Path.Combine(en.WebRootPath, folder, file);
        using var stream = f.OpenReadStream();
        using var img = Image.Load(stream);
        img.Mutate(x => x.Resize(new ResizeOptions { Size = new(200, 200), Mode = ResizeMode.Crop }));
        img.Save(path);
        return file;
    }

    public string SavePhotoFromUrl(string url, string folder)
    {
        try
        {
            using var client = new HttpClient();
            var response = client.GetAsync(url).Result;
            if (response.IsSuccessStatusCode)
            {
                var file = Guid.NewGuid().ToString("n") + ".jpg";
                var path = Path.Combine(en.WebRootPath, folder, file);
                using var stream = response.Content.ReadAsStreamAsync().Result;
                using var img = Image.Load(stream);
                img.Mutate(x => x.Resize(new ResizeOptions { Size = new(200, 200), Mode = ResizeMode.Crop }));
                img.Save(path);
                return file;
            }
        }
        catch { return "default_photo.png"; }
        return "default_photo.png";
    }

    public void DeletePhoto(string file, string folder)
    {
        if (string.IsNullOrEmpty(file))
            return;
        var path = Path.Combine(en.WebRootPath, folder, Path.GetFileName(file));
        if (File.Exists(path))
            File.Delete(path);
    }

    public bool PhotoExists(string file, string folder)
    {
        if (string.IsNullOrEmpty(file))
            return false;
        var path = Path.Combine(en.WebRootPath, folder, Path.GetFileName(file));
        return File.Exists(path);
    }

    // --- Security & Auth Helpers ---
    private readonly PasswordHasher<object> ph = new();
    public string HashPassword(string password) => ph.HashPassword(0, password);
    public bool VerifyPassword(string hash, string password) =>
        ph.VerifyHashedPassword(0, hash, password) == PasswordVerificationResult.Success;

    public void SignIn(string email, string role, bool rememberMe)
    {
        List<Claim> claims = [
            new(ClaimTypes.Name, email),
            new(ClaimTypes.NameIdentifier, email),
            new(ClaimTypes.Role, role),
        ];
        ClaimsIdentity identity = new(claims, "Cookies");
        ClaimsPrincipal principal = new(identity);
        AuthenticationProperties properties = new()
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? DateTime.UtcNow.AddDays(7) : null
        };
        ct.HttpContext!.SignInAsync(principal, properties);
    }

    public void SignOut() => ct.HttpContext!.SignOutAsync();

    public string RandomPassword()
    {
        string s = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        Random r = new();
        return new string(Enumerable.Repeat(s, 10).Select(st => st[r.Next(st.Length)]).ToArray());
    }

    public string GenerateOTP() => new Random().Next(100000, 999999).ToString();

    // --- Email & Utility Helpers ---
    public void SendEmail(MailMessage mail)
    {
        string user = cf["Smtp:User"] ?? "";
        string pass = cf["Smtp:Pass"] ?? "";
        string name = cf["Smtp:Name"] ?? "";
        string host = cf["Smtp:Host"] ?? "";
        int port = cf.GetValue<int>("Smtp:Port");

        mail.From = new MailAddress(user, name);
        using var smtp = new SmtpClient
        {
            Host = host,
            Port = port,
            EnableSsl = true,
            Credentials = new NetworkCredential(user, pass),
        };
        smtp.Send(mail);
    }

    public SelectList GetMonthList()
    {
        var list = Enumerable.Range(1, 12).Select(n => new {
            Id = n,
            Name = new DateTime(1, n, 1).ToString("MMMM")
        });
        return new SelectList(list, "Id", "Name");
    }

    public SelectList GetYearList(int min, int max, bool reverse = false)
    {
        var list = Enumerable.Range(min, max - min + 1).ToList();
        if (reverse)
            list.Reverse();
        return new SelectList(list);
    }

    public void LogActivity(DB db, string adminId, string action, string details, string icon, string cssClass)
    {
        db.AuditLogs.Add(new AuditLog
        {
            AdminId = adminId,
            Action = action,
            Details = details,
            Icon = icon,
            CssClass = cssClass,
            Timestamp = DateTime.Now
        });
        db.SaveChanges();
    }

    public bool VerifyCaptcha(string responseToken)
    {
        if (string.IsNullOrEmpty(responseToken))
            return false;
        string secretKey = "6Ld3iBosAAAAAK2pXnJdiSD7YCX-wnUjBnZ2C28o";
        string apiUrl = $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={responseToken}";
        using var client = new HttpClient();
        var result = client.GetStringAsync(apiUrl).Result;
        using var doc = JsonDocument.Parse(result);
        return doc.RootElement.GetProperty("success").GetBoolean();
    }

    public Dictionary<string, int> GetCart() => ct.HttpContext!.Session.Get<Dictionary<string, int>>("Cart") ?? [];

    public void SetCart(Dictionary<string, int>? dict = null)
    {
        if (dict == null)
            ct.HttpContext!.Session.Remove("Cart");
        else
            ct.HttpContext!.Session.Set("Cart", dict);
    }

    public List<string> GetSeatsForBooking(int bookingId) => [];
    // Helper.cs
    public List<string> GetBookedSeatsForVehicle(string tripId, DB db)
    {
        // 1. 先找到当前行程对应的车辆 ID 和出发时间
        var currentTrip = db.Trips.FirstOrDefault(t => t.Id == tripId);
        if (currentTrip == null) return new List<string>();

        var vehicleId = currentTrip.VehicleId;
        var tripDate = currentTrip.DepartureTime.Date;

        // 2. 查找该车在该日期下，所有行程中已支付的座位号
        return db.Passengers
            .Include(p => p.Booking).ThenInclude(b => b.Trip)
            .Where(p => p.Booking.Trip.VehicleId == vehicleId &&
                        p.Booking.Trip.DepartureTime.Date == tripDate &&
                        p.Booking.Status == "Paid")
            .Select(p => p.SeatNumber)
            .Distinct() // 去重
            .ToList();
    }

    public List<string> GenerateSeatLayout(int totalSeats)
    {
        List<string> seats = [];
        int rows = Math.Min(10, totalSeats / 3);
        for (int r = 1; r <= rows; r++)
        {
            seats.Add(r + "A");
            seats.Add(r + "B");
            seats.Add(r + "C");
        }
        return seats;
    }

    public string GenerateBookingRef()
    {
        string s = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        Random r = new();
        return new string(Enumerable.Repeat(s, 10).Select(st => st[r.Next(st.Length)]).ToArray());
    }
}