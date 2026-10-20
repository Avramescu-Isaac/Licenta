using CourseHub.Data;
using CourseHub.Models;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace CourseHub.Services
{
    public class OtpCleanupService : BackgroundService
    {
        private readonly IServiceProvider _serviceProvider;
        private readonly ILogger<OtpCleanupService> _logger;

        public OtpCleanupService(IServiceProvider serviceProvider, ILogger<OtpCleanupService> logger)
        {
            _serviceProvider = serviceProvider;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("Serviciul de curățare OTP a pornit.");

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    _logger.LogInformation("Verific OTP-uri expirate...");
                    using (var scope = _serviceProvider.CreateScope())
                    {
                        var context = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
                        var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();

                        var expiredOtps = await context.OtpCodes
                            .Where(o => o.ExpirationTime < DateTime.UtcNow)
                            .ToListAsync();

                        foreach (var otp in expiredOtps)
                        {
                            var user = await userManager.FindByIdAsync(otp.UserId);
                            if (user != null && !user.EmailConfirmed)
                            {
                                await userManager.DeleteAsync(user);
                                _logger.LogInformation("Șters utilizator expirat: {UserId}", user.Id);
                            }

                            context.OtpCodes.Remove(otp);
                            _logger.LogInformation("Șters OTP expirat: {Code} (UserId={UserId})", otp.Code, otp.UserId);
                        }

                        await context.SaveChangesAsync();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Eroare la curățarea OTP-urilor");
                }

                await Task.Delay(TimeSpan.FromSeconds(10), stoppingToken);
            }
        }
    }
}
