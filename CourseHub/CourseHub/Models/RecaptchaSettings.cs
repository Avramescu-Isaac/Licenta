namespace CourseHub.Models
{
    public class RecaptchaSettings
    {
        // Public site key, shown in the page (safe to be public) - already hardcoded in Register.cshtml.
        public string SiteKey { get; set; } = string.Empty;

        // Private secret key, used server-side to verify the token with Google. NEVER commit the real value -
        // set it via `dotnet user-secrets set "Recaptcha:SecretKey" "..."` instead.
        public string SecretKey { get; set; } = string.Empty;
    }
}
