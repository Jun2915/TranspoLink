using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;

namespace TranspoLink.Controllers;

public class AccountController(DB db,
                               IWebHostEnvironment en,
                               Helper hp) : Controller
{
    // GET: Account/Login
    public IActionResult Login()
    {
        return View();
    }

    // POST: Account/Login
    [HttpPost]
    public IActionResult Login(LoginVM vm, string? returnURL)
    {
        // 1. CAPTCHA Check
        string captchaResponse = HttpContext.Request.Form["g-recaptcha-response"];
        if (!hp.VerifyCaptcha(captchaResponse))
        {
            ModelState.AddModelError("", "Please verify that you are not a robot.");
            return View(vm);
        }

        // 2. Find User
        User? u = null;
        if (!string.IsNullOrEmpty(vm.Email))
        {
            u = db.Users.FirstOrDefault(x => x.Email == vm.Email);
        }
        else if (!string.IsNullOrEmpty(vm.Phone))
        {
            u = db.Users.FirstOrDefault(x => x.Phone == vm.Phone);
        }

        // 3. CHECK IF BLOCKED (Before checking password!)
        if (u != null && u.LockoutEnd > DateTime.Now)
        {
            var timeLeft = u.LockoutEnd.Value - DateTime.Now;
            var msg = timeLeft.TotalMinutes >= 60
                ? $"Account locked. Try again in {Math.Ceiling(timeLeft.TotalHours)} hour(s)."
                : $"Account locked. Try again in {Math.Ceiling(timeLeft.TotalMinutes)} minute(s).";

            ModelState.AddModelError("", msg);
            return View(vm);
        }

        // 4. Verify Password
        if (u == null || !hp.VerifyPassword(u.Hash, vm.Password))
        {
            // --- FAILED LOGIN LOGIC ---
            if (u != null)
            {
                u.LoginRetryCount++; // Increment failure count

                // Rule: 10 Fails = 1 Hour Block
                if (u.LoginRetryCount >= 10)
                {
                    u.LockoutEnd = DateTime.Now.AddHours(1);
                    ModelState.AddModelError("", "Too many failed attempts! Account locked for 1 hour.");
                }
                // Rule: 5 Fails = 10 Minute Block
                else if (u.LoginRetryCount >= 5)
                {
                    u.LockoutEnd = DateTime.Now.AddMinutes(10);
                    ModelState.AddModelError("", "Too many failed attempts! Account locked for 10 minutes.");
                }
                // Rule: 3 Fails = 5 Minute Block
                else if (u.LoginRetryCount >= 3)
                {
                    u.LockoutEnd = DateTime.Now.AddMinutes(5);
                    ModelState.AddModelError("", "Too many failed attempts! Account locked for 5 minutes.");
                }
                else
                {
                    // Just a warning
                    int left = 3 - u.LoginRetryCount;
                    if (left > 0)
                    {
                        ModelState.AddModelError("", $"Login failed. You have {left} attempt(s) before lockout.");
                    }
                    else
                    {
                        ModelState.AddModelError("", "Login failed.");
                    }
                }
                db.SaveChanges(); // Save the counter/lockout
            }
            else
            {
                ModelState.AddModelError("", "Login credentials not matched.");
            }
        }

        // 5. SUCCESS LOGIN LOGIC
        if (ModelState.IsValid)
        {
            // Reset counters on success!
            if (u != null)
            {
                u.LoginRetryCount = 0;
                u.LockoutEnd = null;
                db.SaveChanges();

                TempData["Info"] = $"Welcome, {u.Name}!";

                string identifier = u.Email ?? u.Phone ?? u.Id;
                hp.SignIn(identifier, u.Role, vm.RememberMe);

                if (string.IsNullOrEmpty(returnURL))
                {
                    return RedirectToAction("Index", "Home");
                }
                return Redirect(returnURL);
            }
        }

        return View(vm);
    }

    public IActionResult Logout(string? returnURL)
    {
        TempData["Info"] = "Logout successfully.";
        hp.SignOut();
        return RedirectToAction("Index", "Home");
    }

    public IActionResult AccessDenied(string? returnURL)
    {
        return View();
    }

    public bool CheckEmail(string email)
    {
        return !db.Users.Any(u => u.Email == email);
    }
    public bool CheckPhone(string phoneNumber)
    {
        return !db.Users.Any(u => u.Phone == phoneNumber);
    }

    public IActionResult Register()
    {
        return View();
    }

    // POST: Account/Register
    [HttpPost]
    public IActionResult Register(RegisterVM vm)
    {
        // CAPTCHA Check
        string captchaResponse = HttpContext.Request.Form["g-recaptcha-response"];
        if (!hp.VerifyCaptcha(captchaResponse))
        {
            ModelState.AddModelError("", "Please verify that you are not a robot.");
            return View(vm);
        }

        if (string.IsNullOrEmpty(vm.Email) && string.IsNullOrEmpty(vm.PhoneNumber))
        {
            ModelState.AddModelError("", "Please provide either an Email or a Phone number.");
        }

        if (!string.IsNullOrEmpty(vm.Email) && db.Users.Any(u => u.Email == vm.Email))
        {
            ModelState.AddModelError("Email", "Duplicated Email.");
        }

        if (!string.IsNullOrEmpty(vm.PhoneNumber) && db.Users.Any(u => u.Phone == vm.PhoneNumber))
        {
            ModelState.AddModelError("PhoneNumber", "Duplicated Phone Number.");
        }

        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            // Generate ID C001, C002, etc.
            string nextId = hp.GetNextId(db, "Member");

            var newMember = new Member()
            {
                Id = nextId,
                Email = vm.Email,
                Phone = vm.PhoneNumber,
                Hash = hp.HashPassword(vm.Password),
                Name = vm.Name,
                // Saved to "photos" to match view logic
                PhotoURL = vm.Photo != null ? hp.SavePhoto(vm.Photo, "photos") : "default_photo.png",
            };

            db.Members.Add(newMember);
            db.SaveChanges();

            ViewBag.Success = true;
            ViewBag.RegisteredName = vm.Name;
            ViewBag.RegisteredContact = !string.IsNullOrEmpty(vm.Email) ? vm.Email : vm.PhoneNumber;

            return View(vm);
        }

        return View(vm);
    }

    [Authorize]
    public IActionResult UpdatePassword()
    {
        return View();
    }

    [Authorize]
    [HttpPost]
    public IActionResult UpdatePassword(UpdatePasswordVM vm)
    {
        var identifier = User.Identity!.Name;
        var u = db.Users.FirstOrDefault(u => u.Email == identifier || u.Phone == identifier);

        if (u == null) return RedirectToAction("Index", "Home");

        if (!hp.VerifyPassword(u.Hash, vm.Current))
        {
            ModelState.AddModelError("Current", "Current Password not matched.");
        }

        if (ModelState.IsValid)
        {
            u.Hash = hp.HashPassword(vm.New);
            db.SaveChanges();

            TempData["Info"] = "Password updated.";
            return RedirectToAction();
        }

        return View();
    }

    // ============================================================================
    // PROFILE MANAGEMENT (Supports Admin & Member)
    // ============================================================================

    [Authorize]
    public IActionResult UpdateProfile()
    {
        var identifier = User.Identity!.Name;
        var u = db.Users.FirstOrDefault(u => u.Email == identifier || u.Phone == identifier);

        if (u == null) return RedirectToAction("Index", "Home");

        var vm = new UpdateProfileVM
        {
            Email = u.Email,
            Phone = u.Phone,
            Name = u.Name,
        };

        if (u is Member m) vm.PhotoURL = m.PhotoURL;
        else if (u is Admin a) vm.PhotoURL = a.PhotoURL;

        return View(vm);
    }

    [Authorize]
    [HttpPost]
    public IActionResult UpdateProfile(UpdateProfileVM vm)
    {
        var identifier = User.Identity!.Name;
        var u = db.Users.FirstOrDefault(u => u.Email == identifier || u.Phone == identifier);

        if (u == null) return RedirectToAction("Index", "Home");

        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            u.Name = vm.Name;

            if (vm.Photo != null)
            {
                string newPhoto = hp.SavePhoto(vm.Photo, "photos");

                if (u is Member m)
                {
                    if (!string.IsNullOrEmpty(m.PhotoURL) && m.PhotoURL != "default_photo.png")
                    {
                        hp.DeletePhoto(m.PhotoURL, "photos");
                    }
                    m.PhotoURL = newPhoto;
                }
                else if (u is Admin a)
                {
                    if (!string.IsNullOrEmpty(a.PhotoURL) && !a.PhotoURL.StartsWith("/images/"))
                    {
                        hp.DeletePhoto(a.PhotoURL, "photos");
                    }
                    a.PhotoURL = newPhoto;
                }
            }

            db.SaveChanges();
            TempData["Info"] = "Profile updated successfully.";
            return RedirectToAction();
        }

        vm.Email = u.Email;
        vm.Phone = u.Phone;

        if (u is Member m2) vm.PhotoURL = m2.PhotoURL;
        else if (u is Admin a2) vm.PhotoURL = a2.PhotoURL;

        return View(vm);
    }

    public IActionResult ResetPassword()
    {
        return View();
    }

    [HttpPost]
    public IActionResult ResetPassword(ResetPasswordVM vm)
    {
        var u = db.Users.FirstOrDefault(x => x.Email == vm.Email);

        if (u == null)
        {
            ModelState.AddModelError("Email", "Email not found.");
        }

        if (ModelState.IsValid)
        {
            string password = hp.RandomPassword();
            u!.Hash = hp.HashPassword(password);
            db.SaveChanges();

            if (!string.IsNullOrEmpty(u.Email))
            {
                SendResetPasswordEmail(u, password);
                TempData["Info"] = $"Password reset. Check your email.";
            }

            return RedirectToAction();
        }

        return View();
    }

    private void SendResetPasswordEmail(User u, string password)
    {
        if (string.IsNullOrEmpty(u.Email)) return;

        var mail = new MailMessage();
        mail.To.Add(new MailAddress(u.Email, u.Name));
        mail.Subject = "Reset Password";
        mail.IsBodyHtml = true;

        var url = Url.Action("Login", "Account", null, "https");

        var path = "";
        if (u is Member m)
        {
            path = Path.Combine(en.WebRootPath, "photos", m.PhotoURL ?? "default_photo.png");
        }
        else if (u is Admin a)
        {
            if (a.PhotoURL != null && a.PhotoURL.StartsWith("/images/"))
                path = Path.Combine(en.WebRootPath, a.PhotoURL.TrimStart('/'));
            else
                path = Path.Combine(en.WebRootPath, "photos", a.PhotoURL ?? "");
        }

        if (System.IO.File.Exists(path))
        {
            var att = new Attachment(path);
            mail.Attachments.Add(att);
            att.ContentId = "photo";
        }

        mail.Body = $@"
            <p>Dear {u.Name},<p>
            <p>Your password has been reset to:</p>
            <h1 style='color: red'>{password}</h1>
            <p>From, 🐱 Super Admin</p>
        ";

        hp.SendEmail(mail);
    }
}