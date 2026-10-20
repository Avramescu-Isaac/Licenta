

using System.Diagnostics;
using System.Globalization;
using CourseHub.Data;
using CourseHub.Models;
using CourseHub.Services;
using Microsoft.AspNetCore.Hosting.Server;
using Microsoft.AspNetCore.Hosting.Server.Features;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Localization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AddHttpsRedirection(options =>
{
    options.RedirectStatusCode = StatusCodes.Status307TemporaryRedirect;
    options.HttpsPort = 443;
});

builder.Services.ConfigureApplicationCookie(options =>
{
    options.Cookie.SameSite = SameSiteMode.Lax;
    options.Cookie.SecurePolicy = CookieSecurePolicy.Always;
});

var connectionString = builder.Configuration.GetConnectionString("DefaultConnection")
    ?? throw new InvalidOperationException("Connection string 'DefaultConnection' not found.");
builder.Services.AddDbContext<ApplicationDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddDatabaseDeveloperPageExceptionFilter();

builder.Services.AddDefaultIdentity<ApplicationUser>(options =>
{
    options.SignIn.RequireConfirmedAccount = true;
    options.User.RequireUniqueEmail = true;
})
.AddRoles<IdentityRole>()
.AddEntityFrameworkStores<ApplicationDbContext>();

builder.Services.AddSignalR();

builder.Services.Configure<EmailSettings>(builder.Configuration.GetSection("EmailSettings"));

builder.Services.AddTransient<IEmailSender, EmailSender>();

// ====== reCAPTCHA (Register page "I'm not a robot" check) ======
builder.Services.AddHttpClient();
builder.Services.Configure<RecaptchaSettings>(builder.Configuration.GetSection("Recaptcha"));
builder.Services.AddScoped<IRecaptchaVerifier, RecaptchaVerifier>();

// ====== LOCALIZATION (Romanian / English) ======
// Resource files live under /Resources, mirroring the path of each
// Razor view / page (e.g. Views/Home/Index.cshtml -> Resources/Views/Home/Index.en.resx).
builder.Services.AddLocalization(options => options.ResourcesPath = "Resources");

// ====== CSRF ======
// Razor Pages validate the antiforgery token automatically, but MVC controllers do NOT
// unless asked to - so every POST action (ban, delete, promote, hire...) is validated
// globally here. All forms in the app are built with the form tag helper, which already
// emits the hidden token, so nothing in the views has to change. The two fetch() calls in
// the chat page send the token in a header instead, hence HeaderName below.
builder.Services.AddAntiforgery(options => options.HeaderName = "RequestVerificationToken");

builder.Services.AddControllersWithViews(options =>
    {
        options.Filters.Add(new Microsoft.AspNetCore.Mvc.AutoValidateAntiforgeryTokenAttribute());
    })
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

builder.Services.AddRazorPages()
    .AddViewLocalization()
    .AddDataAnnotationsLocalization();

builder.Services.Configure<RequestLocalizationOptions>(options =>
{
    var supportedCultures = new[]
    {
        new CultureInfo("ro"),
        new CultureInfo("en")
    };

    // Romanian is the default/native language of the app.
    options.DefaultRequestCulture = new RequestCulture(culture: "ro", uiCulture: "ro");
    options.SupportedCultures = supportedCultures;
    options.SupportedUICultures = supportedCultures;

    // Only read the culture from a cookie set by the language switcher,
    // so the site always defaults to Romanian unless the user picked English.
    options.RequestCultureProviders = new List<IRequestCultureProvider>
    {
        new CookieRequestCultureProvider()
    };
});

builder.Services.AddHostedService<CourseHub.Services.OtpCleanupService>();

builder.Services.Configure<IISServerOptions>(options =>
{
    options.MaxRequestBodySize = 100_000_000;
});

var app = builder.Build();

