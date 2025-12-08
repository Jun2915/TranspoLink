using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Google;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Net.Mail;
using System.Security.Claims;

namespace TranspoLink.Controllers;

public class AccountController(DB db,
                               IWebHostEnvironment en,
                               Helper hp) : Controller
{
    // GET: Account/Login
    public IActionResult Login()
    {
        // If already logged in, redirect to home
        if (User.Identity.IsAuthenticated)
        {
            return RedirectToAction("Index", "Home");
        }
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

        // 2. Find User (Combined Logic: Search both columns)
        User? u = null;
        if (!string.IsNullOrEmpty(vm.Input))
        {
            u = db.Users.FirstOrDefault(x => x.Email == vm.Input || x.Phone == vm.Input);
        }

        // --- CHECK IF BLOCKED ---
        if (u != null && u.IsBlocked)
        {
            ModelState.AddModelError("", "Your account has been blocked by Admin. Please contact support.");
            return View(vm);
        }

        // 3. CHECK IF LOCKED OUT
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
            if (u != null)
            {
                u.LoginRetryCount++;

                if (u.LoginRetryCount >= 10)
                {
                    u.LockoutEnd = DateTime.Now.AddHours(1);
                    ModelState.AddModelError("", "Too many failed attempts! Account locked for 1 hour.");
                }
                else if (u.LoginRetryCount >= 5)
                {
                    u.LockoutEnd = DateTime.Now.AddMinutes(10);
                    ModelState.AddModelError("", "Too many failed attempts! Account locked for 10 minutes.");
                }
                else if (u.LoginRetryCount >= 3)
                {
                    u.LockoutEnd = DateTime.Now.AddMinutes(5);
                    ModelState.AddModelError("", "Too many failed attempts! Account locked for 5 minutes.");
                }
                else
                {
                    int left = 3 - u.LoginRetryCount;
                    if (left > 0)
                        ModelState.AddModelError("", $"Login failed. You have {left} attempt(s) before lockout.");
                    else
                        ModelState.AddModelError("", "Login failed.");
                }
                db.SaveChanges();
            }
            else
            {
                ModelState.AddModelError("", "Invalid credentials.");
            }
        }

        // 5. SUCCESS LOGIN
        if (ModelState.IsValid)
        {
            if (u != null)
            {
                u.LoginRetryCount = 0;
                u.LockoutEnd = null;
                db.SaveChanges();

                TempData["Info"] = $"Welcome, {u.Name}!";

                string identifier = u.Email ?? u.Phone ?? u.Id;

                // Helper.SignIn handles the "Remember Me" logic internally via IsPersistent
                hp.SignIn(identifier, u.Role, vm.RememberMe);

                if (string.IsNullOrEmpty(returnURL))
                {
                    if (u.Role == "Admin")
                    {
                        hp.LogActivity(db, u.Id, "Logged In", "System Access", "🔑", "marker-login");
                        return RedirectToAction("Index", "Admin");
                    }

                    return RedirectToAction("Index", "Home");
                }
                return Redirect(returnURL);
            }
        }

        return View(vm);
    }

    public IActionResult GoogleLogin()
    {
        var properties = new AuthenticationProperties { RedirectUri = Url.Action("GoogleResponse") };
        return Challenge(properties, GoogleDefaults.AuthenticationScheme);
    }

    public async Task<IActionResult> GoogleResponse()
    {
        var result = await HttpContext.AuthenticateAsync(GoogleDefaults.AuthenticationScheme);

        if (!result.Succeeded)
        {
            TempData["Info"] = "Google Login Failed.";
            return RedirectToAction("Login");
        }

        // Retrieve Claims
        var claims = result.Principal.Identities.FirstOrDefault()?.Claims;
        var email = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Email)?.Value;
        var name = claims?.FirstOrDefault(c => c.Type == ClaimTypes.Name)?.Value;

        // 👇 UPDATED: Get the photo using the key we defined in Program.cs
        var photoUrl = claims?.FirstOrDefault(c => c.Type == "urn:google:picture")?.Value;

        if (string.IsNullOrEmpty(email))
        {
            TempData["Info"] = "Could not retrieve email from Google.";
            return RedirectToAction("Login");
        }

        // Check if user exists
        var u = db.Users.FirstOrDefault(x => x.Email == email);

        if (u == null)
        {
            // --- NEW USER REGISTRATION ---
            string nextId = hp.GetNextId(db, "Member");

            // Download photo
            string localPhoto = "default_photo.png";
            if (!string.IsNullOrEmpty(photoUrl))
            {
                localPhoto = hp.SavePhotoFromUrl(photoUrl, "images");
            }

            var newMember = new Member
            {
                Id = nextId,
                Email = email,
                Name = name ?? "Google User",
                Hash = hp.HashPassword(hp.RandomPassword()),
                PhotoURL = localPhoto,
                IsBlocked = false
            };

            db.Members.Add(newMember);
            db.SaveChanges();
            u = newMember;
        }
        else
        {
            // --- EXISTING USER UPDATE (Fix for your issue) ---
            if ((string.IsNullOrEmpty(u.Role) || u.Role == "Member") && // Only update Members
                (string.IsNullOrEmpty(u.Name) || u.Name == "Google User" || u is Member m && m.PhotoURL == "default_photo.png"))
            {
                if (!string.IsNullOrEmpty(photoUrl))
                {
                    // Cast to Member to access PhotoURL
                    if (u is Member member)
                    {
                        member.PhotoURL = hp.SavePhotoFromUrl(photoUrl, "images");
                        if (member.Name == "Google User" && !string.IsNullOrEmpty(name)) member.Name = name;

                        db.SaveChanges();
                    }
                }
            }
        }

        // Check Block Status
        if (u.IsBlocked)
        {
            TempData["Info"] = "Your account is blocked. Please contact admin.";
            return RedirectToAction("Login");
        }

        // Login the user
        string identifier = u.Email ?? u.Id;
        hp.SignIn(identifier, u.Role, true);
        TempData["Info"] = $"Welcome back, {u.Name}!";

        return RedirectToAction("Index", "Home");
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

    // Register Page
    public IActionResult Register()
    {
        if (User.Identity.IsAuthenticated)
        {
            return RedirectToAction("Index", "Home");
        }
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

        // Auto-detect logic
        string? email = null;
        string? phone = null;

        if (!string.IsNullOrEmpty(vm.Input))
        {
            if (vm.Input.Contains("@"))
            {
                email = vm.Input;
                if (db.Users.Any(u => u.Email == email))
                {
                    ModelState.AddModelError("Input", "This Email is already registered.");
                }
            }
            else
            {
                phone = vm.Input;
                if (!System.Text.RegularExpressions.Regex.IsMatch(phone, @"^[0-9+\-\s]+$"))
                {
                    ModelState.AddModelError("Input", "Invalid Phone Number format.");
                }
                else if (db.Users.Any(u => u.Phone == phone))
                {
                    ModelState.AddModelError("Input", "This Phone Number is already registered.");
                }
            }
        }

        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            string nextId = hp.GetNextId(db, "Member");

            var newMember = new Member()
            {
                Id = nextId,
                Email = email,
                Phone = phone,
                Hash = hp.HashPassword(vm.Password),
                Name = vm.Name,
                PhotoURL = vm.Photo != null ? hp.SavePhoto(vm.Photo, "images") : "default_photo.png",
            };

            db.Members.Add(newMember);
            db.SaveChanges();

            ViewBag.Success = true;
            ViewBag.RegisteredName = vm.Name;

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
            return RedirectToAction("ProfileManage");
        }

        return View();
    }

    [Authorize]
    public IActionResult ProfileManage()
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
    public IActionResult ProfileManage(UpdateProfileVM vm)
    {
        var identifier = User.Identity!.Name;
        var u = db.Users.FirstOrDefault(u => u.Email == identifier || u.Phone == identifier);

        if (u == null) return RedirectToAction("Index", "Home");

        if (!string.IsNullOrEmpty(vm.Email) && db.Users.Any(x => x.Id != u.Id && x.Email == vm.Email))
        {
            ModelState.AddModelError("Email", "This Email is already in use by another account.");
        }

        if (!string.IsNullOrEmpty(vm.Phone) && db.Users.Any(x => x.Id != u.Id && x.Phone == vm.Phone))
        {
            ModelState.AddModelError("Phone", "This Phone Number is already in use by another account.");
        }

        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            u.Name = vm.Name;
            u.Email = vm.Email;
            u.Phone = vm.Phone;

            if (vm.Photo != null)
            {
                string newPhoto = hp.SavePhoto(vm.Photo, "images");

                if (u is Member m)
                {
                    if (!string.IsNullOrEmpty(m.PhotoURL) && m.PhotoURL != "default_photo.png")
                    {
                        hp.DeletePhoto(m.PhotoURL, "images");
                    }
                    m.PhotoURL = newPhoto;
                }
                else if (u is Admin a)
                {
                    if (!string.IsNullOrEmpty(a.PhotoURL) && !a.PhotoURL.StartsWith("/images/"))
                    {
                        hp.DeletePhoto(a.PhotoURL, "images");
                    }
                    a.PhotoURL = newPhoto;
                }
            }

            db.SaveChanges();

            string newIdentifier = u.Email ?? u.Phone ?? u.Id;
            hp.SignIn(newIdentifier, u.Role, true);

            TempData["Info"] = "Profile updated successfully.";
            return RedirectToAction();
        }

        if (u is Member m2) vm.PhotoURL = m2.PhotoURL;
        else if (u is Admin a2) vm.PhotoURL = a2.PhotoURL;

        return View(vm);
    }

    // ============================================================================
    // FORGOT PASSWORD FLOW
    // ============================================================================

    public IActionResult ForgotPassword()
    {
        return View();
    }

    [HttpPost]
    public IActionResult ForgotPassword(ForgotPasswordVM vm)
    {
        if (ModelState.IsValid)
        {
            var u = db.Users.FirstOrDefault(x => x.Email == vm.EmailOrPhone || x.Phone == vm.EmailOrPhone);

            if (u == null)
            {
                ModelState.AddModelError("EmailOrPhone", "User not found.");
                return View(vm);
            }

            string otp = hp.GenerateOTP();

            HttpContext.Session.SetString("Reset_OTP", otp);
            HttpContext.Session.SetString("Reset_UserId", u.Id);
            HttpContext.Session.SetString("Reset_Target", vm.EmailOrPhone);

            if (vm.EmailOrPhone.Contains("@"))
            {
                SendOtpEmail(u, otp);
                TempData["Info"] = "OTP sent to your email.";
            }
            else
            {
                Console.WriteLine($"[SMS SIMULATION] OTP for {u.Phone}: {otp}");
                TempData["Info"] = $"OTP sent to phone: {otp}";
            }

            return RedirectToAction("VerifyOtp");
        }
        return View(vm);
    }

    public IActionResult VerifyOtp()
    {
        if (HttpContext.Session.GetString("Reset_OTP") == null)
        {
            return RedirectToAction("ForgotPassword");
        }
        return View();
    }

    [HttpPost]
    public IActionResult VerifyOtp(VerifyOtpVM vm)
    {
        string? sessionOtp = HttpContext.Session.GetString("Reset_OTP");

        if (ModelState.IsValid)
        {
            if (vm.Otp == sessionOtp)
            {
                HttpContext.Session.SetString("Reset_Verified", "true");
                TempData["Info"] = "OTP Verified!";
                return RedirectToAction("ResetPassword");
            }
            else
            {
                ModelState.AddModelError("Otp", "Invalid OTP.");
            }
        }
        return View(vm);
    }

    public IActionResult ResetPassword()
    {
        if (HttpContext.Session.GetString("Reset_Verified") != "true")
        {
            return RedirectToAction("ForgotPassword");
        }

        ViewBag.Target = HttpContext.Session.GetString("Reset_Target");
        return View();
    }

    [HttpPost]
    public IActionResult ResetPassword(ResetPasswordVM vm)
    {
        if (HttpContext.Session.GetString("Reset_Verified") != "true")
        {
            return RedirectToAction("ForgotPassword");
        }

        if (ModelState.IsValid)
        {
            string? userId = HttpContext.Session.GetString("Reset_UserId");
            var u = db.Users.Find(userId);

            if (u != null)
            {
                u.Hash = hp.HashPassword(vm.NewPassword);
                u.LoginRetryCount = 0;
                u.LockoutEnd = null;

                db.SaveChanges();
                HttpContext.Session.Clear();

                TempData["Info"] = "Password reset successfully. Please login.";
                return RedirectToAction("Login");
            }
        }

        ViewBag.Target = HttpContext.Session.GetString("Reset_Target");
        return View(vm);
    }

    private void SendOtpEmail(User u, string otp)
    {
        if (string.IsNullOrEmpty(u.Email)) return;

        var mail = new MailMessage();
        mail.To.Add(new MailAddress(u.Email, u.Name));
        mail.Subject = "Your Reset OTP Code";
        mail.IsBodyHtml = true;

        mail.Body = $@"
            <div style='font-family: Arial; padding: 20px; border: 1px solid #ddd; border-radius: 10px;'>
                <h2>Reset Password Request</h2>
                <p>Hello {u.Name},</p>
                <p>Your OTP code is:</p>
                <h1 style='color: #667eea; letter-spacing: 5px; font-size: 32px;'>{otp}</h1>
                <p>If you did not request this, please ignore this email.</p>
            </div>
        ";

        hp.SendEmail(mail);
    }
}