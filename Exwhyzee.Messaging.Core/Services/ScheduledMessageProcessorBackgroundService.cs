using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Services;

namespace Exwhyzee.Messaging.Core.Services
{
    public class ScheduledMessageProcessorBackgroundService : BackgroundService
    {
        private readonly IServiceScopeFactory _serviceScopeFactory;
        private readonly ILogger<ScheduledMessageProcessorBackgroundService> _logger;
        private readonly TimeSpan _checkInterval = TimeSpan.FromSeconds(30);

        public ScheduledMessageProcessorBackgroundService(
            IServiceScopeFactory serviceScopeFactory,
            ILogger<ScheduledMessageProcessorBackgroundService> logger)
        {
            _serviceScopeFactory = serviceScopeFactory;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("ScheduledMessageProcessorBackgroundService started. Checking every {Interval}s.", _checkInterval.TotalSeconds);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ProcessDueScheduledMessagesAsync(stoppingToken);
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Error occurred during scheduled message processing cycle.");
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

            _logger.LogInformation("ScheduledMessageProcessorBackgroundService stopped.");
        }

        public async Task ProcessDueScheduledMessagesAsync(CancellationToken cancellationToken = default)
        {
            using var scope = _serviceScopeFactory.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var clientService = scope.ServiceProvider.GetRequiredService<IClientService>();
            var notificationService = scope.ServiceProvider.GetService<INotificationService>();

            var nowUtc = DateTime.UtcNow;
            var nowLocal = DateTime.Now;

            // Query due scheduled messages (accepting either UTC or Local matches to handle all server time configurations)
            var dueMessages = await db.Messages
                .Where(m => m.Status == MessageStatus.Scheduled && m.Scheduleddate != null && (m.Scheduleddate <= nowUtc || m.Scheduleddate <= nowLocal))
                .OrderBy(m => m.Scheduleddate)
                .Take(25)
                .ToListAsync(cancellationToken);

            if (!dueMessages.Any())
            {
                return;
            }

            _logger.LogInformation("Found {Count} due scheduled message(s) ready for dispatch.", dueMessages.Count);

            foreach (var message in dueMessages)
            {
                if (cancellationToken.IsCancellationRequested) break;

                try
                {
                    // 1. Parse and format recipients
                    List<string> rawNumbers = SmsServices.RemoveDuplicates(message.Recipients ?? "");
                    List<string> formattedNumbers = SmsServices.FormatNumbers(rawNumbers);

                    if (!formattedNumbers.Any())
                    {
                        message.Status = MessageStatus.Failed;
                        message.Response = "No valid recipients found in scheduled message.";
                        db.Entry(message).State = EntityState.Modified;
                        await db.SaveChangesAsync(cancellationToken);
                        continue;
                    }

                    // 2. Calculate Page count and Units required
                    int pageCount = SmsServices.CountPage(message.MessageContent ?? "");
                    decimal unitsRate = SmsServices.UnitsPerPage(formattedNumbers);
                    decimal totalUnitsNeeded = pageCount * unitsRate;

                    // 3. Check Client Wallet Balance
                    var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == message.UserId, cancellationToken);
                    if (client == null)
                    {
                        message.Status = MessageStatus.Failed;
                        message.Response = "Client profile not found.";
                        db.Entry(message).State = EntityState.Modified;
                        await db.SaveChangesAsync(cancellationToken);
                        continue;
                    }

                    if (client.Units < totalUnitsNeeded)
                    {
                        message.Status = MessageStatus.Failed;
                        message.Response = $"Insufficient wallet units ({client.Units:N2} available, {totalUnitsNeeded:N2} required).";
                        db.Entry(message).State = EntityState.Modified;
                        await db.SaveChangesAsync(cancellationToken);

                        if (notificationService != null)
                        {
                            await notificationService.SendNotificationAsync(
                                message.UserId,
                                "Scheduled Broadcast Failed: Low Units",
                                $"Your scheduled SMS broadcast [{message.SenderId}] could not be dispatched because your wallet balance is insufficient ({client.Units:N2} available vs {totalUnitsNeeded:N2} required). Please top up your wallet.",
                                "LOW_BALANCE",
                                "/ClientPanel/Dashboard/BuyUnit");
                        }
                        continue;
                    }

                    // 4. Dispatch SMS via Gateway
                    var response = await clientService.SendSmsById(message.MessageId, totalUnitsNeeded);

                    if (response != null && !string.IsNullOrEmpty(response.status) && response.status.ToLower().Contains("success"))
                    {
                        // Deduct Units from client wallet
                        client.Units -= totalUnitsNeeded;
                        db.Entry(client).State = EntityState.Modified;

                        // Mark Message as Sent
                        message.UnitsUsed = totalUnitsNeeded;
                        message.Status = MessageStatus.Sent;
                        message.DeliveredDate = DateTime.UtcNow;
                        message.Response = response.msg ?? "Dispatched successfully";
                        message.Response_status = response.status;
                        message.Response_cost = response.cost;
                        message.Response_msg = response.msg;
                        message.Response_page = response.page;
                        message.Response_error_code = response.error_code;
                        db.Entry(message).State = EntityState.Modified;

                        await db.SaveChangesAsync(cancellationToken);

                        _logger.LogInformation("Scheduled message ID {Id} sent successfully to {Count} recipients. Units used: {Units}.", message.MessageId, formattedNumbers.Count, totalUnitsNeeded);

                        if (notificationService != null)
                        {
                            await notificationService.SendNotificationAsync(
                                message.UserId,
                                "Scheduled Broadcast Dispatched",
                                $"Your scheduled broadcast [{message.SenderId}] to {formattedNumbers.Count} recipient(s) was dispatched successfully ({totalUnitsNeeded:N2} units used).",
                                "SMS_DISPATCH",
                                "/ClientPanel/Dashboard/MessageHistory");
                        }
                    }
                    else
                    {
                        // Gateway rejected
                        message.Status = MessageStatus.Failed;
                        message.Response = response?.msg ?? response?.status ?? "Gateway returned error on dispatch.";
                        message.Response_status = response?.status;
                        message.Response_error_code = response?.error_code;
                        db.Entry(message).State = EntityState.Modified;
                        await db.SaveChangesAsync(cancellationToken);

                        _logger.LogWarning("Scheduled message ID {Id} dispatch failed: {Msg}", message.MessageId, message.Response);

                        if (notificationService != null)
                        {
                            await notificationService.SendNotificationAsync(
                                message.UserId,
                                "Scheduled Broadcast Dispatch Error",
                                $"Your scheduled broadcast [{message.SenderId}] encountered a gateway error: {message.Response}",
                                "SMS_FAILED",
                                "/ClientPanel/Dashboard/MessageHistory");
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "Exception while dispatching scheduled message ID {Id}", message.MessageId);
                    message.Status = MessageStatus.Failed;
                    message.Response = $"System error: {ex.Message}";
                    db.Entry(message).State = EntityState.Modified;
                    await db.SaveChangesAsync(cancellationToken);
                }
            }
        }
    }
}
