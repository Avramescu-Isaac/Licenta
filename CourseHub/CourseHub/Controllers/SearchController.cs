using CourseHub.Data;
using CourseHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseHub.Controllers
{
    [Authorize]
    public class SearchController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public SearchController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(string userName, string role, string teachingLevel, string selectedTopic)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            if (currentUser == null) return Unauthorized();

            // Admin accounts aren't real teachers or students - they should never be
            // searchable, filterable, or hireable, regardless of whatever Role they picked
            // in the onboarding wizard.
            var adminIds = (await _userManager.GetUsersInRoleAsync("Admin"))
                .Select(u => u.Id)
                .ToList();

            var users = _context.Users.Where(u => !adminIds.Contains(u.Id));

            if (!string.IsNullOrWhiteSpace(userName))
                users = users.Where(u => u.UserName.Contains(userName));

            if (!string.IsNullOrWhiteSpace(role))
                users = users.Where(u => u.Role == role);

            if (!string.IsNullOrWhiteSpace(teachingLevel))
                users = users.Where(u => u.TeachingLevel == teachingLevel);

            if (!string.IsNullOrWhiteSpace(selectedTopic))
            {
                // SelectedTopics is a comma-separated list of topic ids ("1,3,5").
                users = users.Where(u => u.SelectedTopics != null &&
                                         u.SelectedTopics.Contains(selectedTopic));
            }

            var matchedUserIds = await _context.HireRequests
            .Where(h => h.IsAccepted &&
                       (h.RequesterId == currentUser.Id || h.TargetId == currentUser.Id))
            .Select(h => h.RequesterId == currentUser.Id ? h.TargetId : h.RequesterId)
            .ToListAsync();
            var topics = await _context.Topics.ToListAsync();

            var teachingLevels = await _context.Users
                .Where(u => !adminIds.Contains(u.Id))
                .Where(u => u.TeachingLevel != null && u.TeachingLevel != "")
                .Select(u => u.TeachingLevel)
                .Distinct()
                .ToListAsync();
            var viewModel = new UserSearchViewModel
            {
                UserName = userName,
                Role = role,
                TeachingLevel = teachingLevel,
                SelectedTopic = selectedTopic,
                Results = await users.ToListAsync(),
                CurrentUserId = currentUser.Id,
                CurrentUserRole = currentUser.Role,
                MatchedUserIds = matchedUserIds,
                Topics = topics,
                TeachingLevels = teachingLevels
            };

            return View(viewModel);
        }
    }
}
