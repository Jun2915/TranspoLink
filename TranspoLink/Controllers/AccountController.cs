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
        // Fix: Use FirstOrDefault instead of Find(string)
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
            TempData["Info"] = "Login successfully.";

            // Use User.Id or User.Name as the identifier claim if you prefer, 
            // but typical Identity setup uses Name/Email. 
            // Using Email/Phone here for display name.
            string identifier = u!.Email ?? u.Phone ?? u.Id.ToString();

            hp.SignIn(identifier, u.Role, vm.RememberMe);

            if (string.IsNullOrEmpty(returnURL))
            {
                return RedirectToAction("Index", "Home");
            }
            return Redirect(returnURL);
        }

        return View(vm);
    }

    // GET: Account/Logout
    public IActionResult Logout(string? returnURL)
    {
        TempData["Info"] = "Logout successfully.";

        hp.SignOut();

        return RedirectToAction("Index", "Home");
    }

    // GET: Account/AccessDenied
    public IActionResult AccessDenied(string? returnURL)
    {
        return View();
    }

    // ------------------------------------------------------------------------
    // Others
    // ------------------------------------------------------------------------

    // GET: Account/CheckEmail
    public bool CheckEmail(string email)
    {
        return !db.Users.Any(u => u.Email == email);
    }

    // GET: Account/Register
    public IActionResult Register()
    {
        return View();
    }

    // POST: Account/Register
    [HttpPost]
    public IActionResult Register(RegisterVM vm)
    {
        // Custom Validation: Must have Email OR Phone
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
            db.Members.Add(new Member()
            {
                Email = vm.Email,       // Can be null
                Phone = vm.PhoneNumber, // Added Phone mapping
                Hash = hp.HashPassword(vm.Password),
                Name = vm.Name,
                PhotoURL = vm.Photo != null ? hp.SavePhoto(vm.Photo, "images") : "/images/add_photo.png",
            });
            db.SaveChanges();

            TempData["Info"] = "Register successfully. Please login.";
            return RedirectToAction("Login");
        }

        return View(vm);
    }

    // GET: Account/UpdatePassword
    [Authorize]
    public IActionResult UpdatePassword()
    {
        return View();
    }

    // POST: Account/UpdatePassword
    [Authorize]
    [HttpPost]
    public IActionResult UpdatePassword(UpdatePasswordVM vm)
    {
        // Find by currently logged in user name (which is Email or Phone)
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

    // GET: Account/UpdateProfile
    [Authorize(Roles = "Member")]
    public IActionResult UpdateProfile()
    {
        var identifier = User.Identity!.Name;
        var m = db.Members.FirstOrDefault(u => u.Email == identifier || u.Phone == identifier);

        if (m == null) return RedirectToAction("Index", "Home");

        var vm = new UpdateProfileVM
        {
            Email = m.Email,
            Phone = m.Phone,
            Name = m.Name,
            PhotoURL = m.PhotoURL,
        };

        return View(vm);
    }

    // POST: Account/UpdateProfile
    [Authorize(Roles = "Member")]
    [HttpPost]
    public IActionResult UpdateProfile(UpdateProfileVM vm)
    {
        var identifier = User.Identity!.Name;
        var m = db.Members.FirstOrDefault(u => u.Email == identifier || u.Phone == identifier);

        if (m == null) return RedirectToAction("Index", "Home");

        if (vm.Photo != null)
        {
            var err = hp.ValidatePhoto(vm.Photo);
            if (err != "") ModelState.AddModelError("Photo", err);
        }

        if (ModelState.IsValid)
        {
            m.Name = vm.Name;
            // m.Email = vm.Email; // Generally we don't allow changing unique ID easily without re-verification
            // m.Phone = vm.Phone; 

            if (vm.Photo != null)
            {
                if (m.PhotoURL != null && m.PhotoURL != "/images/add_photo.png")
                {
                    hp.DeletePhoto(m.PhotoURL, "photos");
                }
                m.PhotoURL = hp.SavePhoto(vm.Photo, "photos");
            }

            db.SaveChanges();

            TempData["Info"] = "Profile updated.";
            return RedirectToAction();
        }

        vm.Email = m.Email;
        vm.Phone = m.Phone;
        vm.PhotoURL = m.PhotoURL;
        return View(vm);
    }

    // GET: Account/ResetPassword
    public IActionResult ResetPassword()
    {
        return View();
    }

    // POST: Account/ResetPassword
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

            // Send reset password email (Only works if user has email)
            if (!string.IsNullOrEmpty(u.Email))
            {
                SendResetPasswordEmail(u, password);
                TempData["Info"] = $"Password reset. Check your email.";
            }
            else
            {
                // If registered by phone only, we can't email. 
                TempData["Info"] = $"Password reset to: {password} (Please copy this)";
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

        var path = u switch
        {
            Admin => Path.Combine(en.WebRootPath, "photos", "admin.jpg"),
            Member m => Path.Combine(en.WebRootPath, "photos", m.PhotoURL ?? "default.jpg"),
            _ => "",
        };

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
            <p>
                Please <a href='{url}'>login</a>
                with your new password.
            </p>
            <p>From, 🐱 Super Admin</p>
        ";

        hp.SendEmail(mail);
    }
}