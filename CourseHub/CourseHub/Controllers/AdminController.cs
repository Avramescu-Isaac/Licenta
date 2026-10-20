using CourseHub.Data;
using CourseHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace CourseHub.Controllers
{
    // Only accounts in the "Admin" Identity role can reach anything in this controller -
    // enforced by the framework itself, not by app-level if-checks.
    [Authorize(Roles = "Admin")]
    public class AdminController : Controller
    {
        private const string AdminRole = "Admin";

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly RoleManager<IdentityRole> _roleManager;
        private readonly IWebHostEnvironment _env;
        private readonly IStringLocalizer<AdminController> _localizer;
        private readonly ILogger<AdminController> _logger;

        public AdminController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            RoleManager<IdentityRole> roleManager,
            IWebHostEnvironment env,
            IStringLocalizer<AdminController> localizer,
            ILogger<AdminController> logger)
        {
            _context = context;
            _userManager = userManager;
            _roleManager = roleManager;
            _env = env;
            _localizer = localizer;
            _logger = logger;
        }

        // ===================== DASHBOARD =====================

        public async Task<IActionResult> Index()
        {
            var stats = new AdminDashboardStats
            {
                TotalUsers = await _context.Users.CountAsync(),
                TeachCount = await _context.Users.CountAsync(u => u.Role == "Teach"),
                LearnCount = await _context.Users.CountAsync(u => u.Role == "Learn"),
                AdminCount = (await _userManager.GetUsersInRoleAsync(AdminRole)).Count,
                TotalMessages = await _context.Messages.CountAsync(),
                PendingHireRequests = await _context.HireRequests.CountAsync(h => !h.IsAccepted),
                AcceptedHireRequests = await _context.HireRequests.CountAsync(h => h.IsAccepted),
                TotalTopics = await _context.Topics.CountAsync(),
                BannedCount = await _context.Users.CountAsync(u => u.BannedUntil != null && u.BannedUntil > DateTime.UtcNow)
            };

            return View(stats);
        }

        // ===================== USERS =====================

        public async Task<IActionResult> Users()
        {
            var users = await _context.Users
                .OrderBy(u => u.UserName)
                .ToListAsync();

            var adminIds = (await _userManager.GetUsersInRoleAsync(AdminRole))
                .Select(u => u.Id)
                .ToHashSet();

            // Full Identity role list per user (e.g. "User", "Teach", "Admin") - shown only
            // here in the admin panel, purely for auditing/visibility into the role table.
            var rolesByUserId = new Dictionary<string, IList<string>>();
            foreach (var user in users)
            {
                rolesByUserId[user.Id] = await _userManager.GetRolesAsync(user);
            }

            ViewBag.AdminIds = adminIds;
            ViewBag.RolesByUserId = rolesByUserId;
            ViewBag.CurrentUserId = _userManager.GetUserId(User);

            return View(users);
        }

        [HttpPost]
        public async Task<IActionResult> ToggleAdmin(string id)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (id == currentUserId)
            {
                TempData["AdminError"] = _localizer["Nu îți poți schimba propriul rol de administrator."].Value;
                return RedirectToAction("Users");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            if (await _userManager.IsInRoleAsync(user, AdminRole))
            {
                await _userManager.RemoveFromRoleAsync(user, AdminRole);
                TempData["AdminMessage"] = _localizer["{0} nu mai este administrator.", user.UserName].Value;
            }
            else
            {
                await _userManager.AddToRoleAsync(user, AdminRole);
                TempData["AdminMessage"] = _localizer["{0} este acum administrator.", user.UserName].Value;
            }

            return RedirectToAction("Users");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteUser(string id)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (id == currentUserId)
            {
                TempData["AdminError"] = _localizer["Nu îți poți șterge propriul cont din panoul de administrare."].Value;
                return RedirectToAction("Users");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Admins can never be deleted via this panel, by anyone - same reasoning as BanUser:
            // otherwise one admin could (accidentally or maliciously) remove every other admin
            // and leave the platform with no way back into this panel.
            if (await _userManager.IsInRoleAsync(user, AdminRole))
            {
                TempData["AdminError"] = _localizer["Nu poți șterge un alt administrator."].Value;
                return RedirectToAction("Users");
            }

            // Clean up everything that references this user before removing the account itself.
            var messages = await _context.Messages
                .Where(m => m.SenderId == id || m.ReceiverId == id)
                .ToListAsync();
            _context.Messages.RemoveRange(messages);

            var hireRequests = await _context.HireRequests
                .Where(h => h.RequesterId == id || h.TargetId == id)
                .ToListAsync();
            _context.HireRequests.RemoveRange(hireRequests);

            var otpCodes = await _context.OtpCodes
                .Where(o => o.UserId == id)
                .ToListAsync();
            _context.OtpCodes.RemoveRange(otpCodes);

            if (!string.IsNullOrEmpty(user.AvatarPath) && user.AvatarPath.StartsWith("/avatars/"))
            {
                var physicalPath = Path.Combine(_env.WebRootPath, "avatars", Path.GetFileName(user.AvatarPath));
                if (System.IO.File.Exists(physicalPath))
                {
                    try { System.IO.File.Delete(physicalPath); }
                    catch (IOException ex) { _logger.LogWarning(ex, "Could not delete avatar file for deleted user {UserId}.", id); }
                }
            }

            await _context.SaveChangesAsync();

            var deletedUserName = user.UserName;
            var result = await _userManager.DeleteAsync(user);

            if (result.Succeeded)
            {
                TempData["AdminMessage"] = _localizer["Contul {0} a fost șters.", deletedUserName].Value;
            }
            else
            {
                TempData["AdminError"] = _localizer["Contul nu a putut fi șters."].Value;
            }

            return RedirectToAction("Users");
        }

        // Bans/unbans a user. "duration" is one of: "1d", "7d", "1m", "permanent", "unban".
        // A permanent ban is stored as DateTime.MaxValue rather than a separate bool flag -
        // "is banned" is always just BannedUntil > UtcNow.
        [HttpPost]
        public async Task<IActionResult> BanUser(string id, string duration)
        {
            var currentUserId = _userManager.GetUserId(User);
            if (id == currentUserId)
            {
                TempData["AdminError"] = _localizer["Nu te poți bloca pe tine însuți."].Value;
                return RedirectToAction("Users");
            }

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            // Admins can never be banned via this panel, by anyone - otherwise an admin could
            // accidentally (or maliciously) lock out another admin with no way back in short of
            // editing the database directly.
            if (await _userManager.IsInRoleAsync(user, AdminRole))
            {
                TempData["AdminError"] = _localizer["Nu poți bloca un alt administrator."].Value;
                return RedirectToAction("Users");
            }

            switch (duration)
            {
                case "1d":
                    user.BannedUntil = DateTime.UtcNow.AddDays(1);
                    TempData["AdminMessage"] = _localizer["{0} a fost blocat pentru 1 zi.", user.UserName].Value;
                    break;
                case "7d":
                    user.BannedUntil = DateTime.UtcNow.AddDays(7);
                    TempData["AdminMessage"] = _localizer["{0} a fost blocat pentru 7 zile.", user.UserName].Value;
                    break;
                case "1m":
                    user.BannedUntil = DateTime.UtcNow.AddMonths(1);
                    TempData["AdminMessage"] = _localizer["{0} a fost blocat pentru 1 lună.", user.UserName].Value;
                    break;
                case "permanent":
                    user.BannedUntil = DateTime.MaxValue;
                    TempData["AdminMessage"] = _localizer["{0} a fost blocat permanent.", user.UserName].Value;
                    break;
                case "unban":
                    user.BannedUntil = null;
                    TempData["AdminMessage"] = _localizer["{0} a fost deblocat.", user.UserName].Value;
                    break;
                default:
                    TempData["AdminError"] = _localizer["Durată de blocare necunoscută."].Value;
                    return RedirectToAction("Users");
            }

            await _context.SaveChangesAsync();
            return RedirectToAction("Users");
        }

        // Read-only oversight view: every message a given user has SENT, for moderation
        // purposes. Doesn't expose the other side of each conversation beyond who received it.
        public async Task<IActionResult> UserMessages(string id)
        {
            var targetUser = await _userManager.FindByIdAsync(id);
            if (targetUser == null) return NotFound();

            var messages = await _context.Messages
                .Where(m => m.SenderId == id)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            var receiverIds = messages.Select(m => m.ReceiverId).Distinct().ToList();
            var receiversById = await _context.Users
                .Where(u => receiverIds.Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u);

            ViewBag.TargetUser = targetUser;
            ViewBag.ReceiversById = receiversById;

            return View(messages);
        }

        // ===================== TOPICS =====================

        public async Task<IActionResult> Topics()
        {
            var topics = await _context.Topics.OrderBy(t => t.Name).ToListAsync();
            return View(topics);
        }

        [HttpPost]
        public async Task<IActionResult> CreateTopic(string name, string? description)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["AdminError"] = _localizer["Numele materiei este obligatoriu."].Value;
                return RedirectToAction("Topics");
            }

            _context.Topics.Add(new Topic
            {
                Name = name.Trim(),
                Description = description?.Trim() ?? ""
            });
            await _context.SaveChangesAsync();

            TempData["AdminMessage"] = _localizer["Materia a fost adăugată."].Value;
            return RedirectToAction("Topics");
        }

        [HttpPost]
        public async Task<IActionResult> EditTopic(int id, string name, string? description)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null) return NotFound();

            if (string.IsNullOrWhiteSpace(name))
            {
                TempData["AdminError"] = _localizer["Numele materiei este obligatoriu."].Value;
                return RedirectToAction("Topics");
            }

            topic.Name = name.Trim();
            topic.Description = description?.Trim() ?? "";
            await _context.SaveChangesAsync();

            TempData["AdminMessage"] = _localizer["Materia a fost actualizată."].Value;
            return RedirectToAction("Topics");
        }

        [HttpPost]
        public async Task<IActionResult> DeleteTopic(int id)
        {
            var topic = await _context.Topics.FindAsync(id);
            if (topic == null) return NotFound();

            // Topic ids are stored as a comma-separated string on ApplicationUser.SelectedTopics,
            // not as a real foreign key, so there's no DB constraint to violate here - but we
            // still scrub references so nobody's profile points at a topic that no longer exists.
            var affectedUsers = await _context.Users
                .Where(u => u.SelectedTopics != null && u.SelectedTopics != "")
                .ToListAsync();

            foreach (var user in affectedUsers)
            {
                var ids = user.SelectedTopics!.Split(',', StringSplitOptions.RemoveEmptyEntries).ToList();
                if (ids.Remove(id.ToString()))
                {
                    user.SelectedTopics = string.Join(",", ids);
                }
            }

            _context.Topics.Remove(topic);
            await _context.SaveChangesAsync();

            TempData["AdminMessage"] = _localizer["Materia a fost ștearsă."].Value;
            return RedirectToAction("Topics");
        }
    }

    public class AdminDashboardStats
    {
        public int TotalUsers { get; set; }
        public int TeachCount { get; set; }
        public int LearnCount { get; set; }
        public int AdminCount { get; set; }
        public int TotalMessages { get; set; }
        public int PendingHireRequests { get; set; }
        public int AcceptedHireRequests { get; set; }
        public int TotalTopics { get; set; }
        public int BannedCount { get; set; }
    }
}
