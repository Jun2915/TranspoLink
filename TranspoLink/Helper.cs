using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
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
    // ------------------------------------------------------------------------
    // ID Generator Helper
    // ------------------------------------------------------------------------
    public string GetNextId(DB db, string role)
    {
        string prefix = role == "Admin" ? "A" : "C";

        var existingIds = db.Users
            .Where(u => u.Id.StartsWith(prefix))
            .Select(u => u.Id)
            .ToList()
            .Select(id => int.TryParse(id.Substring(1), out int n) ? n : 0)
            .OrderBy(n => n)
            .ToList();

        int nextNum = 1;
        foreach (var num in existingIds)
        {
            if (num == nextNum)
            {
                nextNum++;
            }
            else if (num > nextNum)
            {
                break;
            }
        }

        return prefix + nextNum.ToString("D3");
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

    // ------------------------------------------------------------------------
    // Photo Upload Helper Functions
    // ------------------------------------------------------------------------

    public string ValidatePhoto(IFormFile f)
    {
        // Add null check
        if (f == null || f.Length == 0)
        {
            return "Please select a photo.";
        }

        var reType = new Regex(@"^image\/(jpeg|png)$", RegexOptions.IgnoreCase);
        var reName = new Regex(@"^.+\.(jpeg|jpg|png)$", RegexOptions.IgnoreCase);

        if (!reType.IsMatch(f.ContentType) || !reName.IsMatch(f.FileName))
        {
            return "Only JPG and PNG photos are allowed.";
        }
        else if (f.Length > 1 * 1024 * 1024)
        {
            return "Photo size cannot exceed 1MB.";
        }

        return "";
    }

    public string SavePhoto(IFormFile f, string folder)
    {
        var file = Guid.NewGuid().ToString("n") + ".jpg";
        var path = Path.Combine(en.WebRootPath, folder, file);

        var options = new ResizeOptions
        {
            Size = new(200, 200),
            Mode = ResizeMode.Crop,
        };

        using var stream = f.OpenReadStream();
        using var img = Image.Load(stream);
        img.Mutate(x => x.Resize(options));
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

                // Resize to match your system standard
                img.Mutate(x => x.Resize(new ResizeOptions
                {
                    Size = new Size(200, 200),
                    Mode = ResizeMode.Crop
                }));

                img.Save(path);
                return file;
            }
        }
        catch
        {
            // If download fails, return default
            return "default_photo.png";
        }
        return "default_photo.png";
    }

    public void DeletePhoto(string file, string folder)
    {
        file = Path.GetFileName(file);
        var path = Path.Combine(en.WebRootPath, folder, file);
        File.Delete(path);
    }

    public bool PhotoExists(string file, string folder)
    {
        if (string.IsNullOrEmpty(file)) return false;

        file = Path.GetFileName(file);
        var path = Path.Combine(en.WebRootPath, folder, file);
        return File.Exists(path);
    }


    // ------------------------------------------------------------------------
    // Security Helper Functions
    // ------------------------------------------------------------------------

    private readonly PasswordHasher<object> ph = new();

    public string HashPassword(string password)
    {
        return ph.HashPassword(0, password);
    }

    public bool VerifyPassword(string hash, string password)
    {
        return ph.VerifyHashedPassword(0, hash, password)
               == PasswordVerificationResult.Success;
    }

    public void SignIn(string email, string role, bool rememberMe)
    {
        List<Claim> claims =
        [
            new(ClaimTypes.Name, email),
            new(ClaimTypes.NameIdentifier, email),
            new(ClaimTypes.Role, role),
        ];

        ClaimsIdentity identity = new(claims, "Cookies");

        ClaimsPrincipal principal = new(identity);

        AuthenticationProperties properties = new()
        {
            IsPersistent = rememberMe,
            ExpiresUtc = rememberMe ? DateTime.UtcNow.AddDays(1) : null // Optional: Set explicit expiry for persistent
        };

        ct.HttpContext!.SignInAsync(principal, properties);
    }

    public void SignOut()
    {
        ct.HttpContext!.SignOutAsync();
    }

    public string RandomPassword()
    {
        string s = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        string password = "";

        Random r = new();

        for (int i = 1; i <= 10; i++)
        {
            password += s[r.Next(s.Length)];
        }

        return password;
    }



    // ------------------------------------------------------------------------
    // Email Helper Functions
    // ------------------------------------------------------------------------

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



    // ------------------------------------------------------------------------
    // DateTime Helper Functions
    // ------------------------------------------------------------------------

    // Return January (1) to December (12)
    public SelectList GetMonthList()
    {
        var list = new List<object>();

        for (int n = 1; n <= 12; n++)
        {
            list.Add(new
            {
                Id = n,
                Name = new DateTime(1, n, 1).ToString("MMMM"),
            });
        }

        return new SelectList(list, "Id", "Name");
    }

    // Return min to max years
    public SelectList GetYearList(int min, int max, bool reverse = false)
    {
        var list = new List<int>();

        for (int n = min; n <= max; n++)
        {
            list.Add(n);
        }

        if (reverse) list.Reverse();

        return new SelectList(list);
    }



    // ------------------------------------------------------------------------
    // Shopping Cart Helper Functions
    // ------------------------------------------------------------------------

    public Dictionary<string, int> GetCart()
    {
        return ct.HttpContext!.Session.Get<Dictionary<string, int>>("Cart") ?? [];
    }

    public void SetCart(Dictionary<string, int>? dict = null)
    {
        if (dict == null)
        {
            ct.HttpContext!.Session.Remove("Cart");
        }
        else
        {
            ct.HttpContext!.Session.Set("Cart", dict);
        }
    }

    public bool VerifyCaptcha(string responseToken)
    {
        if (string.IsNullOrEmpty(responseToken)) return false;

        string secretKey = "6Ld3iBosAAAAAK2pXnJdiSD7YCX-wnUjBnZ2C28o";
        string apiUrl = $"https://www.google.com/recaptcha/api/siteverify?secret={secretKey}&response={responseToken}";

        using (var client = new HttpClient())
        {
            var result = client.GetStringAsync(apiUrl).Result;
            using var doc = JsonDocument.Parse(result);
            return doc.RootElement.GetProperty("success").GetBoolean();
        }
    }

    // ------------------------------------------------------------------------
    // OTP Helper
    // ------------------------------------------------------------------------
    public string GenerateOTP()
    {
        Random r = new();
        return r.Next(100000, 999999).ToString();
    }

    public void LogActivity(DB db, string adminId, string action, string details, string icon, string cssClass)
    {
        var log = new AuditLog
        {
            AdminId = adminId,
            Action = action,
            Details = details,
            Icon = icon,
            CssClass = cssClass,
            Timestamp = DateTime.Now
        };
        db.AuditLogs.Add(log);
        db.SaveChanges();
    }

    // Add this method inside your Helper class
    public string GetNextVehicleNumber(DB db)
    {
        // Find all VehicleNumbers that start with 'V'
        var existingIds = db.Vehicles
            .Where(v => v.VehicleNumber.StartsWith("V"))
            .Select(v => v.VehicleNumber)
            .ToList()
            // Extract the number part (e.g., "V005" -> 5)
            .Select(id => int.TryParse(id.Substring(1), out int n) ? n : 0)
            .OrderBy(n => n)
            .ToList();

        // Find the first missing number (Gap)
        int nextNum = 1;
        foreach (var num in existingIds)
        {
            if (num == nextNum) nextNum++;
            else if (num > nextNum) break;
        }

        // Return format V001, V002, etc.
        return "V" + nextNum.ToString("D3");
    }
    // ------------------------------------------------------------------------
    // Booking Helper Functions
    // ------------------------------------------------------------------------
    public List<string> GetSeatsForBooking(int bookingId)
    {
        // Fetch seat numbers from the database for a confirmed/pending booking.
        // Since we don't have a dedicated Seat table, this is simulated:
        // This assumes seat numbers are stored in a comma-separated format in the Booking model 
        // or a related table (which we need to add later for full functionality). 
        // For now, we simulate finding the booked seats via the bookingId, which is not ideal.

        // **FIX: We will assume there is a BookingSeat table to fetch this data later, 
        // and for this helper, we'll return an empty list as we don't know the booking details yet.**
        return new List<string>();
    }

    public List<string> GetBookedSeatsForTrip(string tripId)
    {


        // In a real application, you would query the DB
        return new List<string> { "" }; // General busy seats
    }


    public List<string> GenerateSeatLayout(int totalSeats)
    {
        // Generates a simulated 2+1 layout (A, B, C) for demonstration (10 rows max).
        List<string> seats = new List<string>();
        int rows = Math.Min(10, totalSeats / 3);
        for (int r = 1; r <= rows; r++)
        {
            // FIX: Ensure seat labels are correct (e.g., 1A, 1B, 1C)
            seats.Add(r + "A"); // Pair
            seats.Add(r + "B"); // Pair
            seats.Add(r + "C"); // Single
        }
        return seats;
    }


    public string GenerateBookingRef()
    {
        // Generates a 10-character reference number
        string s = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        Random r = new();
        string reference = "";
        for (int i = 0; i < 10; i++)
        {
            reference += s[r.Next(s.Length)];
        }
        return reference;
    }
}

