using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Exwhyzee.Messaging.Core.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Exwhyzee.Messaging.Core.Services
{
    public class NotificationService : INotificationService
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly ILogger<NotificationService> _logger;
        private static readonly HttpClient _httpClient = new HttpClient();

        public NotificationService(
            ApplicationDbContext db,
            IConfiguration configuration,
            ILogger<NotificationService> logger)
        {
            _db = db;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<AppNotification> SendNotificationAsync(string userId, string title, string message, string type = "System", string actionUrl = null)
        {
            try
            {
                var notification = new AppNotification
                {
                    UserId = userId,
                    Title = title,
                    Message = message,
                    NotificationType = type,
                    ActionUrl = actionUrl,
                    IsRead = false,
                    DateCreated = DateTime.UtcNow
                };

                _db.AppNotifications.Add(notification);
                await _db.SaveChangesAsync();

                // Trigger OneSignal Web Push in background if credentials exist
                _ = Task.Run(() => SendOneSignalPushAsync(userId, title, message, actionUrl));

                return notification;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error creating AppNotification for User {UserId}", userId);
                return null;
            }
        }

        public async Task<List<AppNotification>> GetUserNotificationsAsync(string userId, int count = 20)
        {
            try
            {
                return await _db.AppNotifications
                    .Where(x => x.UserId == userId)
                    .OrderByDescending(x => x.DateCreated)
                    .Take(count)
                    .ToListAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error fetching notifications for User {UserId}", userId);
                return new List<AppNotification>();
            }
        }

        public async Task<int> GetUnreadCountAsync(string userId)
        {
            try
            {
                return await _db.AppNotifications
                    .Where(x => x.UserId == userId && !x.IsRead)
                    .CountAsync();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error getting unread notification count for User {UserId}", userId);
                return 0;
            }
        }

        public async Task<bool> MarkAsReadAsync(int notificationId, string userId)
        {
            try
            {
                var notif = await _db.AppNotifications.FirstOrDefaultAsync(x => x.Id == notificationId && x.UserId == userId);
                if (notif != null)
                {
                    notif.IsRead = true;
                    notif.DateRead = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                    return true;
                }
                return false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking notification {Id} as read", notificationId);
                return false;
            }
        }

        public async Task<bool> MarkAllAsReadAsync(string userId)
        {
            try
            {
                var unreadList = await _db.AppNotifications.Where(x => x.UserId == userId && !x.IsRead).ToListAsync();
                foreach (var item in unreadList)
                {
                    item.IsRead = true;
                    item.DateRead = DateTime.UtcNow;
                }
                await _db.SaveChangesAsync();
                return true;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error marking all notifications as read for User {UserId}", userId);
                return false;
            }
        }

        private async Task SendOneSignalPushAsync(string userId, string title, string message, string actionUrl)
        {
            try
            {
                string appId = _configuration["OneSignal:AppId"];
                string restKey = _configuration["OneSignal:RestApiKey"];

                if (string.IsNullOrWhiteSpace(appId) || string.IsNullOrWhiteSpace(restKey) || appId.Contains("YOUR_ONESIGNAL"))
                {
                    return; // OneSignal not configured yet
                }

                var payload = new
                {
                    app_id = appId,
                    target_channel = "push",
                    include_aliases = new { external_id = new[] { userId } },
                    include_external_user_ids = new[] { userId },
                    headings = new { en = title },
                    contents = new { en = message },
                    url = actionUrl
                };

                var request = new HttpRequestMessage(HttpMethod.Post, "https://api.onesignal.com/notifications");
                request.Headers.TryAddWithoutValidation("Authorization", "Key " + restKey);
                request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await _httpClient.SendAsync(request);
                if (!response.IsSuccessStatusCode)
                {
                    var responseBody = await response.Content.ReadAsStringAsync();
                    _logger.LogWarning("OneSignal push notification failed: {Status} - {Body}", response.StatusCode, responseBody);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error dispatching OneSignal push for user {UserId}", userId);
            }
        }
    }
}
