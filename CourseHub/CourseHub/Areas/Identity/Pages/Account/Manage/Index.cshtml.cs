using CourseHub.Data;
using CourseHub.Models;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Logging;
using System;
using System.ComponentModel.DataAnnotations;
using System.IO;
using System.Threading.Tasks;

namespace CourseHub.Areas.Identity.Pages.Account.Manage
{
    public class IndexModel : PageModel
    {
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly SignInManager<ApplicationUser> _signInManager;
        private readonly ApplicationDbContext _context;
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer<IndexModel> _localizer;
        private readonly ILogger<IndexModel> _logger;

        // Avatar upload constraints
        private static readonly string[] AllowedAvatarExtensions = { ".jpg", ".jpeg", ".png", ".webp", ".gif" };
        private static readonly string[] AllowedAvatarContentTypes = { "image/jpeg", "image/png", "image/webp", "image/gif" };
        private const long MaxAvatarSizeBytes = 3 * 1024 * 1024; // 3 MB

        public IndexModel(
            UserManager<ApplicationUser> userManager,
            SignInManager<ApplicationUser> signInManager,
            ApplicationDbContext context,
            IWebHostEnvironment env,
            IStringLocalizer<IndexModel> localizer,
            ILogger<IndexModel> logger)
        {
            _userManager = userManager;
            _signInManager = signInManager;
            _context = context;
            _env = env;
            _localizer = localizer;
            _logger = logger;
        }

        public string Username { get; set; }

        // Current avatar path (e.g. "/avatars/xxxx.jpg"), or null if none set.
        public string AvatarPath { get; set; }

        [TempData]
        public string StatusMessage { get; set; }

        [BindProperty]
        public InputModel Input { get; set; }

        [BindProperty]
        public IFormFile AvatarFile { get; set; }

        public class InputModel
        {
            [Display(Name = "Mini bio")]
            [StringLength(500)]
            public string Bio { get; set; }
        }

        // Store original values to check for changes
        private string _originalBio;

        public async Task<IActionResult> OnGetAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            Username = user.UserName;
            AvatarPath = user.AvatarPath;
            Input = new InputModel { Bio = user.Bio };
            _originalBio = user.Bio;

            // Clear any old status messages when navigating to this page
            StatusMessage = null;

            return Page();
        }

        public async Task<IActionResult> OnPostAsync()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return NotFound();

            AvatarPath = user.AvatarPath;

            if (!ModelState.IsValid)
                return Page();

            // Check if anything actually changed
            bool hasChanges = false;

            // 1) Check Bio
            if (Input.Bio != user.Bio)
            {
                user.Bio = Input.Bio;
                hasChanges = true;
            }

            if (!hasChanges)
            {
                StatusMessage = _localizer["Nicio modificare efectuată."];
                return RedirectToPage();
            }

            // Save changes
            await _userManager.UpdateAsync(user);
            await _signInManager.RefreshSignInAsync(user);

