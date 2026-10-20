using CourseHub.Data;
using CourseHub.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace CourseHub.Controllers.Api
{
    [Route("api/messages")]
    [ApiController]
    [Authorize]
    public class MessagesController : ControllerBase
    {
        private static readonly string[] AllowedFileExtensions =
        {
            ".pdf", ".doc", ".docx", ".ppt", ".pptx", ".xls", ".xlsx", ".txt", ".zip",
            ".png", ".jpg", ".jpeg", ".gif", ".webp"
        };
        private const long MaxFileSizeBytes = 15 * 1024 * 1024; // 15 MB

        private readonly ApplicationDbContext _context;
        private readonly UserManager<ApplicationUser> _userManager;
        private readonly IWebHostEnvironment _env;
        private readonly ILogger<MessagesController> _logger;

        public MessagesController(
            ApplicationDbContext context,
            UserManager<ApplicationUser> userManager,
            IWebHostEnvironment env,
            ILogger<MessagesController> logger)
        {
            _context = context;
            _userManager = userManager;
            _env = env;
            _logger = logger;
        }

        // Two users may only exchange messages once a hire request between them has been
        // accepted. Checked on every write path, not just hidden in the UI - a message
        // endpoint that trusts the client's receiverId would let any account write to any other.
        private Task<bool> AreConnectedAsync(string userA, string userB) =>
            _context.HireRequests.AnyAsync(h =>
                h.IsAccepted &&
                ((h.RequesterId == userA && h.TargetId == userB) ||
                 (h.RequesterId == userB && h.TargetId == userA)));

        [HttpPost]
        public async Task<IActionResult> Post([FromBody] Message message)
        {
            // Never trust a client-supplied SenderId - always use the authenticated user,
            // otherwise anyone could send messages that appear to be from someone else.
            var senderId = _userManager.GetUserId(User);
            if (senderId == null) return Unauthorized();

            if (string.IsNullOrWhiteSpace(message.ReceiverId))
                return BadRequest("Missing receiverId.");

            if (!await AreConnectedAsync(senderId, message.ReceiverId))
                return Forbid();

            message.SenderId = senderId;
            message.IsRead = false;
            _context.Messages.Add(message);
            await _context.SaveChangesAsync();
            return Ok();
        }

        // Lets a teacher attach a file (worksheet, slides, etc.) to a chat message.
        // Only accounts with Role == "Teach" may use this - enforced here, not just hidden
        // in the UI, since a student could otherwise call this endpoint directly.
        [HttpPost("upload-file")]
        [RequestSizeLimit(MaxFileSizeBytes)]
        public async Task<IActionResult> UploadFile([FromForm] string receiverId, [FromForm] IFormFile file)
        {
            var senderId = _userManager.GetUserId(User);
            if (senderId == null) return Unauthorized();

            var sender = await _userManager.FindByIdAsync(senderId);
            if (sender == null || sender.Role != "Teach")
                return Forbid();

            if (string.IsNullOrWhiteSpace(receiverId))
                return BadRequest("Missing receiverId.");

            if (!await AreConnectedAsync(senderId, receiverId))
                return Forbid();

            if (file == null || file.Length == 0)
                return BadRequest("No file provided.");

            if (file.Length > MaxFileSizeBytes)
                return BadRequest("File is too large.");

            var extension = Path.GetExtension(file.FileName).ToLowerInvariant();
            if (Array.IndexOf(AllowedFileExtensions, extension) < 0)
                return BadRequest("Unsupported file type.");

            var chatFilesFolder = Path.Combine(_env.WebRootPath, "chat-files");
            Directory.CreateDirectory(chatFilesFolder);

            // GUID-prefixed filename avoids overwriting another file that happens to share
            // the same original name (unlike plain-original-filename storage, which can
            // silently clobber a previous upload).
            var storedFileName = $"{Guid.NewGuid():N}{extension}";
            var physicalPath = Path.Combine(chatFilesFolder, storedFileName);

            using (var stream = new FileStream(physicalPath, FileMode.Create))
            {
                await file.CopyToAsync(stream);
            }

            var originalFileName = Path.GetFileName(file.FileName);

            var message = new Message
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Content = $"{originalFileName}",
                FilePath = $"/chat-files/{storedFileName}",
                FileName = originalFileName,
                FileSize = file.Length,
                IsRead = false
            };

            _context.Messages.Add(message);
            await _context.SaveChangesAsync();

            return Ok(new
            {
                filePath = message.FilePath,
                fileName = message.FileName,
                fileSize = message.FileSize
            });
        }

        // Used by the navbar notification bell to poll for new messages without a full page reload.
        [HttpGet("unread-count")]
        public async Task<IActionResult> UnreadCount()
        {
            var userId = _userManager.GetUserId(User);
            if (userId == null) return Unauthorized();

            var count = await _context.Messages
                .CountAsync(m => m.ReceiverId == userId && !m.IsRead);

            return Ok(new { count });
        }
    }
}
