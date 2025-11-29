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
    // ------------------------------------------------------------------------
    // ID Generator Helper (Fills Gaps)
    // ------------------------------------------------------------------------
    public string GetNextId(DB db, string role)
    {
        // Determine prefix: Admin -> "A", Member -> "C"
        string prefix = role == "Admin" ? "A" : "C";

        // 1. Get all existing ID numbers for this role
        // We fetch them into memory to parse and sort them easily
        var existingIds = db.Users
            .Where(u => u.Id.StartsWith(prefix))
            .Select(u => u.Id)
            .ToList()
            .Select(id => int.TryParse(id.Substring(1), out int n) ? n : 0) // Extract number part safely
            .OrderBy(n => n)
            .ToList();

        // 2. Find the first missing number (Gap)
        int nextNum = 1;
        foreach (var num in existingIds)
        {
            if (num == nextNum)
            {
                // If the number exists, move to the next expected number
                nextNum++;
            }
            else if (num > nextNum)
            {
                // If we found a number larger than expected, we found a gap!
                // E.g., We have [1, 3]. We expect 2, but found 3. So 2 is free.
                break;
            }
        }

        // 3. Format and return (e.g., A002)
        return prefix + nextNum.ToString("D3");
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
            new(ClaimTypes.Role, role),
        ];

        ClaimsIdentity identity = new(claims, "Cookies");

        ClaimsPrincipal principal = new(identity);

        AuthenticationProperties properties = new()
        {
            IsPersistent = rememberMe,
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

        string secretKey = "6Ld3iBosAAAAAK2pXnJdiSD7YCX-wnUjBnZ2C28o"; // <--- PASTE HERE
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
}