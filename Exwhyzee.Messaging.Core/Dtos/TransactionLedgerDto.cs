using System;
using Exwhyzee.Messaging.Core.Models;

namespace Exwhyzee.Messaging.Core.Dtos
{
    public class TransactionLedgerDto
    {
        public int TransactionId { get; set; }
        public int ClientId { get; set; }
        public string UserId { get; set; }
        public string ClientFullName { get; set; }
        public string Username { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public decimal Units { get; set; }
        public decimal Amount { get; set; }
        public decimal EffectiveAmountPaid { get; set; }
        public TransactionType TransactionType { get; set; }
        public string PaymentSource { get; set; }
        public string SourceCategory { get; set; } // "Paystack", "BankDeposit", "AdminTopUp", "Voucher"
        public TransactionStatus Status { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateApproved { get; set; }
        public DateTime EffectiveDate => DateApproved ?? DateCreated;
        public string ApprovedBy { get; set; }
        public string TransactionReference { get; set; }
        public string GatewayResponse { get; set; }
        public string Note { get; set; }
    }

    public class TransactionLedgerStatsDto
    {
        public int TotalTransactions { get; set; }
        public int ApprovedCount { get; set; }
        public decimal ApprovedRevenueNgn { get; set; }
        public decimal ApprovedUnitsCredited { get; set; }
        public int PendingCount { get; set; }
        public decimal PendingAmountNgn { get; set; }
        public int FailedOrCancelledCount { get; set; }
        public int PaystackCount { get; set; }
        public decimal PaystackRevenueNgn { get; set; }
        public int BankDepositCount { get; set; }
        public decimal BankDepositRevenueNgn { get; set; }
        public int AdminTopUpCount { get; set; }
        public decimal AdminTopUpUnits { get; set; }
    }
}

