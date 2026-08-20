using System;
using System.Collections.Generic;
using System.Linq;


namespace Exwhyzee.Messaging.Core.Models
{
    public class Client
    {
        public Client()
        {
            Discount = 0;
            AllowNotifications = AllowNotifications.Allow;
        }

        public int ClientId { get; set; }
        public string UserId { get; set; }
        public decimal Units { get; set; }
        public string Surname { get; set; }
        public string FirstName { get; set; }
        public string ApiKey { get; set; }
        public string GeminiApiKey { get; set; }
        public string OtherNames { get; set; }
        public decimal Discount { get; set; }
        public AllowNotifications AllowNotifications { get; set; }

        public decimal LowUnitReminderThreshold { get; set; } = 50.0m;
        public DateTime? LastLowUnitAlertDate { get; set; }

        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("UserId")]
        public ApplicationUser User { get; set; }
    }
}

