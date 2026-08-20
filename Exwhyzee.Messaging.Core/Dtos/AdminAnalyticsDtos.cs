using System;

namespace Exwhyzee.Messaging.Core.Dtos
{
    public class TopUserUsageDto
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public int MessagesCount { get; set; }
        public decimal UnitsBurned { get; set; }
        public double DeliveryRate { get; set; }
    }

    public class TopFunderDto
    {
        public int ClientId { get; set; }
        public string UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public decimal TotalAmountPaid { get; set; }
        public decimal TotalUnitsBought { get; set; }
        public int TransactionCount { get; set; }
        public DateTime? LastFundingDate { get; set; }
    }

    public class HeavyBlastSenderDto
    {
        public string UserId { get; set; }
        public string Username { get; set; }
        public string FullName { get; set; }
        public int MaxRecipientsInSingleMsg { get; set; }
        public int TotalRecipientsReached { get; set; }
        public int TotalBroadcasts { get; set; }
        public DateTime? LastBroadcastDate { get; set; }
    }

    public class PaystackLiquidityDto
    {
        public decimal TotalPaystackRevenue { get; set; }
        public int TotalApprovedPaystackTxns { get; set; }
        public decimal TotalUnitsCreditedViaPaystack { get; set; }
        public decimal TotalPendingTransferAmount { get; set; }
        public decimal TotalAdminTransferredAmount { get; set; }
        public int PendingTransfersCount { get; set; }
        public int PendingPaystackTxns { get; set; }
        public string AppTag { get; set; } = "Exwhyzee.Messaging";
    }
}
