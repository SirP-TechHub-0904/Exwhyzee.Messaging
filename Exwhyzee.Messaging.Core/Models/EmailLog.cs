using System;
using System.ComponentModel.DataAnnotations;

namespace Exwhyzee.Messaging.Core.Models
{
    public class EmailLog
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(250)]
        public string RecipientEmail { get; set; }

        [StringLength(300)]
        public string Subject { get; set; }

        public string BodyHtml { get; set; }

        public DateTime DateSent { get; set; } = DateTime.UtcNow;

        public bool IsSuccess { get; set; }

        public string ErrorMessage { get; set; }
    }
}
