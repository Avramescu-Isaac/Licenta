using CourseHub.Data;
using CourseHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Localization;

namespace CourseHub.Controllers
{
    [Authorize]
    public class HireController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IStringLocalizer<HireController> _localizer;

        public HireController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IStringLocalizer<HireController> localizer)
        {
            _context = context;
            _userManager = userManager;
            _localizer = localizer;
        }

        [HttpPost]
        public async Task<IActionResult> SendHireRequest(string targetId)
        {
            var userId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);

            var target = await _context.Users.FindAsync(targetId);
            if (target == null || target.Role != "Teach" || userId == targetId)
            {
                TempData["HireError"] = _localizer["Nu s-a putut trimite cererea."].Value;
                return RedirectBackToSearch();
            }

            // Admins aren't real tutors, even if their onboarding Role happens to say "Teach" -
            // block hiring them directly, since Search already hides them from the UI.
            if (await _userManager.IsInRoleAsync(target, "Admin"))
            {
                TempData["HireError"] = _localizer["Nu s-a putut trimite cererea."].Value;
                return RedirectBackToSearch();
            }

            var exists = await _context.HireRequests
                .AnyAsync(r => r.RequesterId == userId && r.TargetId == targetId);
            if (exists)
            {
                TempData["HireError"] = _localizer["Ai trimis deja o cerere acestui profesor."].Value;
                return RedirectBackToSearch();
            }

            var request = new HireRequest
            {
                RequesterId = userId,
                TargetId = targetId,
                IsAccepted = false
            };

            _context.HireRequests.Add(request);
            await _context.SaveChangesAsync();

            TempData["HireMessage"] = _localizer["Cerere trimisă!"].Value;
            return RedirectBackToSearch();
        }

        // Sends the user back to wherever they clicked "Trimite cerere" from (the Search results
        // page, with their filters intact), falling back to a plain Search page if there's
        // no usable referer for some reason.
        private IActionResult RedirectBackToSearch()
        {
            var referer = Request.Headers.Referer.ToString();
            if (!string.IsNullOrEmpty(referer) && Url.IsLocalUrl(referer))
            {
                return Redirect(referer);
            }

            return RedirectToAction("Index", "Search");
        }

        // Teachers see requests sent TO them (pending ones, with an Accept button).
        // Students/learners see requests THEY sent, with a status badge instead - this used
        // to just return Unauthorized for anyone who wasn't a teacher, which is what you saw.
        [HttpGet]
        public async Task<IActionResult> Requests()
        {
            var currentUserId = _userManager.GetUserId(User);
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Unauthorized();

            if (user.Role == "Teach")
            {
                var incoming = await _context.HireRequests
                    .Include(r => r.Requester)
                    .Where(r => r.TargetId == currentUserId && !r.IsAccepted)
                    .OrderByDescending(r => r.RequestedAt)
                    .ToListAsync();

                ViewBag.IsTeach = true;
                return View(incoming);
            }
            else
            {
                var sent = await _context.HireRequests
                    .Include(r => r.Target)
                    .Where(r => r.RequesterId == currentUserId)
                    .OrderByDescending(r => r.RequestedAt)
                    .ToListAsync();

                ViewBag.IsTeach = false;
                return View(sent);
            }
        }

        [HttpPost]
        public async Task<IActionResult> Accept(int id)
        {
            var request = await _context.HireRequests.FindAsync(id);
            if (request == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            if (request.TargetId != currentUserId) return Unauthorized();

            request.IsAccepted = true;
            await _context.SaveChangesAsync();

            TempData["HireMessage"] = _localizer["Cererea a fost acceptată."].Value;
            return RedirectToAction("Requests");
        }

        // Lets a teacher decline a pending request sent to them. Unlike Cancel (which the
        // requester uses to withdraw their own pending request), this is the target/teacher
        // side of the same "pending" state - the request is simply removed so the student
        // is free to send a new one later if they want to.
        [HttpPost]
        public async Task<IActionResult> Reject(int id)
        {
            var request = await _context.HireRequests.FindAsync(id);
            if (request == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            if (request.TargetId != currentUserId) return Unauthorized();

            if (!request.IsAccepted)
            {
                _context.HireRequests.Remove(request);
                await _context.SaveChangesAsync();
                TempData["HireMessage"] = _localizer["Cererea a fost respinsă."].Value;
            }

            return RedirectToAction("Requests");
        }

        // Lets a student cancel/withdraw a hire request they sent (only while it's still
        // pending - once a teacher accepts, canceling here doesn't make sense anymore).
        [HttpPost]
        public async Task<IActionResult> Cancel(int id)
        {
            var request = await _context.HireRequests.FindAsync(id);
            if (request == null) return NotFound();

            var currentUserId = _userManager.GetUserId(User);
            if (request.RequesterId != currentUserId) return Unauthorized();

            if (!request.IsAccepted)
            {
                _context.HireRequests.Remove(request);
                await _context.SaveChangesAsync();
                TempData["HireMessage"] = _localizer["Cererea a fost anulată."].Value;
            }

            return RedirectToAction("Requests");
        }
    }
}
