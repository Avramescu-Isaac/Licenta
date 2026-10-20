
using CourseHub.Models;
using Microsoft.AspNetCore.Identity.UI.Services;
using Microsoft.Extensions.Options;
using System.Net;
using System.Net.Mail;
using System.Threading.Tasks;

namespace CourseHub.Services
{
    public class EmailSender : IEmailSender
    {
        private readonly EmailSettings _settings;
        public EmailSender(IOptions<EmailSettings> options)
        {
            _settings = options.Value;
        }

        public async Task SendEmailAsync(string email, string subject, string htmlMessage)
        {
            var msg = new MailMessage
            {
                From = new MailAddress(_settings.From, "CourseHub"),
                Subject = subject,
                Body = htmlMessage,
                IsBodyHtml = true
            };
            msg.To.Add(email);

            using var client = new SmtpClient(_settings.Host, _settings.Port)
            {
                EnableSsl = _settings.EnableSsl,
                Credentials = new NetworkCredential(_settings.UserName, _settings.Password),
                Timeout = 60_000
            };

            await client.SendMailAsync(msg);
        }
    }
}