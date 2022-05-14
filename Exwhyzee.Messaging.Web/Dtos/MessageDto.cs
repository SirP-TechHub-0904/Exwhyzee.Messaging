using Exwhyzee.Messaging.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Dtos
{
    public class MessageDto
    {
        public int MessageId { get; set; }
        public string SenderId { get; set; }
        public DateTime? DeliveredDate { get; set; }
        public MessageStatus Status { get; set; }
        
        public string Recipients { get; set; }
        public int RecipientsCount { get; set; }
        public decimal UnitsUsed { get; set; }
        public DateTime? Scheduleddate { get; set; }


    }
}