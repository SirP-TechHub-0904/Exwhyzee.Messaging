using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
{
    public class Message
    {
        public int MessageId { get; set; }
        public string SenderId { get; set; }
        public string Recipients { get; set; }
        public string MessageContent { get; set; }
        public string Response { get; set; }
        public string SummaryReport { get; set; }
        public decimal UnitsUsed { get; set; }
        public DateTime? Scheduleddate { get; set; }
        public DateTime? DeliveredDate { get; set; }
        public MessageStatus Status { get; set; }
        public string UserId { get; set; }
        
        public ApplicationUser User { get; set; }
        public string Resent { get; set; }
    }
}