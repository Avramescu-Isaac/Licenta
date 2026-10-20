using Microsoft.AspNetCore.Identity;

namespace CourseHub.Models
{
    

    public class ApplicationUser : IdentityUser
    {
        public string? Bio { get; set; }

        public string? Role { get; set; }
        public string? EducationLevel { get; set; }
        public string? TeachingLevel { get; set; }
        public int? Age { get; set; }
        public string? Gender { get; set; }
        public string? SelectedTopics { get; set; }

        // Relative web path to the user's avatar image (e.g. "/avatars/xxxx.jpg"),
        // or null if the user has no custom avatar and should fall back to their initial.
        public string? AvatarPath { get; set; }

        // Null = not banned. A future UTC date = banned until then.
        // DateTime.MaxValue is used to represent a permanent ban (so "is banned" is always
        // just "BannedUntil > UtcNow", no separate bool flag needed).
        public DateTime? BannedUntil { get; set; }
    }
}