// ====== IDENTITY ROLE SEEDING / BACKFILL ======
// Runs once on every startup. Roles used across the app:
//   - "User"  : assigned to EVERY account automatically at registration. Not used for
//               authorization anywhere - purely so the admin panel can see, at a glance,
//               that an account is a normal registered member (mirrors how a lot of RBAC
//               systems always keep a base role for every principal).
//   - "Teach" / "Learn" : mirrors the existing ApplicationUser.Role text field into the
//               real Identity role table too, kept in sync whenever Role is set. The app's
//               own logic still reads the ApplicationUser.Role field (nothing about existing
//               behavior changes) - this just also makes [Authorize(Roles = "Teach")]-style
//               checks possible in the future, and lets the admin panel display roles from
//               a single, authoritative source.
//   - "Admin" : full admin panel access, seeded via AdminSeeding:Emails below.
//
// This block is also a self-healing BACKFILL: any existing account missing "User", or
// missing a "Teach"/"Learn" role that doesn't match its current ApplicationUser.Role, gets
// fixed up automatically. Safe to run every startup - all operations check first.
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    var roleManager = services.GetRequiredService<RoleManager<IdentityRole>>();
    var userManager = services.GetRequiredService<UserManager<ApplicationUser>>();
    var logger = services.GetRequiredService<ILogger<Program>>();

    const string AdminRole = "Admin";
    const string BaseUserRole = "User";
    string[] allRoles = { AdminRole, BaseUserRole, "Teach", "Learn" };

    foreach (var roleName in allRoles)
    {
        if (!await roleManager.RoleExistsAsync(roleName))
        {
            await roleManager.CreateAsync(new IdentityRole(roleName));
            logger.LogInformation("Created the '{Role}' role.", roleName);
        }
    }

    // Backfill: every existing account gets the base "User" role, and its Teach/Learn
    // Identity role is kept in sync with its ApplicationUser.Role field.
    var allUsers = userManager.Users.ToList();
    foreach (var user in allUsers)
    {
        if (!await userManager.IsInRoleAsync(user, BaseUserRole))
        {
            await userManager.AddToRoleAsync(user, BaseUserRole);
        }

        if (user.Role is "Teach" or "Learn" && !await userManager.IsInRoleAsync(user, user.Role))
        {
            // Remove the other one first, in case it was set previously and Role changed.
            var otherRole = user.Role == "Teach" ? "Learn" : "Teach";
            if (await userManager.IsInRoleAsync(user, otherRole))
            {
                await userManager.RemoveFromRoleAsync(user, otherRole);
            }

            await userManager.AddToRoleAsync(user, user.Role);
        }
    }

    var adminEmails = app.Configuration.GetSection("AdminSeeding:Emails").Get<string[]>() ?? Array.Empty<string>();

    foreach (var email in adminEmails)
    {
        if (string.IsNullOrWhiteSpace(email)) continue;

        var user = await userManager.FindByEmailAsync(email);
        if (user == null)
        {
            logger.LogWarning("AdminSeeding: no registered account found for '{Email}' yet - register it normally first, then restart.", email);
            continue;
        }

        if (!await userManager.IsInRoleAsync(user, AdminRole))
        {
            await userManager.AddToRoleAsync(user, AdminRole);
            logger.LogInformation("AdminSeeding: promoted '{Email}' to Admin.", email);
        }
    }

    // ====== TEST DATA (dev only) ======
    // Creates ~25 ready-to-log-in accounts (already email-confirmed, roles/profile filled
    // in) plus a few sample connections/messages, so you don't have to register + OTP-verify
    // 25 times by hand. Only ever runs in Development, and only creates accounts that don't
    // already exist by email - safe to leave in place.
    if (app.Environment.IsDevelopment())
    {
        await TestDataSeeder.SeedAsync(services, logger);
    }
}

if (app.Environment.IsDevelopment())
{
    app.UseMigrationsEndPoint();

    // launchSettings.json has "launchBrowser": false for this profile - Visual Studio
    // launching (and attaching to) the browser itself was what caused the process to be
    // killed mid-request on certain pages (e.g. avatar upload). Opening the browser here,
    // as a plain OS process via ShellExecute, gives the same "opens automatically" UX
    // without creating that VS<->browser link.
    var lifetime = app.Services.GetRequiredService<IHostApplicationLifetime>();
    lifetime.ApplicationStarted.Register(() =>
    {
        try
        {
            var addressFeature = app.Services.GetRequiredService<IServer>()
                .Features.Get<IServerAddressesFeature>();

            var url = addressFeature?.Addresses.FirstOrDefault(a => a.StartsWith("https://"))
                      ?? addressFeature?.Addresses.FirstOrDefault();

            if (!string.IsNullOrEmpty(url))
            {
                Process.Start(new ProcessStartInfo(url) { UseShellExecute = true });
            }
        }
        catch
        {
            // Best effort only - if this fails for any reason, just open the browser manually.
        }
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

var localizationOptions = app.Services.GetRequiredService<IOptions<RequestLocalizationOptions>>().Value;
app.UseRequestLocalization(localizationOptions);

app.UseRouting();
app.MapHub<CourseHub.Hubs.ChatHub>("/chathub");

app.UseAuthentication();
app.UseAuthorization();

// Keeps an account whose profile wizard is unfinished from reaching the rest of the app.
// Placed after authorization so it can read the signed-in user, and after UseStaticFiles
// so css/js are never affected.
app.UseMiddleware<CourseHub.Services.ProfileCompletionMiddleware>();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");
app.MapRazorPages();

app.Run();
