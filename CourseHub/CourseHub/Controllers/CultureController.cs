using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;

namespace CourseHub.Controllers
{
    // Handles switching the site language (Romanian / English) from the
    // language switcher in the navbar. Stores the choice in a cookie so it
    // persists across requests, then redirects back to the page the user was on.
    public class CultureController : Controller
    {
        [HttpGet]
        public IActionResult SetLanguage(string culture, string? returnUrl)
        {
            if (culture != "ro" && culture != "en")
            {
                culture = "ro";
            }

            Response.Cookies.Append(
                CookieRequestCultureProvider.DefaultCookieName,
                CookieRequestCultureProvider.MakeCookieValue(new RequestCulture(culture)),
                new CookieOptions
                {
                    Expires = DateTimeOffset.UtcNow.AddYears(1),
                    IsEssential = true,
                    SameSite = SameSiteMode.Lax,
                    Secure = true
                }
            );

            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            {
                return LocalRedirect(returnUrl);
            }

            return RedirectToAction("Index", "Home");
        }
    }
}
