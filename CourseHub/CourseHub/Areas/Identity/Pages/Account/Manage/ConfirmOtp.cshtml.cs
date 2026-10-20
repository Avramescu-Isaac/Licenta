using CourseHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;

namespace CourseHub.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ConfirmOtpModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<ConfirmOtpModel> _localizer;

        public ConfirmOtpModel(UserManager<ApplicationUser> userManager, IStringLocalizer<ConfirmOtpModel> localizer)
        {
            _userManager = userManager;
            _localizer = localizer;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public class InputModel
        {
            [Required]
            public string UserId { get; set; }

            [Required]
            [Display(Name = "Confirmation code")]
            public string Code { get; set; }
        }

        public void OnGet(string userId)
        {
            Input = new InputModel { UserId = userId };
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid) return Page();

            var user = await _userManager.FindByIdAsync(Input.UserId);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, _localizer["Utilizator invalid."]);
                return Page();
            }

            var decodedBytes = WebEncoders.Base64UrlDecode(Input.Code);
            var token = Encoding.UTF8.GetString(decodedBytes);

            var result = await _userManager.ConfirmEmailAsync(user, token);
            if (result.Succeeded)
            {
                return RedirectToPage("./Login", new { area = "Identity" });
            }

            ModelState.AddModelError(string.Empty, _localizer["Cod invalid."]);
            return Page();
        }
    }
}
