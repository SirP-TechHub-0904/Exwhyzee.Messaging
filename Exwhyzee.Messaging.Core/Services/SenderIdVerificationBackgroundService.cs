using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Data.IServices;

namespace Exwhyzee.Messaging.Core.Services
{
    /// <summary>
    /// Background service that periodically polls KudiSMS (/check_senderID) every 1 hour
    /// to auto-verify and approve pending Sender IDs, and auto-submit unverified ones (/senderID).
    /// </summary>
    public class SenderIdVerificationBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<SenderIdVerificationBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromHours(1);

        public SenderIdVerificationBackgroundService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<SenderIdVerificationBackgroundService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("SenderIdVerificationBackgroundService started. Checking every {Interval} minutes.", _checkInterval.TotalMinutes);

            // Initial warm-up delay of 1 minute on startup
            try
            {
                await Task.Delay(TimeSpan.FromMinutes(1), stoppingToken);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await CheckAndSyncSenderIdsAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during Sender ID verification background cycle.");
                }

                try
                {
                    await Task.Delay(_checkInterval, stoppingToken);
                }
                catch (TaskCanceledException)
                {
                    break;
                }
            }

            _logger.LogInformation("SenderIdVerificationBackgroundService stopped.");
        }

        public async Task CheckAndSyncSenderIdsAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var clientService = scope.ServiceProvider.GetRequiredService<IClientService>();

            var pendingOrUnverified = await db.XyzSenderIDs
                .Include(s => s.Client)
                .Where(s => s.XYZ_status == null || 
                            s.XYZ_status == "Pending Approval" || 
                            s.XYZ_status == "Pending" || 
                            s.XYZ_status == "Not on Gateway" || 
                            s.XYZ_status == "fail")
                .ToListAsync(cancellationToken);

            if (!pendingOrUnverified.Any())
            {
                _logger.LogInformation("No pending or unverified Sender IDs to process.");
                return;
            }

            _logger.LogInformation("Processing {Count} unapproved/pending Sender IDs for gateway verification...", pendingOrUnverified.Count);

            foreach (var sid in pendingOrUnverified)
            {
                if (cancellationToken.IsCancellationRequested) break;

                if (string.IsNullOrWhiteSpace(sid.SenderId)) continue;

                string rawId = sid.SenderId.Trim();
                
                // Ensure alphanumeric formatting
                string cleanId = Regex.Replace(rawId, @"[^a-zA-Z0-9]", "");
                if (cleanId.Length > 11)
                {
                    cleanId = cleanId.Substring(0, 11);
                }

                if (string.IsNullOrEmpty(cleanId)) continue;

                try
                {
                    // 1. Check current status with KudiSMS POST /check_senderID
                    var checkResp = await clientService.VerifySenderId(cleanId);
                    string rawStatus = (!string.IsNullOrEmpty(checkResp?.senderidStatus) ? checkResp.senderidStatus : (checkResp?.msg ?? checkResp?.status ?? "")).ToLower();
                    string errorCode = checkResp?.error_code ?? "";

                    if (rawStatus == "approved" || rawStatus.Contains("active") || (checkResp?.status == "success" && (checkResp?.msg?.ToLower().Contains("approved") == true)))
                    {
                        sid.XYZ_status = "Approved";
                        sid.XYZ_error_code = "000";
                        sid.XYZ_msg = "Approved and active for SMS broadcasts.";

                        // Notify user in-app
                        if (sid.Client != null && !string.IsNullOrEmpty(sid.Client.UserId))
                        {
                            try
                            {
                                var notif = new AppNotification
                                {
                                    UserId = sid.Client.UserId,
                                    Title = $"Sender ID Approved: [{cleanId}]",
                                    Message = $"Great news! Your Sender ID [{cleanId}] has been verified and approved by operators for instant SMS broadcasts.",
                                    NotificationType = "SenderIdApproved",
                                    ActionUrl = "/ClientPanel/Dashboard/Compose",
                                    IsRead = false,
                                    DateCreated = DateTime.UtcNow
                                };
                                db.AppNotifications.Add(notif);
                            }
                            catch { }
                        }

                        _logger.LogInformation("Sender ID [{SenderId}] successfully approved on gateway!", cleanId);
                    }
                    else if (errorCode == "106" || rawStatus.Contains("doesn't exist") || rawStatus.Contains("do not exist") || sid.XYZ_status == "Not on Gateway")
                    {
                        // 2. Sender ID is not yet on gateway route -> Auto-submit via POST /senderID
                        var subResp = await clientService.SubmitSenderId(cleanId, "Account verification and transactional alerts.");
                        sid.XYZ_error_code = subResp?.error_code;

                        if (subResp != null && (subResp.status == "success" || subResp.error_code == "000" || subResp.msg?.ToLower().Contains("success") == true))
                        {
                            sid.XYZ_status = "Pending Approval";
                            sid.XYZ_msg = "Submitted to gateway for operator review (1-24 hrs).";
                            _logger.LogInformation("Sender ID [{SenderId}] auto-submitted to gateway for review.", cleanId);
                        }
                        else
                        {
                            sid.XYZ_status = "Not on Gateway";
                            sid.XYZ_msg = subResp?.msg ?? "Sender ID could not be submitted to gateway.";
                        }
                    }
                    else if (errorCode == "105" || rawStatus.Contains("blocked"))
                    {
                        sid.XYZ_status = "Blocked";
                        sid.XYZ_error_code = "105";
                        sid.XYZ_msg = "Sender ID has been blocked by operators.";
                    }
                    else
                    {
                        sid.XYZ_status = "Pending Approval";
                        sid.XYZ_msg = "Under operator review. Please check in 20 minutes.";
                    }

                    sid.SenderId = cleanId; // update with cleaned alphanumeric
                    db.Entry(sid).State = EntityState.Modified;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to check or auto-submit Sender ID [{SenderId}].", cleanId);
                }

                // Friendly throttle between API calls
                await Task.Delay(1000, cancellationToken);
            }

            await db.SaveChangesAsync(cancellationToken);
            _logger.LogInformation("Completed Sender ID verification cycle.");
        }
    }
}
