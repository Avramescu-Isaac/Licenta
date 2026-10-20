using System.Text.Json;
using CourseHub.Models;
using Microsoft.Extensions.Options;

namespace CourseHub.Services
{
    public interface IRecaptchaVerifier
    {
        Task<bool> VerifyAsync(string? recaptchaResponse, string? remoteIp, CancellationToken cancellationToken = default);
    }

    // Verifies a Google reCAPTCHA v2 ("I'm not a robot") token against Google's siteverify endpoint.
    public class RecaptchaVerifier : IRecaptchaVerifier
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly RecaptchaSettings _settings;
        private readonly ILogger<RecaptchaVerifier> _logger;

        public RecaptchaVerifier(
            IHttpClientFactory httpClientFactory,
            IOptions<RecaptchaSettings> settings,
            ILogger<RecaptchaVerifier> logger)
        {
            _httpClientFactory = httpClientFactory;
            _settings = settings.Value;
            _logger = logger;
        }

        public async Task<bool> VerifyAsync(string? recaptchaResponse, string? remoteIp, CancellationToken cancellationToken = default)
        {
            // No token submitted at all -> definitely not verified.
            if (string.IsNullOrWhiteSpace(recaptchaResponse))
                return false;

            if (string.IsNullOrWhiteSpace(_settings.SecretKey))
            {
                // Misconfiguration guard: fail closed (block registration) rather than silently
                // letting everyone through if the secret key was never set up.
                _logger.LogError("Recaptcha:SecretKey is not configured. Rejecting registration until it is set.");
                return false;
            }

            var client = _httpClientFactory.CreateClient();

            var parameters = new Dictionary<string, string>
            {
                ["secret"] = _settings.SecretKey,
                ["response"] = recaptchaResponse
            };
            if (!string.IsNullOrEmpty(remoteIp))
            {
                parameters["remoteip"] = remoteIp;
            }

            try
            {
                using var response = await client.PostAsync(
                    "https://www.google.com/recaptcha/api/siteverify",
                    new FormUrlEncodedContent(parameters),
                    cancellationToken);

                response.EnsureSuccessStatusCode();

                var json = await response.Content.ReadAsStringAsync(cancellationToken);
                using var doc = JsonDocument.Parse(json);

                return doc.RootElement.TryGetProperty("success", out var successProp) && successProp.GetBoolean();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "reCAPTCHA verification request failed.");
                return false;
            }
        }
    }
}
