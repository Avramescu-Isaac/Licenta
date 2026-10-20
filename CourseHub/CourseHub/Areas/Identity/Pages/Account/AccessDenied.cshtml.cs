using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc.RazorPages;

namespace CourseHub.Areas.Identity.Pages.Account
{
    // Replaces the Identity UI default so the "access denied" screen follows the
    // site language like every other page.
    [AllowAnonymous]
    public class AccessDeniedModel : PageModel
    {
        public void OnGet()
        {
        }
    }
}
