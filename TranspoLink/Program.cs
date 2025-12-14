global using TranspoLink;
global using TranspoLink.Models;
using Microsoft.AspNetCore.Authentication;
using TranspoLink.Hubs;

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
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
})
    .AddGoogle(options =>
    {
        options.ClientId = builder.Configuration["Authentication:Google:ClientId"];
        options.ClientSecret = builder.Configuration["Authentication:Google:ClientSecret"];
        options.CallbackPath = "/signin-google";
        options.ClaimActions.MapJsonKey("urn:google:picture", "picture", "url");
        options.Events = new Microsoft.AspNetCore.Authentication.OAuth.OAuthEvents
        {
            OnRedirectToAuthorizationEndpoint = context =>
            {
                context.Response.Redirect(context.RedirectUri + "&prompt=select_account");
                return Task.CompletedTask;
            },
            OnRemoteFailure = context =>
            {
                context.Response.Redirect("/Account/GoogleLogin");
                context.HandleResponse();
                return Task.CompletedTask;
            }
        };
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

builder.Services.AddSignalR();

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

app.MapHub<ChatHub>("/chathub");

app.Run();