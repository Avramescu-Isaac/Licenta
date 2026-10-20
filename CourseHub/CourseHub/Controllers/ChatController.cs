using CourseHub.Data;
using CourseHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseHub.Controllers
{
    [Authorize]
    public class ChatController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public ChatController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> WithUser(string userId)
        {
            var currentUser = await _userManager.GetUserAsync(User);
            var currentUserId = currentUser?.Id;
            var targetUser = await _context.Users.FindAsync(userId);
            if (targetUser == null) return NotFound();

            // A conversation only exists between two users linked by an accepted hire request.
            // The UI only ever links here from the connections list, but the check has to live
            // on the server too - otherwise anyone could open a chat with any account just by
            // typing its id into the URL.
            var areConnected = await _context.HireRequests.AnyAsync(h =>
                h.IsAccepted &&
                ((h.RequesterId == currentUserId && h.TargetId == userId) ||
                 (h.RequesterId == userId && h.TargetId == currentUserId)));

            if (!areConnected) return Forbid();

            var messages = await _context.Messages
                .Where(m =>
                    (m.SenderId == currentUserId && m.ReceiverId == userId) ||
                    (m.SenderId == userId && m.ReceiverId == currentUserId))
                .OrderBy(m => m.Timestamp)
                .ToListAsync();

            // Opening this conversation marks anything this user sent us as read,
            // which is what drives the unread-count badge in the navbar.
            var unreadFromThisUser = messages
                .Where(m => m.ReceiverId == currentUserId && !m.IsRead)
                .ToList();

            if (unreadFromThisUser.Count > 0)
            {
                foreach (var message in unreadFromThisUser)
                {
                    message.IsRead = true;
                }
                await _context.SaveChangesAsync();
            }

            ViewBag.TargetUser = targetUser;
            // Only teachers can attach files - the button only makes sense (and the
            // server-side endpoint only allows it) when the current user is a "Teach" account.
            ViewBag.CanSendFiles = currentUser?.Role == "Teach";
            return View(messages);
        }
    }
}
