global using TranspoLink.Models;
global using TranspoLink;

var builder = WebApplication.CreateBuilder(args);

// Add Services
builder.Services.AddControllersWithViews();

//Database
builder.Services.AddSqlServer<DB>($@"
    Data Source=(LocalDB)\MSSQLLocalDB;
    AttachDbFilename={builder.Environment.ContentRootPath}\DB.mdf;
");

//Register custom services
builder.Services.AddScoped<Helper>();

//Authentication & Cookies
builder.Services.AddAuthentication("Cookies").AddCookie("Cookies", options =>
{
    options.LoginPath = "/Account/Login";
    options.LogoutPath = "/Account/Logout";
    options.AccessDeniedPath = "/Account/AccessDenied";
    options.ExpireTimeSpan = TimeSpan.FromDays(30);
    options.SlidingExpiration = true;
});

builder.Services.AddAuthorization();

//Session & Http Context
builder.Services.AddHttpContextAccessor();
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

var app = builder.Build();

// Auto-create admin if not exists
using (var scope = app.Services.CreateScope())
{
    var db = scope.ServiceProvider.GetRequiredService<DB>();
    var hp = scope.ServiceProvider.GetRequiredService<Helper>();

    db.Database.EnsureCreated();

    if (!db.Admins.Any())
    {
        var admin = new Admin
        {
            Id = "A001",
            Email = "admin@transpolink.com",
            Hash = hp.HashPassword("Admin123"),
            Name = "System Administrator",
            PhotoURL = "/images/beauty_admin.png" 
        };

        db.Admins.Add(admin);
        db.SaveChanges();

        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
        Console.WriteLine("✅ Admin Account Created!");
        Console.WriteLine("🆔 ID: A001");
        Console.WriteLine("📧 Email: admin@transpolink.com");
        Console.WriteLine("🔑 Password: Admin123");
        Console.WriteLine("━━━━━━━━━━━━━━━━━━━━━━━━━━━━");
    }
}

//Exception Handling
if (app.Environment.IsDevelopment())
{
    app.UseDeveloperExceptionPage();
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

//Localization
app.UseRequestLocalization("en-MY");

// Session before authentication
app.UseSession();

// Authentication & Authorization
app.UseAuthentication();
app.UseAuthorization();

//Routes
app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

app.Run();