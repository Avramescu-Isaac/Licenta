using CourseHub.Data;
using CourseHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseHub.Controllers
{
    [Authorize]
    public class MessagesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;

        public MessagesController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Inbox()
        {
            var currentUser = await _userManager.GetUserAsync(User);

            var senderIds = await _context.Messages
                .Where(m => m.ReceiverId == currentUser.Id)
                .GroupBy(m => m.SenderId)
                .Select(g => g.Key)
                .ToListAsync();

            var senders = await _context.Users
                .Where(u => senderIds.Contains(u.Id))
                .ToListAsync();

            var unreadBySender = await _context.Messages
                .Where(m => m.ReceiverId == currentUser.Id && !m.IsRead)
                .GroupBy(m => m.SenderId)
                .Select(g => new { SenderId = g.Key, Count = g.Count() })
                .ToDictionaryAsync(x => x.SenderId, x => x.Count);

            var lastMessageBySender = await _context.Messages
                .Where(m => m.ReceiverId == currentUser.Id && senderIds.Contains(m.SenderId))
                .GroupBy(m => m.SenderId)
                .Select(g => new { SenderId = g.Key, LastAt = g.Max(m => m.Timestamp) })
                .ToDictionaryAsync(x => x.SenderId, x => x.LastAt);

            ViewBag.UnreadBySender = unreadBySender;

            // Show most recently active conversations first.
            senders = senders
                .OrderByDescending(s => lastMessageBySender.TryGetValue(s.Id, out var t) ? t : DateTime.MinValue)
                .ToList();

            return View(senders);
        }
    }
}
