using CourseHub.Models;
using Microsoft.AspNetCore.Identity;

namespace CourseHub.Services
{
    // A confirmed account that never finished the profile wizard has Role == null, and
    // nothing downstream handles that state: every role check in the app is written as
    // Role == "Teach" ? ... : ..., so such an account silently behaves like a learner with
    // no level and no subjects - it shows up in searches, can be hired, and can open pages
    // that assume a completed profile.
    //
    // Hiding the navbar links while the wizard is unfinished is not a fix on its own: the
    // pages stay reachable by typing their address. The rule therefore lives here, checked
    // on every request, the same way the rest of the app keeps authorization on the server
    // rather than in the interface.
    public class ProfileCompletionMiddleware
    {
        private readonly RequestDelegate _next;

        // The only things an account with an unfinished profile may still reach: the wizard
        // itself, signing out, and switching the interface language.
        private static readonly string[] AllowedPaths =
        {
            "/Identity/Account/ProfileWizard",
            "/Identity/Account/Logout",
            "/Culture/SetLanguage"
        };

        private const string WizardPath = "/Identity/Account/ProfileWizard";

        public ProfileCompletionMiddleware(RequestDelegate next)
        {
            _next = next;
        }

        public async Task InvokeAsync(HttpContext context, UserManager<ApplicationUser> userManager)
        {
            if (context.User.Identity?.IsAuthenticated != true)
            {
                await _next(context);
                return;
            }

            // Fast path: finishing step 1 of the wizard writes the chosen role into the real
            // Identity role table, so a completed profile carries one of these claims and
            // costs no database round-trip here.
            if (context.User.IsInRole("Teach") || context.User.IsInRole("Learn"))
            {
                await _next(context);
                return;
            }

            foreach (var allowed in AllowedPaths)
            {
                if (context.Request.Path.StartsWithSegments(allowed, StringComparison.OrdinalIgnoreCase))
                {
                    await _next(context);
                    return;
                }
            }

            // The claim can also be missing just because the sign-in cookie was issued before
            // the role was assigned, so confirm against the database before redirecting -
            // otherwise a user who has actually finished would be sent back into the wizard.
            var user = await userManager.GetUserAsync(context.User);
            if (user != null && !string.IsNullOrEmpty(user.Role))
            {
                await _next(context);
                return;
            }

            context.Response.Redirect(WizardPath);
        }
    }
}
