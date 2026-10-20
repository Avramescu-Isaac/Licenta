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
using Microsoft.AspNetCore.Localization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace CourseHub.Areas.Identity.Pages.Account
{
    [ValidateAntiForgeryToken]
    public class LogoutModel : PageModel
    {
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly ILogger<LogoutModel> _logger;

        public LogoutModel(
        SignInManager<ApplicationUser> signInManager,
        UserManager<ApplicationUser> userManager,
        ApplicationDbContext context,
        ILogger<LogoutModel> logger)
        {
            _signInManager = signInManager;
            _userManager = userManager;
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> OnPost(string returnUrl = null)
        {
            _logger.LogInformation("Logout: entered OnPost");

            // Read the account before signing out, while the identity is still available.
            // An account that never finished the profile wizard has no role, no level and no
            // subjects; leaving it behind would put an empty profile in everyone's search
            // results, so it is removed instead of being kept as a half-registered account.
            var user = await _userManager.GetUserAsync(User);
            var abandonedSignup = user != null && string.IsNullOrEmpty(user.Role);

            await HttpContext.SignOutAsync(IdentityConstants.ApplicationScheme);

            _logger.LogInformation("Logout: SignOutAsync() called");

            // Clear all cookies except the language preference, so switching
            // to English/Romanian survives logging out.
            foreach (var cookie in Request.Cookies.Keys)
            {
                if (cookie == CookieRequestCultureProvider.DefaultCookieName)
                    continue;

                Response.Cookies.Delete(cookie, new CookieOptions
                {
                    Secure = true,
                    SameSite = SameSiteMode.Lax
                });
            }

            _logger.LogInformation("Logout: cookies cleared");

            if (abandonedSignup)
            {
                // Only the one-time codes can reference an account at this stage - a profile
                // that was never completed cannot have sent messages or hire requests.
                var otpCodes = await _context.OtpCodes.Where(o => o.UserId == user.Id).ToListAsync();
                if (otpCodes.Count > 0)
                {
                    _context.OtpCodes.RemoveRange(otpCodes);
                    await _context.SaveChangesAsync();
                }

                var result = await _userManager.DeleteAsync(user);
                if (result.Succeeded)
                {
                    _logger.LogInformation("Logout: removed account {UserId} - profile wizard was never finished.", user.Id);
                }
                else
                {
                    _logger.LogWarning("Logout: could not remove unfinished account {UserId}.", user.Id);
                }
            }

            return RedirectToPage("/Account/Login", new { area = "Identity" });
        }

    }
}
