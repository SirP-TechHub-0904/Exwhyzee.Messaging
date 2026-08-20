using System;
using System.Collections.Generic;

namespace Exwhyzee.Messaging.Core.Dtos
{
    public class UserManagementDto
    {
        public string UserId { get; set; }
        public int ClientId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public string PhoneNumber { get; set; }
        public decimal UnitsBalance { get; set; }
        public DateTime DateRegistered { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public bool LockoutEnabled { get; set; }
        public List<string> Roles { get; set; } = new List<string>();
    }

    public class AdminPaystackInitRequest
    {
        public string TargetUserId { get; set; }
        public decimal Amount { get; set; }
    }
}