            StatusMessage = _localizer["Profil actualizat cu succes!"];
            return RedirectToPage();
        }

        public async Task<IActionResult> OnPostUploadAvatarAsync()
        {
            _logger.LogInformation("[Avatar] Handler started.");

            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return NotFound();

                Username = user.UserName;
                AvatarPath = user.AvatarPath;
                Input = new InputModel { Bio = user.Bio };
                ModelState.Clear();

                _logger.LogInformation("[Avatar] Loaded user {UserId}.", user.Id);

                if (AvatarFile == null || AvatarFile.Length == 0)
                {
                    _logger.LogWarning("[Avatar] No file received.");
                    StatusMessage = _localizer["Te rugăm să selectezi o imagine."];
                    return Page();
                }

                _logger.LogInformation("[Avatar] File received: {FileName}, {Length} bytes, {ContentType}.",
                    AvatarFile.FileName, AvatarFile.Length, AvatarFile.ContentType);

                if (AvatarFile.Length > MaxAvatarSizeBytes)
                {
                    StatusMessage = _localizer["Imaginea este prea mare. Dimensiunea maximă este 3 MB."];
                    return Page();
                }

                var extension = Path.GetExtension(AvatarFile.FileName).ToLowerInvariant();
                var contentType = AvatarFile.ContentType?.ToLowerInvariant();

                if (Array.IndexOf(AllowedAvatarExtensions, extension) < 0 ||
                    contentType == null || Array.IndexOf(AllowedAvatarContentTypes, contentType) < 0)
                {
                    _logger.LogWarning("[Avatar] Rejected format. Extension={Extension}, ContentType={ContentType}.", extension, contentType);
                    StatusMessage = _localizer["Format neacceptat. Folosește o imagine JPG, PNG, WEBP sau GIF."];
                    return Page();
                }

                var avatarsFolder = Path.Combine(_env.WebRootPath, "avatars");
                _logger.LogInformation("[Avatar] WebRootPath={WebRootPath}, avatarsFolder={AvatarsFolder}.", _env.WebRootPath, avatarsFolder);
                Directory.CreateDirectory(avatarsFolder);

                // Fully random name, like chat attachments: besides avoiding collisions, it
                // keeps the stored file from naming the account it belongs to. The link back
                // to the user lives in ApplicationUser.AvatarPath, not in the file name.
                var fileName = $"{Guid.NewGuid():N}{extension}";
                var physicalPath = Path.Combine(avatarsFolder, fileName);

                _logger.LogInformation("[Avatar] Writing file to {PhysicalPath}.", physicalPath);
                await using (var stream = new FileStream(physicalPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    await AvatarFile.CopyToAsync(stream);
                }
                _logger.LogInformation("[Avatar] File write complete.");

                // Delete the previous avatar file, if any, now that the new one is saved.
                DeleteAvatarFileIfExists(user.AvatarPath);
                _logger.LogInformation("[Avatar] Old file cleanup complete (if any).");

                // NOTE: Deliberately NOT calling _userManager.UpdateAsync(user) here.
                // UpdateAsync re-runs the full Identity validation pipeline (username AND
                // email uniqueness checks) on every call, which is unnecessary for an
                // avatar-only change and is exactly where this was crashing under the
                // debugger. Since `user` is already tracked by this request's DbContext
                // (UserManager and ApplicationDbContext share the same scoped instance),
                // we can just set the property and save directly - EF Core's change
                // tracker picks up the modification on its own.
                user.AvatarPath = $"/avatars/{fileName}";
                _logger.LogInformation("[Avatar] About to call SaveChangesAsync directly (bypassing UserManager.UpdateAsync).");
                await _context.SaveChangesAsync();
                _logger.LogInformation("[Avatar] SaveChangesAsync complete.");

                await _signInManager.RefreshSignInAsync(user);
                _logger.LogInformation("[Avatar] RefreshSignInAsync complete.");

                StatusMessage = _localizer["Avatar actualizat cu succes!"];
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Avatar] Upload failed with an unhandled exception.");
                StatusMessage = "" + _localizer["A apărut o eroare la încărcarea avatarului. Te rugăm să încerci din nou."];
                return Page();
            }
        }

        public async Task<IActionResult> OnPostRemoveAvatarAsync()
        {
            try
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null) return NotFound();

                DeleteAvatarFileIfExists(user.AvatarPath);

                // Same reasoning as OnPostUploadAvatarAsync: bypass UserManager.UpdateAsync
                // and save the single property directly through the shared DbContext.
                user.AvatarPath = null;
                await _context.SaveChangesAsync();

                await _signInManager.RefreshSignInAsync(user);

                StatusMessage = _localizer["Avatarul a fost eliminat."];
                return RedirectToPage();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "[Avatar] Remove failed with an unhandled exception.");
                StatusMessage = "" + _localizer["A apărut o eroare la eliminarea avatarului. Te rugăm să încerci din nou."];
                return RedirectToPage();
            }
        }

        private void DeleteAvatarFileIfExists(string avatarPath)
        {
            if (string.IsNullOrEmpty(avatarPath) || !avatarPath.StartsWith("/avatars/"))
                return;

            var fileName = Path.GetFileName(avatarPath);
            var physicalPath = Path.Combine(_env.WebRootPath, "avatars", fileName);

            if (System.IO.File.Exists(physicalPath))
            {
                try
                {
                    System.IO.File.Delete(physicalPath);
                }
                catch (IOException ex)
                {
                    // Best-effort cleanup; an orphaned file on disk isn't worth failing the request over.
                    _logger.LogWarning(ex, "[Avatar] Could not delete old avatar file {PhysicalPath}.", physicalPath);
                }
            }
        }
    }
}
