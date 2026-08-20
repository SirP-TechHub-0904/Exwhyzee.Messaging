using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Exwhyzee.Messaging.Core.Models
{
    public class SupportTicket
    {
        [Key]
        public int TicketId { get; set; }

        [Required]
        [StringLength(50)]
        public string TicketNumber { get; set; }

        public string UserId { get; set; }

        [Required]
        [StringLength(100)]
        public string SenderName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(150)]
        public string SenderEmail { get; set; }

        [StringLength(20)]
        public string SenderPhone { get; set; }

        [Required]
        [StringLength(200)]
        public string Subject { get; set; }

        [Required]
        public string Message { get; set; }

        public TicketCategory Category { get; set; }
        public TicketPriority Priority { get; set; }
        public TicketStatus Status { get; set; }

        public DateTime DateCreated { get; set; }
        public DateTime DateUpdated { get; set; }

        public virtual ICollection<TicketResponse> Responses { get; set; } = new List<TicketResponse>();
    }

    public class TicketResponse
    {
        [Key]
        public int ResponseId { get; set; }

        public int TicketId { get; set; }

        public string SenderUserId { get; set; }

        [Required]
        [StringLength(100)]
        public string SenderName { get; set; }

        public bool IsAdminReply { get; set; }

        [StringLength(100)]
        public string AdminRepliedBy { get; set; }

        [Required]
        public string Message { get; set; }

        public DateTime DateCreated { get; set; }

        [ForeignKey("TicketId")]
        public virtual SupportTicket Ticket { get; set; }
    }
}
