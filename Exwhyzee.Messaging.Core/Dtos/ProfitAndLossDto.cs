using System;
using Exwhyzee.Messaging.Core.Models;

namespace Exwhyzee.Messaging.Core.Dtos
{
    public class ProfitAndLossItemDto
    {
        public int MessageId { get; set; }
        public string UserId { get; set; }
        public int ClientId { get; set; }
        public string Username { get; set; }
        public string ClientFullName { get; set; }
        public string Email { get; set; }
        public string SenderId { get; set; }
        public int RecipientsCount { get; set; }
        public int PageCount { get; set; }
        public string MessagePreview { get; set; }
        public DateTime SentDate { get; set; }
        public MessageStatus Status { get; set; }

        // Our End: Message.UnitsUsed deducted from client wallet
        public decimal ClientUnitsCharged { get; set; }

        // Kudi End: Message.Response_cost deducted from our Kudi gateway balance
        public decimal GatewayCostUnits { get; set; }
        public bool HasGatewayCost { get; set; }

        // Net Spread = Our End (UnitsUsed) - Kudi End (Response_cost)
        public decimal NetProfitUnits => Math.Round(ClientUnitsCharged - GatewayCostUnits, 2);
        public decimal MarginPercent => ClientUnitsCharged > 0
            ? Math.Round(((ClientUnitsCharged - GatewayCostUnits) / ClientUnitsCharged) * 100m, 1)
            : 0m;

        public decimal? GatewayBalanceAfter { get; set; }
        public string RawGatewayBalance { get; set; }
        public string ResponseStatus { get; set; }
    }

    public class ProfitAndLossStatsDto
    {
        public int TotalBroadcasts { get; set; }
        public int TotalRecipients { get; set; }
        public decimal TotalClientUnitsCharged { get; set; }
        public decimal TotalGatewayCostUnits { get; set; }
        public decimal TotalNetProfitUnits => Math.Round(TotalClientUnitsCharged - TotalGatewayCostUnits, 2);
        public decimal OverallMarginPercent => TotalClientUnitsCharged > 0
            ? Math.Round(((TotalClientUnitsCharged - TotalGatewayCostUnits) / TotalClientUnitsCharged) * 100m, 1)
            : 0m;

        public int ProfitableBroadcastsCount { get; set; }
        public decimal ProfitableUnitsSum { get; set; }

        public int BreakEvenBroadcastsCount { get; set; }
        public decimal BreakEvenUnitsVolume { get; set; }

        public int LossOrBreakEvenBroadcastsCount { get; set; }
        public int LossBroadcastsCount { get; set; }
        public decimal LossUnitsSum { get; set; }

        public string LatestGatewayBalance { get; set; }
        public decimal PricePerUnit { get; set; } = 1.0m;
        public decimal FlatUnitsPerSms { get; set; } = 6.95m;
    }
}
