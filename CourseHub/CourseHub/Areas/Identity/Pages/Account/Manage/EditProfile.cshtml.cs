using System;
using System.Linq;
using System.Threading.Tasks;
using CourseHub.Data;
using CourseHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace CourseHub.Areas.Identity.Pages.Account.Manage
{
    public class EditProfileModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IEmailSender _emailSender;
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<EditProfileModel> _localizer;

        public EditProfileModel(
            UserManager<ApplicationUser> userManager,
            IEmailSender emailSender,
            ApplicationDbContext context,
            IStringLocalizer<EditProfileModel> localizer)
        {
            _userManager = userManager;
            _emailSender = emailSender;
            _context = context;
            _localizer = localizer;
        }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        [Required]
        public string Username { get; set; }

        [BindProperty]
        [Required, EmailAddress]
        public string Email { get; set; }

        [BindProperty]
        [StringLength(6, MinimumLength = 6, ErrorMessage = "Codul trebuie sa aiba 6 cifre.")]
        public string OtpCode { get; set; }

        [BindProperty]
        public bool ShowOtpInput { get; set; }

        public List<Topic> AvailableTopics { get; set; } = new List<Topic>();
        public bool IsLearnUser { get; set; }
        public bool IsTeachUser { get; set; }

        // Both roles choose their subjects in the profile wizard, so both can change them
        // here. The view renders the subject picker under exactly this condition - if it
        // ever rendered for a role that can't save them, an empty post would wipe them.
        public bool CanEditTopics => IsLearnUser || IsTeachUser;

        [BindProperty]
        public List<int> SelectedTopicIds { get; set; } = new List<int>();

        // Only one of these two is ever shown/used, depending on the user's Role.
        [BindProperty]
        public string? EducationLevel { get; set; }

        [BindProperty]
        public string? TeachingLevel { get; set; }

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound(_localizer["Utilizatorul nu a fost găsit."].Value);

            Username = user.UserName;
            Email = user.Email;
            EducationLevel = user.EducationLevel;
            TeachingLevel = user.TeachingLevel;
            ShowOtpInput = false;

            IsLearnUser = user.Role == "Learn";
            IsTeachUser = user.Role == "Teach";

            if (CanEditTopics)
            {
                AvailableTopics = await _context.Topics.ToListAsync();
                if (!string.IsNullOrEmpty(user.SelectedTopics))
                {
                    SelectedTopicIds = user.SelectedTopics
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(int.Parse)
                        .ToList();
                }
            }

            // Clear any old status messages when navigating to this page
            StatusMessage = null;

            return Page();
        }

        public async Task<IActionResult> OnPostSendCodeAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound(_localizer["Utilizatorul nu a fost găsit."].Value);

            IsLearnUser = user.Role == "Learn";
            IsTeachUser = user.Role == "Teach";
            AvailableTopics = await _context.Topics.ToListAsync();

            ModelState.Remove(nameof(OtpCode));

            // Check if anything actually changed
            bool hasChanges = false;

            if (Username != user.UserName)
            {
                if (await _userManager.FindByNameAsync(Username) != null)
                    ModelState.AddModelError(nameof(Username), _localizer["Numele de utilizator este deja folosit."]);
                else
                    hasChanges = true;
            }

            if (Email != user.Email)
            {
                if (await _userManager.FindByEmailAsync(Email) != null)
                    ModelState.AddModelError(nameof(Email), _localizer["Emailul este deja folosit."]);
                else
                    hasChanges = true;
            }

            // Check subject changes - both roles pick subjects, so both can change them.
            if (CanEditTopics)
            {
                var newTopicsString = string.Join(",", SelectedTopicIds.OrderBy(x => x));
                var oldTopicsString = user.SelectedTopics ?? "";

                // Sort both for comparison
                var oldSorted = string.Join(",", (oldTopicsString.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(int.Parse).OrderBy(x => x)));

                if (newTopicsString != oldSorted)
                {
                    hasChanges = true;
                }
            }

            // Check education level changes for Learn users
            if (IsLearnUser)
            {
                if (EducationLevel != user.EducationLevel)
                {
                    hasChanges = true;
                }
            }

            // Check teaching level changes for Teach users
            if (IsTeachUser)
            {
                if (TeachingLevel != user.TeachingLevel)
                {
                    hasChanges = true;
                }
            }

            if (!hasChanges)
            {
                StatusMessage = _localizer["Nicio modificare efectuată."];
                ShowOtpInput = false;
                return RedirectToPage();
            }

            if (!ModelState.IsValid)
            {
                return Page();
            }

            var code = new Random().Next(100000, 999999).ToString();
            _context.OtpCodes.Add(new OtpCode
            {
                UserId = user.Id,
                Code = code,
                ExpirationTime = DateTime.UtcNow.AddMinutes(10)
            });
            await _context.SaveChangesAsync();

            await _emailSender.SendEmailAsync(Email, _localizer["Cod de confirmare CourseHub"],
                _localizer["Codul tău OTP este: <strong>{0}</strong>", code]);

            ShowOtpInput = true;
            StatusMessage = _localizer["Cod de verificare trimis. Te rugăm să-l introduci mai jos."];
            return Page();
        }

        public async Task<IActionResult> OnPostSaveChangesAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound(_localizer["Utilizatorul nu a fost găsit."].Value);

            IsLearnUser = user.Role == "Learn";
            IsTeachUser = user.Role == "Teach";
            AvailableTopics = await _context.Topics.ToListAsync();

            var otpEntry = _context.OtpCodes
                .Where(o => o.UserId == user.Id && o.Code == OtpCode)
                .OrderByDescending(o => o.ExpirationTime)
                .FirstOrDefault();

            if (otpEntry == null || otpEntry.ExpirationTime < DateTime.UtcNow)
            {
                ModelState.AddModelError(nameof(OtpCode), _localizer["Cod invalid sau expirat."]);
                ShowOtpInput = true;
                return Page();
            }

            // Update sensitive information
            if (Username != user.UserName)
                await _userManager.SetUserNameAsync(user, Username);

            if (Email != user.Email)
                await _userManager.SetEmailAsync(user, Email);

            // Update subjects - same three-subject cap as the profile wizard, for both roles.
            if (CanEditTopics)
            {
                if (SelectedTopicIds.Count > 3)
                {
                    ModelState.AddModelError("", _localizer["Poți selecta maximum 3 materii"]);
                    ShowOtpInput = true;
                    return Page();
                }

                user.SelectedTopics = string.Join(",", SelectedTopicIds);
            }

            // Update education level for Learn users
            if (IsLearnUser)
            {
                user.EducationLevel = EducationLevel;
            }

            // Update teaching level for Teach users
            if (IsTeachUser)
            {
                user.TeachingLevel = TeachingLevel;
            }

            user.EmailConfirmed = true;
            await _userManager.UpdateAsync(user);

            _context.OtpCodes.Remove(otpEntry);
            await _context.SaveChangesAsync();

            StatusMessage = _localizer["Profil actualizat cu succes!"];
            ShowOtpInput = false;
            return RedirectToPage();
        }
    }
}
