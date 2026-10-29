using System;

namespace Exwhyzee.Messaging.Core.Dtos
{
    public class EmailLogItemDto
    {
        public int Id { get; set; }
        public string RecipientEmail { get; set; }
        public string Subject { get; set; }
        public DateTime DateSent { get; set; }
        public bool IsSuccess { get; set; }
        public string ErrorMessage { get; set; }
        public string Category { get; set; }

        // Matched platform user info (if recipient email belongs to a registered user)
        public string MatchedUserId { get; set; }
        public string MatchedUserName { get; set; }
        public string MatchedFullName { get; set; }
    }

    public class EmailLogStatsDto
    {
        public int TotalFiltered { get; set; }
        public int DeliveredCount { get; set; }
        public int FailedCount { get; set; }
        public decimal DeliveryRatePercent { get; set; }
        public int SentTodayCount { get; set; }
        public int OtpSecurityCount { get; set; }
        public int PaymentReceiptCount { get; set; }
        public int WelcomeNoticeCount { get; set; }
        public int UniqueRecipientsCount { get; set; }
    }
}

