using System;
using System.ComponentModel.DataAnnotations;

namespace Exwhyzee.Messaging.Core.Models
{
    /// <summary>
    /// Represents an automated or manual outflow transfer executed via Paystack.
    /// Tracks live Paystack transfer status, recipient bank details, and linked customer funding deposits.
    /// </summary>
    public class PaystackTransferOutflow
    {
        public int Id { get; set; }

        [MaxLength(100)]
        [Display(Name = "Transfer Code")]
        public string TransferCode { get; set; } // e.g. TRF_43CFM00JDEZ547AG

        [MaxLength(100)]
        public string TransferReference { get; set; }

        [Display(Name = "Amount (NGN)")]
        public decimal Amount { get; set; }

        [MaxLength(100)]
        public string RecipientCode { get; set; }

        [MaxLength(200)]
        public string AccountName { get; set; }

        [MaxLength(50)]
        public string AccountNumber { get; set; }

        [MaxLength(100)]
        public string BankName { get; set; }

        [MaxLength(50)]
        public string Status { get; set; } // Pending, Success, Failed, Reversed, Otp, PendingApproval

        [MaxLength(200)]
        public string InitiatedBy { get; set; }

        public DateTime DateInitiated { get; set; }

        public DateTime? DateVerified { get; set; }

        [MaxLength(200)]
        public string VerifiedBy { get; set; }

        public string PaystackMessage { get; set; }

        public bool IsRolledBack { get; set; }

        public DateTime? DateRolledBack { get; set; }

        [MaxLength(200)]
        public string RolledBackBy { get; set; }

        /// <summary>
        /// Comma-separated or JSON list of customer Transaction IDs covered in this transfer batch.
        /// </summary>
        public string TransactionIdsJson { get; set; }

        public int TransactionsCount { get; set; }
    }
}
