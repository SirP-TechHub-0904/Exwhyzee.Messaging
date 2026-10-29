using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Exwhyzee.Messaging.Core.Models
{
    public enum KycVerificationStatus
    {
        Unverified = 0,
        Triggered = 1,
        PendingReview = 2,
        Approved = 3,
        Rejected = 4,
        Exempted = 5
    }

    public class UserKycProfile
    {
        [Key]
        public int Id { get; set; }

        public string UserId { get; set; }
        public int? ClientId { get; set; }

        // Residential / Office Address
        public string ResidentialAddress { get; set; }
        public string City { get; set; }
        public string State { get; set; }
        public string Country { get; set; } = "Nigeria";

        // Phone Verification
        public bool IsPhoneVerified { get; set; }
        public DateTime? PhoneVerifiedDate { get; set; }
        public string PhoneOtpCode { get; set; }
        public DateTime? PhoneOtpExpiry { get; set; }

        // Passport Photograph
        public string PassportPhotoPath { get; set; }

        // Identity Document (NIN, Voter's Card, Driver's License, Int'l Passport, CAC)
        public string IdType { get; set; }
        public string IdNumber { get; set; }
        public string IdDocumentFrontPath { get; set; }
        public string IdDocumentBackPath { get; set; }

        // Lifecycle & Audit
        public KycVerificationStatus KycStatus { get; set; } = KycVerificationStatus.Unverified;
        public string TriggerReason { get; set; }
        public string AdminReviewNote { get; set; }

        public DateTime? TriggeredDate { get; set; }
        public DateTime? SubmittedDate { get; set; }
        public DateTime? ReviewedDate { get; set; }
        public string ReviewedBy { get; set; }
        public DateTime LastUpdated { get; set; } = DateTime.UtcNow;
    }
}

