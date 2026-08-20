using System.Collections.Generic;
using System.Threading.Tasks;
using Exwhyzee.Messaging.Core.Models;

namespace Exwhyzee.Messaging.Core.Services
{
    public interface INotificationService
    {
        Task<AppNotification> SendNotificationAsync(string userId, string title, string message, string type = "System", string actionUrl = null);
        Task<List<AppNotification>> GetUserNotificationsAsync(string userId, int count = 20);
        Task<int> GetUnreadCountAsync(string userId);
        Task<bool> MarkAsReadAsync(int notificationId, string userId);
        Task<bool> MarkAllAsReadAsync(string userId);
    }
}
