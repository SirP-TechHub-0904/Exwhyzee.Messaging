using System;
namespace Exwhyzee.Messaging.Core.Models
{
    public class MessageBatch
    {
        public long Id { get; set; }
        public string MessageContent { get; set; }
        public DateTime DeliveredDate { get; set; }
    }
}


