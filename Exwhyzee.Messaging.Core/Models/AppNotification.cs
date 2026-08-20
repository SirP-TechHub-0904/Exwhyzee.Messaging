using System;
using System.ComponentModel.DataAnnotations;

namespace Exwhyzee.Messaging.Core.Models
{
    public class AppNotification
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        [StringLength(200)]
        public string Title { get; set; }

        [Required]
        public string Message { get; set; }

        [StringLength(50)]
        public string NotificationType { get; set; } = "System"; // "Wallet", "SenderId", "Broadcast", "System", "Security"

        [StringLength(500)]
        public string ActionUrl { get; set; }

        public bool IsRead { get; set; } = false;

        public DateTime DateCreated { get; set; } = DateTime.UtcNow;

        public DateTime? DateRead { get; set; }
    }
}
