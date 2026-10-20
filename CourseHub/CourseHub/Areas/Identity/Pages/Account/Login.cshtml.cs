// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using CourseHub.Data;
using CourseHub.Models;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;

namespace CourseHub.Areas.Identity.Pages.Account
{
    public class LoginModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ILogger<LoginModel> _logger;
        private readonly IStringLocalizer<LoginModel> _localizer;

        public LoginModel(SignInManager<ApplicationUser> signInManager, UserManager<ApplicationUser> userManager, ILogger<LoginModel> logger, IStringLocalizer<LoginModel> localizer)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _logger = logger;
            _localizer = localizer;
        }

        [BindProperty]
        public InputModel Input { get; set; } = new InputModel();

        public IList<AuthenticationScheme> ExternalLogins { get; set; }
        public string ReturnUrl { get; set; }

        [TempData]
        public string ErrorMessage { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Email sau Username")]
            public string EmailOrUsername { get; set; } = string.Empty;

            [Required]
            [DataType(DataType.Password)]
            public string Password { get; set; } = string.Empty;

            [Display(Name = "Remember me?")]
            public bool RememberMe { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            if (!string.IsNullOrEmpty(ErrorMessage))
            {
                ModelState.AddModelError(string.Empty, ErrorMessage);
            }

            returnUrl ??= Url.Content("~/");
            await HttpContext.SignOutAsync(IdentityConstants.ExternalScheme);

            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
            ReturnUrl = returnUrl;
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            if (ModelState.IsValid)
            {
                ApplicationUser user = null;

                if (Input.EmailOrUsername.Contains('@'))
                {
                    var matches = _userManager.Users
                        .Where(u => u.NormalizedEmail == Input.EmailOrUsername.ToUpper())
                        .ToList();

                    if (matches.Count == 1)
                        user = matches[0];
                    else if (matches.Count > 1)
                    {
                        ModelState.AddModelError(string.Empty, _localizer["Acest email este folosit de mai multe conturi."]);
                        return Page();
                    }
                }
                else
                {
                    user = await _userManager.FindByNameAsync(Input.EmailOrUsername);
                }



                if (user == null)
                {
                    ModelState.AddModelError(string.Empty, _localizer["Autentificare eșuată. Verifică datele introduse."]);
                    return Page();
                }

                // Check the password without actually signing in yet, so a banned user's
                // wrong password still just gets the normal "invalid credentials" message -
                // the ban message only appears once we know the credentials were correct.
                var passwordCheck = await _signInManager.CheckPasswordSignInAsync(user, Input.Password, lockoutOnFailure: false);

                if (passwordCheck.Succeeded)
                {
                    if (user.BannedUntil.HasValue && user.BannedUntil.Value > DateTime.UtcNow)
                    {
                        var banMessage = user.BannedUntil.Value == DateTime.MaxValue
                            ? _localizer["Ai fost blocat permanent."].Value
                            : _localizer["Ai fost blocat până la: {0}.", user.BannedUntil.Value.ToLocalTime().ToString("dd MMM yyyy, HH:mm")].Value;

                        ModelState.AddModelError(string.Empty, banMessage);
                        return Page();
                    }

                    await _signInManager.SignInAsync(user, isPersistent: Input.RememberMe);
                    _logger.LogInformation("User logged in.");
                    return LocalRedirect(returnUrl);
                }
                if (passwordCheck.RequiresTwoFactor)
                {
                    return RedirectToPage("./LoginWith2fa", new { ReturnUrl = returnUrl, RememberMe = Input.RememberMe });
                }
                if (passwordCheck.IsLockedOut)
                {
                    _logger.LogWarning("User account locked out.");
                    return RedirectToPage("./Lockout");
                }
                else
                {
                    ModelState.AddModelError(string.Empty, _localizer["Autentificare eșuată. Verifică datele introduse."]);
                    return Page();
                }
            }

            return Page();
        }
    }
}
