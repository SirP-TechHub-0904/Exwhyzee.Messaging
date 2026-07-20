using Exwhyzee.Messaging.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Exwhyzee.Messaging.Core.Dtos
{
    public class ClientDto
    {
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

