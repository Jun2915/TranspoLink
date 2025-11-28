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
        // --- CAPTCHA CHECK START ---
        string captchaResponse = HttpContext.Request.Form["g-recaptcha-response"];
        if (!hp.VerifyCaptcha(captchaResponse))
        {
            ModelState.AddModelError("", "Please verify that you are not a robot.");
            return View(vm);
        }
        // --- CAPTCHA CHECK END ---

        User? u = null;

        if (!string.IsNullOrEmpty(vm.Email))
        {
            u = db.Users.FirstOrDefault(x => x.Email == vm.Email);
        }
        else if (!string.IsNullOrEmpty(vm.Phone))
        {
            u = db.Users.FirstOrDefault(x => x.Phone == vm.Phone);
        }

        if (u == null || !hp.VerifyPassword(u.Hash, vm.Password))
        {
            ModelState.AddModelError("", "Login credentials not matched.");
        }

        if (ModelState.IsValid)
        {
            TempData["Info"] = $"Welcome, {u!.Name}!";

            // u.Id is now a string (e.g., C001), so no need for ToString()
            string identifier = u!.Email ?? u.Phone ?? u.Id;
            hp.SignIn(identifier, u.Role, vm.RememberMe);

            if (string.IsNullOrEmpty(returnURL))
            {
                return RedirectToAction("Index", "Home");
            }
            return Redirect(returnURL);
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
        // --- CAPTCHA CHECK START ---
        string captchaResponse = HttpContext.Request.Form["g-recaptcha-response"];
        if (!hp.VerifyCaptcha(captchaResponse))
        {
            ModelState.AddModelError("", "Please verify that you are not a robot.");
            return View(vm);
        }
        // --- CAPTCHA CHECK END ---
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
                // FIX: Changed "images" to "photos"
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
    // UPDATE PROFILE (NOW SUPPORTS ADMIN AND MEMBER)
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

        // Determine photo URL based on role
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

            // Handle Photo Upload
            if (vm.Photo != null)
            {
                // Save new photo to 'photos' folder
                string newPhoto = hp.SavePhoto(vm.Photo, "photos");

                if (u is Member m)
                {
                    // Delete old photo if it's not the default
                    if (!string.IsNullOrEmpty(m.PhotoURL) && m.PhotoURL != "default_photo.png")
                    {
                        hp.DeletePhoto(m.PhotoURL, "photos");
                    }
                    m.PhotoURL = newPhoto;
                }
                else if (u is Admin a)
                {
                    // For Admin, only delete if it's NOT the static /images/ path
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

        // Restore data if validation fails
        vm.Email = u.Email;
        vm.Phone = u.Phone;
        
        if (u is Member m2) vm.PhotoURL = m2.PhotoURL;
        else if (u is Admin a2) vm.PhotoURL = a2.PhotoURL;

        return View(vm);
    }

    // ============================================================================

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
        
        // Handle different photo paths for email attachment
        if (u is Member m)
        {
             path = Path.Combine(en.WebRootPath, "photos", m.PhotoURL ?? "default_photo.png");
        }
        else if (u is Admin a)
        {
             if (a.PhotoURL != null && a.PhotoURL.StartsWith("/images/"))
             {
                 // Remove leading slash for Path.Combine
                 path = Path.Combine(en.WebRootPath, a.PhotoURL.TrimStart('/'));
             }
             else
             {
                 path = Path.Combine(en.WebRootPath, "photos", a.PhotoURL ?? "");
             }
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