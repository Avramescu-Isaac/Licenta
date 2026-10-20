// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using System;
using System.ComponentModel.DataAnnotations;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Localization;
using CourseHub.Models;

namespace CourseHub.Areas.Identity.Pages.Account
{
    [AllowAnonymous]
    public class ResetPasswordModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly IStringLocalizer<ResetPasswordModel> _localizer;

        public ResetPasswordModel(
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            IStringLocalizer<ResetPasswordModel> localizer)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _localizer = localizer;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        public bool ShowEmailField { get; set; } = true;
        public bool ShowPasswordFields { get; set; } = false;

        public class InputModel
        {
            [Required]
            [EmailAddress]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }

            public string Code { get; set; }
        }

        public IActionResult OnGet(string code = null)
        {
            if (code == null)
            {
                ShowEmailField = true;
                ShowPasswordFields = false;
                Input = new InputModel();
                return Page();
            }
            else
            {
                ShowEmailField = false;
                ShowPasswordFields = true;
                Input = new InputModel
                {
                    Code = Encoding.UTF8.GetString(WebEncoders.Base64UrlDecode(code))
                };
                return Page();
            }
        }

        public async Task<IActionResult> OnPostAsync()
        {
            // Step 1: User entered email - send reset link
            if (string.IsNullOrEmpty(Input.Code))
            {
                if (!ModelState.IsValid)
                {
                    ShowEmailField = true;
                    ShowPasswordFields = false;
                    return Page();
                }

                var user = await _userManager.FindByEmailAsync(Input.Email);
                if (user == null || !(await _userManager.IsEmailConfirmedAsync(user)))
                {
                    // Don't reveal that the user does not exist or is not confirmed
                    StatusMessage = _localizer["Dacă emailul tău este înregistrat și confirmat, vei primi un link de resetare a parolei."];
                    ShowEmailField = true;
                    ShowPasswordFields = false;
                    return Page();
                }

                var code = await _userManager.GeneratePasswordResetTokenAsync(user);
                code = WebEncoders.Base64UrlEncode(Encoding.UTF8.GetBytes(code));

                // Build reset link (this will go to the same page with the code)
                var callbackUrl = Url.Page(
                    "/Account/ResetPassword",
                    pageHandler: null,
                    values: new { area = "Identity", code },
                    protocol: Request.Scheme);

                var resetLinkText = _localizer["Apasă aici pentru a-ți reseta parola"];
                await _emailSender.SendEmailAsync(
                    Input.Email,
                    _localizer["Resetează-ți parola"],
                    $"<a href='{callbackUrl}'>{resetLinkText}</a>.");

                StatusMessage = _localizer["Linkul de resetare a parolei a fost trimis pe emailul tău."];
                ShowEmailField = true;
                ShowPasswordFields = false;
                return Page();
            }

            // Step 2: User entered new password with code
            if (!ModelState.IsValid)
            {
                ShowEmailField = false;
                ShowPasswordFields = true;
                return Page();
            }

            var user2 = await _userManager.FindByEmailAsync(Input.Email);
            if (user2 == null)
            {
                // Don't reveal that the user does not exist
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            var result = await _userManager.ResetPasswordAsync(user2, Input.Code, Input.Password);
            if (result.Succeeded)
            {
                return RedirectToPage("./ResetPasswordConfirmation");
            }

            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }

            ShowEmailField = false;
            ShowPasswordFields = true;
            return Page();
        }
    }
}
