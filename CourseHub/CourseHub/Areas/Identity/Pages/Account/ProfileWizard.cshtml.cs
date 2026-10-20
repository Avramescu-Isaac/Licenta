using CourseHub.Data;
using CourseHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;
using System.ComponentModel.DataAnnotations;

namespace CourseHub.Areas.Identity.Pages.Account
{
    public class ProfileWizardModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IStringLocalizer<ProfileWizardModel> _localizer;

        public ProfileWizardModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context,
            IStringLocalizer<ProfileWizardModel> localizer)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _localizer = localizer;
        }

        [BindProperty]
        public WizardModel Wizard { get; set; } = new WizardModel();

        [BindProperty]
        public int CurrentStep { get; set; } = 1;

        public List<Topic> AvailableTopics { get; set; } = new List<Topic>();

        public class WizardModel
        {
            [Required(ErrorMessage = "Please select a role")]
            public string Role { get; set; } = "";

            public string? EducationLevel { get; set; }
            public string? TeachingLevel { get; set; }

            public int? Age { get; set; }
            public string? Gender { get; set; }

            public List<int> SelectedTopics { get; set; } = new List<int>();
        }

        public async Task OnGetAsync()
        {
            await LoadUserData();
            AvailableTopics = await _context.Topics.ToListAsync();
        }

        public async Task<IActionResult> OnPostAsync(string wizardAction)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            CurrentStep = int.TryParse(Request.Form["CurrentStep"], out var step) ? step : 1;
            AvailableTopics = await _context.Topics.ToListAsync();

            if (wizardAction == "prev")
            {
                CurrentStep = Math.Max(1, CurrentStep - 1);

                // Step 3's form only posts the selected topics - it carries neither the role
                // nor the step-2 details. Repopulating from what was already saved on the way
                // forward keeps the previous step filled in (instead of showing a blank form
                // with neither level field, because Wizard.Role bound to an empty string).
                var postedTopics = Wizard.SelectedTopics;
                await LoadUserData();
                if (postedTopics is { Count: > 0 })
                {
                    Wizard.SelectedTopics = postedTopics;
                }

                // asp-for renders the posted ModelState value in preference to the model, so
                // without this the fields would come back empty despite the reload above.
                ModelState.Clear();
                return Page();
            }

            // Defensive fallback for any step that didn't carry the role back in its form:
            // the account's saved role is the authoritative value, and the branches below
            // depend on it to decide which level field to validate and save.
            if (CurrentStep > 1 && string.IsNullOrEmpty(Wizard.Role))
            {
                Wizard.Role = user.Role ?? "";
            }

            if (CurrentStep == 1)
            {
                if (string.IsNullOrEmpty(Wizard.Role))
                {
                    ModelState.AddModelError("Wizard.Role", _localizer["Te rugăm să selectezi un rol"]);
                    return Page();
                }

                user.Role = Wizard.Role;
                await _userManager.UpdateAsync(user);

                // Keep the real Identity role table in sync with the domain Role field.
                // (App logic still reads ApplicationUser.Role everywhere - this just also
                // makes the role table itself an accurate, admin-panel-visible source of truth.)
                var otherRole = Wizard.Role == "Teach" ? "Learn" : "Teach";
                if (await _userManager.IsInRoleAsync(user, otherRole))
                {
                    await _userManager.RemoveFromRoleAsync(user, otherRole);
                }
                if (!await _userManager.IsInRoleAsync(user, Wizard.Role))
                {
                    await _userManager.AddToRoleAsync(user, Wizard.Role);
                }

                // The sign-in cookie was issued before the role existed, so it still carries
                // the old claims. Refreshing it here is what lets the rest of the app - the
                // navbar and the profile-completion check - see the role without a re-login.
                await _signInManager.RefreshSignInAsync(user);

                CurrentStep = 2;
            }
            else if (CurrentStep == 2)
            {
                if (wizardAction == "next2")
                {
                    ModelState.Remove("Wizard.EducationLevel");
                    ModelState.Remove("Wizard.TeachingLevel");
                    ModelState.Remove("Wizard.Age");
                    ModelState.Remove("Wizard.Gender");

                    bool hasErrors = false;

                    if (Wizard.Role == "Learn" && string.IsNullOrEmpty(Wizard.EducationLevel))
                    {
                        ModelState.AddModelError("Wizard.EducationLevel", _localizer["Nivelul de studii este obligatoriu"]);
                        hasErrors = true;
                    }

                    if (Wizard.Role == "Teach" && string.IsNullOrEmpty(Wizard.TeachingLevel))
                    {
                        ModelState.AddModelError("Wizard.TeachingLevel", _localizer["Nivelul de experiență este obligatoriu"]);
                        hasErrors = true;
                    }

                    if (!Wizard.Age.HasValue)
                    {
                        ModelState.AddModelError("Wizard.Age", _localizer["Vârsta este obligatorie"]);
                        hasErrors = true;
                    }
                    else if (Wizard.Age < 1 || Wizard.Age > 120)
                    {
                        ModelState.AddModelError("Wizard.Age", _localizer["Te rugăm să introduci o vârstă validă (1-120)"]);
                        hasErrors = true;
                    }

                    if (string.IsNullOrEmpty(Wizard.Gender))
                    {
                        ModelState.AddModelError("Wizard.Gender", _localizer["Genul este obligatoriu"]);
                        hasErrors = true;
                    }

                    if (hasErrors) return Page();

                    user.Age = Wizard.Age;
                    user.Gender = Wizard.Gender;

                    // Explicit on both sides: an unrecognised role must not fall through to
                    // "write the teaching level", which is what made the app store the wrong
                    // field back when the role wasn't carried between steps.
                    if (Wizard.Role == "Learn")
                        user.EducationLevel = Wizard.EducationLevel;
                    else if (Wizard.Role == "Teach")
                        user.TeachingLevel = Wizard.TeachingLevel;

                    await _userManager.UpdateAsync(user);
                    CurrentStep = 3;
                }
            }
            else if (CurrentStep == 3)
            {
                if (Wizard.SelectedTopics == null || Wizard.SelectedTopics.Count == 0)
                {
                    ModelState.AddModelError("Wizard.SelectedTopics", _localizer["Te rugăm să selectezi cel puțin o materie"]);
                    return Page();
                }

                if (Wizard.SelectedTopics.Count > 3)
                {
                    ModelState.AddModelError("Wizard.SelectedTopics", _localizer["Poți selecta maximum 3 materii"]);
                    return Page();
                }

                user.SelectedTopics = string.Join(",", Wizard.SelectedTopics);
                await _userManager.UpdateAsync(user);

                return LocalRedirect("~/");
            }
            return Page();
        }

        private async Task LoadUserData()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user != null)
            {
                Wizard.Role = user.Role ?? "";
                Wizard.Age = user.Age;
                Wizard.Gender = user.Gender;
                Wizard.EducationLevel = user.EducationLevel;
                Wizard.TeachingLevel = user.TeachingLevel;

                if (!string.IsNullOrEmpty(user.SelectedTopics))
                {
                    Wizard.SelectedTopics = user.SelectedTopics
                        .Split(',', StringSplitOptions.RemoveEmptyEntries)
                        .Select(int.Parse)
                        .ToList();
                }
            }
        }
    }
}
