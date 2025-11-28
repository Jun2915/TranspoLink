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

        // 3. CHECK IF BLOCKED
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
                ModelState.AddModelError("", "Login credentials not matched.");
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
                hp.SignIn(identifier, u.Role, vm.RememberMe);

                if (string.IsNullOrEmpty(returnURL))
                {
                    if (u.Role == "Admin")
                    {
                        return RedirectToAction("Index", "Admin");
                    }

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
            string nextId = hp.GetNextId(db, "Member");

            var newMember = new Member()
            {
                Id = nextId,
                Email = vm.Email,
                Phone = vm.PhoneNumber,
                Hash = hp.HashPassword(vm.Password),
                Name = vm.Name,
                PhotoURL = vm.Photo != null ? hp.SavePhoto(vm.Photo, "images") : "default_photo.png",
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

        // 1. Check if the New Email/Phone is already taken by SOMEONE ELSE
        if (!string.IsNullOrEmpty(vm.Email) && db.Users.Any(x => x.Id != u.Id && x.Email == vm.Email))
        {
            ModelState.AddModelError("Email", "This Email is already in use by another account.");
        }

        if (!string.IsNullOrEmpty(vm.Phone) && db.Users.Any(x => x.Id != u.Id && x.Phone == vm.Phone))
        {
            ModelState.AddModelError("Phone", "This Phone Number is already in use by another account.");
        }

        // 2. Validate Photo
        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            // 3. UPDATE THE DATA (This part was missing for Phone/Email!)
            u.Name = vm.Name;
            u.Email = vm.Email;
            u.Phone = vm.Phone;

            // Handle Photo
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
            hp.SignIn(newIdentifier, u.Role, true); // Keep them logged in

            TempData["Info"] = "Profile updated successfully.";
            return RedirectToAction();
        }

        // If validation failed, reload current data to show image
        if (u is Member m2) vm.PhotoURL = m2.PhotoURL;
        else if (u is Admin a2) vm.PhotoURL = a2.PhotoURL;

        return View(vm);
    }

    // ============================================================================
    // FORGOT PASSWORD / OTP FLOW (REPLACES OLD RESET PASSWORD)
    // ============================================================================

    // STEP 1: Request OTP
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

            // Store in Session
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
                // Simulated SMS
                Console.WriteLine($"[SMS SIMULATION] OTP for {u.Phone}: {otp}");
                TempData["Info"] = $"OTP sent to phone: {otp}";
            }

            return RedirectToAction("VerifyOtp");
        }
        return View(vm);
    }

    // STEP 2: Verify OTP
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

    // STEP 3: Reset Password
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

                // UNLOCK ACCOUNT if it was blocked
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