using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
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
        public string OtherNames { get; set; }
        public decimal Discount { get; set; }
        public AllowNotifications AllowNotifications { get; set; }

        public ApplicationUser User { get; set; }
    }
}