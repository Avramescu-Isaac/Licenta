using System.ComponentModel.DataAnnotations;

namespace CourseHub.Models
{
    public class Message
    {
        public int Id { get; set; }

        [Required]
        public string SenderId { get; set; }

        [Required]
        public string ReceiverId { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime Timestamp { get; set; } = DateTime.Now;

        // Whether the receiver has opened the conversation this message belongs to
        // since it was sent. Used to drive the unread-message badge in the navbar.
        public bool IsRead { get; set; } = false;

        // Optional file attachment (currently only teachers can attach files to a message).
        // FilePath is the web-relative path (e.g. "/chat-files/xxxx.pdf"); null for plain
        // text messages.
        public string? FilePath { get; set; }
        public string? FileName { get; set; }
        public long? FileSize { get; set; }
    }
}
