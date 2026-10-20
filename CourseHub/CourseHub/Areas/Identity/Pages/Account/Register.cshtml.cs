
// Licensed to the .NET Foundation under one or more agreements.
// The .NET Foundation licenses this file to you under the MIT license.
#nullable disable

using CourseHub.Data;
using CourseHub.Models;
using CourseHub.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Encodings.Web;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace CourseHub.Areas.Identity.Pages.Account
{
    public class RegisterModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IUserStore<ApplicationUser> _userStore;
        private readonly IUserEmailStore<ApplicationUser> _emailStore;
        private readonly ILogger<RegisterModel> _logger;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<RegisterModel> _localizer;
        private readonly IRecaptchaVerifier _recaptchaVerifier;

        public RegisterModel(
            UserManager<ApplicationUser> userManager,
            IUserStore<ApplicationUser> userStore,
            SignInManager<ApplicationUser> signInManager,
            ILogger<RegisterModel> logger,
            IEmailSender emailSender,
            ApplicationDbContext context,
            IStringLocalizer<RegisterModel> localizer,
            IRecaptchaVerifier recaptchaVerifier)
        {
            _userManager = userManager;
            _userStore = userStore;
            _emailStore = GetEmailStore();
            _signInManager = signInManager;
            _logger = logger;
            _emailSender = emailSender;
            _context = context;
            _localizer = localizer;
            _recaptchaVerifier = recaptchaVerifier;
        }

        [BindProperty]
        public InputModel Input { get; set; }

        public string ReturnUrl { get; set; }
        public IList<AuthenticationScheme> ExternalLogins { get; set; }

        public class InputModel
        {
            [Required]
            [Display(Name = "Username")]
            public string Username { get; set; }

            [Required]
            [EmailAddress]
            [Display(Name = "Email")]
            public string Email { get; set; }

            [Required]
            [StringLength(100, ErrorMessage = "The {0} must be at least {2} and at max {1} characters long.", MinimumLength = 6)]
            [DataType(DataType.Password)]
            [Display(Name = "Password")]
            public string Password { get; set; }

            [DataType(DataType.Password)]
            [Display(Name = "Confirm password")]
            [Compare("Password", ErrorMessage = "The password and confirmation password do not match.")]
            public string ConfirmPassword { get; set; }
        }

        public async Task OnGetAsync(string returnUrl = null)
        {
            ReturnUrl = returnUrl;
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();
        }

        public async Task<IActionResult> OnPostAsync(string returnUrl = null)
        {
            returnUrl ??= Url.Content("~/");
            ExternalLogins = (await _signInManager.GetExternalAuthenticationSchemesAsync()).ToList();

            // The reCAPTCHA widget's JS adds this field to the form automatically on submit.
            var recaptchaToken = Request.Form["g-recaptcha-response"].ToString();
            var recaptchaOk = await _recaptchaVerifier.VerifyAsync(recaptchaToken, HttpContext.Connection.RemoteIpAddress?.ToString());

            if (!recaptchaOk)
            {
                ModelState.AddModelError(string.Empty, _localizer["Te rugăm să confirmi că nu ești un robot."]);
                return Page();
            }

            if (ModelState.IsValid)
            {
                var user = new ApplicationUser
                {
                    UserName = Input.Email,
                    Email = Input.Email,
                    Bio = ""
                };

                await _userStore.SetUserNameAsync(user, Input.Username, CancellationToken.None);
                await _emailStore.SetEmailAsync(user, Input.Email, CancellationToken.None);
                var result = await _userManager.CreateAsync(user, Input.Password);

                if (result.Succeeded)
                {
                    _logger.LogInformation("User created a new account with password.");

                    // Every account gets the base "User" Identity role - not used for any
                    // authorization check, just so the admin panel can see it's a normal
                    // registered member (mirrors the real "AspNetRoles" table, alongside
                    // Teach/Learn which gets added once they pick one in the profile wizard).
                    await _userManager.AddToRoleAsync(user, "User");

                    if (_userManager.Options.SignIn.RequireConfirmedAccount)
                    {
                        var newUserId = await _userManager.GetUserIdAsync(user);
                        var otpCode = new Random().Next(100000, 999999).ToString();
                        var expirationTime = DateTime.UtcNow.AddMinutes(10);

                        var otpEntry = new OtpCode
                        {
                            UserId = newUserId,
                            Code = otpCode,
                            ExpirationTime = expirationTime
                        };
                        _context.OtpCodes.Add(otpEntry);
                        await _context.SaveChangesAsync();

                        try
                        {
                            await _emailSender.SendEmailAsync(
                                Input.Email,
                                _localizer["Codul tău de verificare"],
                                _localizer["Codul tău de verificare este: <b>{0}</b>. Este valabil 10 minute.", otpCode]
                            );
                        }
                        catch (Exception ex)
                        {
                            _logger.LogError(ex, "Eroare la trimiterea OTP către {Email}", Input.Email);
                            ModelState.AddModelError(string.Empty, _localizer["Nu am putut trimite emailul. Verifică SMTP-ul."]);
                            return Page();
                        }

                        return RedirectToPage("/Account/VerifyOtp", new { userId = newUserId });
                    }
                    else
                    {
                        await _signInManager.SignInAsync(user, isPersistent: false);
                        return LocalRedirect(returnUrl);
                    }
                }

                foreach (var error in result.Errors)
                {
                    ModelState.AddModelError(string.Empty, error.Description);
                }
            }

            

            return Page();
        }

        private ApplicationUser CreateUser()
        {
            try
            {
                return Activator.CreateInstance<ApplicationUser>();
            }
            catch
            {
                throw new InvalidOperationException($"Can't create an instance of '{nameof(ApplicationUser)}'.");
            }
        }

        private IUserEmailStore<ApplicationUser> GetEmailStore()
        {
            if (!_userManager.SupportsUserEmail)
            {
                throw new NotSupportedException("The default UI requires a user store with email support.");
            }
            return (IUserEmailStore<ApplicationUser>)_userStore;
        }
    }

    public class RecaptchaResponse
    {
        public bool Success { get; set; }
        public List<string> ErrorCodes { get; set; }
    }
}
