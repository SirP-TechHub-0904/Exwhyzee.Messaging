using System;
using System.Collections.Generic;
using System.Linq;
using Exwhyzee.Messaging.Core.Models;

namespace Exwhyzee.Messaging.Core.Dtos
{
    public class UserManagementDto
    {
        public string UserId { get; set; }
        public int ClientId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public string FirstName { get; set; }
        public string Surname { get; set; }
        public string OtherNames { get; set; }
        public string Email { get; set; }
        public bool EmailConfirmed { get; set; }
        public string PhoneNumber { get; set; }
        public bool PhoneNumberConfirmed { get; set; }
        public string Gender { get; set; }
        public decimal UnitsBalance { get; set; }
        public DateTime DateRegistered { get; set; }
        public DateTime? LastTimeSent { get; set; }
        public DateTime? LastActivityDate { get; set; }
        public bool IsActiveWithinOneMonth { get; set; }
        public DateTime? LockoutEnd { get; set; }
        public bool LockoutEnabled { get; set; }
        public bool TwoFactorEnabled { get; set; }
        public List<string> Roles { get; set; } = new List<string>();

        // KYC & Volume Telemetry
        public KycVerificationStatus KycStatus { get; set; }
        public bool IsPhoneVerified { get; set; }
        public string IdType { get; set; }
        public string ResidentialAddress { get; set; }
        public int TotalSmsSent { get; set; }
        public decimal TotalUnitsFunded { get; set; }

        public bool IsAdmin => Roles != null && Roles.Any(r =>
            r.Equals("SuperAdmin", StringComparison.OrdinalIgnoreCase) ||
            r.Equals("Admin", StringComparison.OrdinalIgnoreCase));
    }

    public class UserManagementStatsDto
    {
        public int TotalUsers { get; set; }
        public int ActiveUnderOneMonth { get; set; }
        public int ActiveOverOneMonth { get; set; }
        public int MaleUsers { get; set; }
        public int FemaleUsers { get; set; }
        public int UnspecifiedGenderUsers { get; set; }
        public int ClientsOnlyCount { get; set; }
        public int AdminUsersCount { get; set; }

        public decimal TotalUnitsBalance { get; set; }
        public int UsersUnder1kUnits { get; set; }
        public decimal UnitsSumUnder1k { get; set; }
        public int Users1kTo5kUnits { get; set; }
        public decimal UnitsSum1kTo5k { get; set; }
        public int UsersOver5kUnits { get; set; }
        public decimal UnitsSumOver5k { get; set; }
        public int UsersZeroUnits { get; set; }

        // KYC Stats
        public int KycApprovedUsers { get; set; }
        public int KycPendingReviewUsers { get; set; }
        public int KycTriggeredUsers { get; set; }
        public int PhoneVerifiedUsers { get; set; }
    }

    public class AdminPaystackInitRequest
    {
        public string TargetUserId { get; set; }
        public decimal Amount { get; set; }
    }
}
