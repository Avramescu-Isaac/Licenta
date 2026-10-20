
using CourseHub.Data;
using CourseHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;

namespace CourseHub.Areas.Identity.Pages.Account
{
    public class VerifyOtpModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<VerifyOtpModel> _logger;
        private readonly IStringLocalizer<VerifyOtpModel> _localizer;

        public VerifyOtpModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context,
            ILogger<VerifyOtpModel> logger,
            IStringLocalizer<VerifyOtpModel> localizer)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _logger = logger;
            _localizer = localizer;
        }

        [BindProperty]
        public string UserId { get; set; } = string.Empty;

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public class InputModel
        {
            [Required]
            [StringLength(6, MinimumLength = 6, ErrorMessage = "Codul trebuie să aibă 6 cifre.")]
            [Display(Name = "Cod OTP")]
            public string Code { get; set; } = string.Empty;
        }

        public void OnGet(string userId)
        {
            UserId = userId;
        }

        public async Task<IActionResult> OnPostAsync()
        {
            if (!ModelState.IsValid)
                return Page();

            var otpEntry = _context.OtpCodes
                .Where(o => o.UserId == UserId && o.Code == Input.Code)
                .OrderByDescending(o => o.ExpirationTime)
                .FirstOrDefault();

            if (otpEntry == null || otpEntry.ExpirationTime < DateTime.UtcNow)
            {
                ModelState.AddModelError(string.Empty, _localizer["Cod invalid sau expirat."]);
                return Page();
            }

            var user = await _userManager.FindByIdAsync(UserId);
            if (user == null)
            {
                ModelState.AddModelError(string.Empty, _localizer["Utilizator inexistent."]);
                return Page();
            }

            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);

            _context.OtpCodes.Remove(otpEntry);
            await _context.SaveChangesAsync();

            await _signInManager.SignInAsync(user, isPersistent: false);
            _logger.LogInformation("Utilizatorul și-a confirmat emailul prin OTP și a fost logat.");

            return RedirectToPage("/Account/ProfileWizard");
        }
    }
}
