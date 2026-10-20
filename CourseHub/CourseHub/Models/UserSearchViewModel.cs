namespace CourseHub.Models
{
    public class UserSearchViewModel
    {
        public string? UserName { get; set; }
        public string? Role { get; set; }
        public string? TeachingLevel { get; set; }
        public string? SelectedTopic { get; set; }

        public List<ApplicationUser> Results { get; set; } = new();
        public string CurrentUserId { get; set; }
        public string CurrentUserRole { get; set; }
        public List<string> MatchedUserIds { get; set; } = new();
        public List<Topic> Topics { get; set; } = new();
        public List<string> TeachingLevels { get; set; } = new List<string>();
    }
}
