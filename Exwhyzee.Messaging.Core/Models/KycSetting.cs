using System;
using System.ComponentModel.DataAnnotations;

namespace Exwhyzee.Messaging.Core.Models
{
    public class KycSetting
    {
        [Key]
        public int Id { get; set; }

        public bool IsKycEnabled { get; set; } = true;

        /// <summary>
        /// "Either" (SMS Sent OR Units Funded), "SmsOnly", "UnitsOnly", "Both", "ManualOnly"
        /// </summary>
        public string TriggerRule { get; set; } = "Either";

        /// <summary>
        /// Total SMS recipients / messages sent that triggers mandatory KYC upgrade
        /// </summary>
        public int SmsSentThreshold { get; set; } = 1000;

        /// <summary>
        /// Cumulative approved units funded that triggers mandatory KYC upgrade
        /// </summary>
        public decimal UnitsFundedThreshold { get; set; } = 10000m;

        /// <summary>
        /// Single transaction unit top-up that immediately triggers KYC upgrade (0 = disabled)
        /// </summary>
        public decimal SingleTopUpUnitsThreshold { get; set; } = 5000m;

        /// <summary>
        /// If true, users whose KYC is Triggered or Rejected are blocked from sending new SMS until they submit/complete KYC
        /// </summary>
        public bool EnforceBlockOnCompose { get; set; } = false;

        /// <summary>
        /// If true, users whose KYC is Triggered or Rejected are blocked from funding above threshold until they submit/complete KYC
        /// </summary>
        public bool EnforceBlockOnFunding { get; set; } = false;

        public bool RequirePhoneVerification { get; set; } = true;
        public bool RequireAddress { get; set; } = true;
        public bool RequirePassportPhoto { get; set; } = true;
        public bool RequireGovernmentId { get; set; } = true;

        /// <summary>
        /// Comma-separated list of accepted ID types: NIN,VotersCard,DriversLicense,InternationalPassport,CAC
        /// </summary>
        public string AcceptedIdTypes { get; set; } = "NIN,VotersCard,DriversLicense,InternationalPassport,CAC";

        public string LastUpdatedBy { get; set; }
        public DateTime LastUpdatedDate { get; set; } = DateTime.UtcNow;
    }
}

