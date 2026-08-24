using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Data.Services;

namespace Exwhyzee.Messaging.Core.Services
{
    public class LowUnitAlertService : ILowUnitAlertService
    {
        private ApplicationDbContext CreateDbContext() => new ApplicationDbContext();

        public async Task CheckAndTriggerLowUnitAlertAsync(string userId, decimal currentUnits)
        {
            if (string.IsNullOrWhiteSpace(userId)) return;

            try
            {
                using var db = CreateDbContext();
                var client = await db.Clients.Include(c => c.User).FirstOrDefaultAsync(c => c.UserId == userId);
                if (client == null || client.User == null) return;

                decimal threshold = client.LowUnitReminderThreshold > 0 ? client.LowUnitReminderThreshold : 50.0m;

                if (currentUnits <= threshold)
                {
                    DateTime today = DateTime.UtcNow.Date;
                    if (client.LastLowUnitAlertDate.HasValue && client.LastLowUnitAlertDate.Value.Date == today)
                    {
                        // Cooldown protection: Alert already sent today
                        return;
                    }

                    await SendLowUnitAlertInternalAsync(db, client, currentUnits, threshold, isMorningCron: false);
                }
            }
            catch (Exception ex)
            {
                // Silent fail for background alert execution to never crash host
                System.Diagnostics.Debug.WriteLine($"[LowUnitAlertService] Error: {ex.Message}");
            }
        }

        public async Task RunDailyMorningLowUnitCheckAsync()
        {
            try
            {
                using var db = CreateDbContext();
                DateTime today = DateTime.UtcNow.Date;

                var clients = await db.Clients
                    .Include(c => c.User)
                    .Where(c => c.User != null)
                    .ToListAsync();

                foreach (var client in clients)
                {
                    decimal threshold = client.LowUnitReminderThreshold > 0 ? client.LowUnitReminderThreshold : 50.0m;
                    if (client.Units <= threshold)
                    {
                        if (client.LastLowUnitAlertDate.HasValue && client.LastLowUnitAlertDate.Value.Date == today)
                        {
                            continue; // Skip if alerted today
                        }

                        await SendLowUnitAlertInternalAsync(db, client, client.Units, threshold, isMorningCron: true);
                    }
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[LowUnitAlertService Cron] Error: {ex.Message}");
            }
        }

        private async Task SendLowUnitAlertInternalAsync(ApplicationDbContext db, Client client, decimal currentUnits, decimal threshold, bool isMorningCron)
        {
            string recipientEmail = client.User.Email;
            string clientName = $"{client.Surname} {client.FirstName}".Trim();
            if (string.IsNullOrWhiteSpace(clientName)) clientName = client.User.UserName;

            string subject = isMorningCron
                ? $"🔔 Morning Reminder: Low SMS Unit Balance ({currentUnits:N2} Units)"
                : $"⚠️ Low SMS Unit Alert: Balance is {currentUnits:N2} Units";

            string emailBody = $@"
<!DOCTYPE html>
<html>
<head>
    <meta charset='utf-8'/>
    <style>
        body {{ font-family: 'Segoe UI', Tahoma, Geneva, Verdana, sans-serif; background-color: #F8FAFC; margin: 0; padding: 20px; color: #0F172A; }}
        .card {{ max-width: 580px; margin: 0 auto; background: #FFFFFF; border-radius: 14px; border: 1px solid #E2E8F0; padding: 30px; box-shadow: 0 4px 20px rgba(0,0,0,0.05); }}
        .header {{ text-align: center; padding-bottom: 20px; border-bottom: 1px solid #E2E8F0; }}
        .header h2 {{ color: #E50914; margin: 0; font-size: 1.4rem; font-weight: 800; }}
        .stat-box {{ background: #FEF2F2; border: 1px solid #FCA5A5; border-radius: 10px; padding: 20px; margin: 24px 0; text-align: center; }}
        .stat-val {{ font-size: 2.2rem; font-weight: 800; color: #DC2626; font-family: monospace; }}
        .btn {{ display: inline-block; background: #E50914; color: #FFFFFF !important; text-decoration: none; padding: 12px 28px; border-radius: 10px; font-weight: 800; font-size: 1rem; box-shadow: 0 4px 14px rgba(229,9,20,0.3); }}
        .footer {{ margin-top: 28px; font-size: 0.82rem; color: #64748B; text-align: center; border-top: 1px solid #E2E8F0; padding-top: 16px; }}
    </style>
</head>
<body>
    <div class='card'>
        <div class='header'>
            <h2>⚠️ Low SMS Unit Balance Alert</h2>
        </div>
        <p style='font-size:1.05rem;'>Dear <strong>{clientName}</strong>,</p>
        <p style='font-size:0.95rem; color:#475569;'>Your Exwhyzee Bulk SMS wallet units have dropped below your configured reminder threshold of <strong>{threshold:N2} Units</strong>.</p>
        
        <div class='stat-box'>
            <small style='color:#991B1B; font-weight:800; text-transform:uppercase; letter-spacing:0.5px;'>Current Available Units</small>
            <div class='stat-val'>{currentUnits:N2}</div>
            <span style='color:#991B1B; font-size:0.85rem; font-weight:700;'>Reminder Threshold: {threshold:N2} Units</span>
        </div>

        <p style='font-size:0.95rem; color:#475569;'>To ensure uninterrupted SMS broadcasts and automated transactional alerts, please top up your account balance.</p>

        <div style='text-align:center; margin-top:24px;'>
            <a href='https://xyzsms.com/ClientPanel/Dashboard/BuyUnit' class='btn'>💳 Top Up Units Now →</a>
        </div>

        <div class='footer'>
            Exwhyzee Messaging Platform &bull; Automated System Notice
        </div>
    </div>
</body>
</html>";

            // 1. Send Email via ZeptoMail
            try
            {
                if (!string.IsNullOrWhiteSpace(recipientEmail))
                {
                    var mailService = new ZeptoMailService();
                    await mailService.SendEmailAsync(emailBody, recipientEmail, subject);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[ZeptoMail LowUnit] Email failed: {ex.Message}");
            }

            // 2. Add In-App Notification
            try
            {
                var notification = new AppNotification
                {
                    UserId = client.UserId,
                    Title = $"Low SMS Units Warning ({currentUnits:N2} Units)",
                    Message = $"Your available SMS balance ({currentUnits:N2} Units) is at or below your configured threshold of {threshold:N2} Units. Click to top up.",
                    NotificationType = "LowUnit",
                    ActionUrl = "/ClientPanel/Dashboard/BuyUnit",
                    IsRead = false,
                    DateCreated = DateTime.UtcNow
                };

                db.AppNotifications.Add(notification);
                await db.SaveChangesAsync();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[Notification LowUnit] In-app failed: {ex.Message}");
            }

            // 3. Update Last Alert Date
            client.LastLowUnitAlertDate = DateTime.UtcNow;
            await db.SaveChangesAsync();
        }
    }
}
