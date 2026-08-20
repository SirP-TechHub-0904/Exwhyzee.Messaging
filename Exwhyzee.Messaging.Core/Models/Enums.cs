using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;

namespace Exwhyzee.Messaging.Core.Models
{
    public enum AllowNotifications
    {
        Allow = 1,

        [Description("Disallow")]
        DisAllow = 2
    }

    public enum TransactionStatus
    {
        Pending = 1,
        Approved = 2,
        Cancelled = 3,
        Failed = 4,
        Revoked = 5
    }

    public enum MessageStatus
    {
        Pending = 1,
        Failed = 2,
        Sent = 3,
        Draft = 4,
        Scheduled = 5,
        Revoked = 6
    }

    public enum VoucherStatus
    {
        UnUsed = 1,
        Used = 2,
        Blocked = 3
    }

    public enum TransactionType
    {
        None = 0,

        [Description("Online Payment")]
        OnlinePayment = 1,

        [Description("Bank Deposit")]
        BankDeposit = 2,

        [Description("By Administrator")]
        ByAdmin = 3,

        [Description("Voucher")]
        ByVoucher = 4
    }

    public enum SendingOption
    {
        SendNow = 1,

        SendLater = 2,

        SaveAsDraft = 3
    }

    public enum TwoFactorMethod
    {
        None = 0,
        GoogleAuth = 1,
        MicrosoftAuth = 2,
        SmsOtp = 3,
        EmailOtp = 4
    }

    public enum TicketStatus
    {
        [Description("Open")]
        Open = 1,

        [Description("In Progress")]
        InProgress = 2,

        [Description("Resolved")]
        Resolved = 3,

        [Description("Closed")]
        Closed = 4
    }

    public enum TicketPriority
    {
        Low = 1,
        Medium = 2,
        High = 3,
        Urgent = 4
    }

    public enum TicketCategory
    {
        [Description("General Inquiry")]
        General = 1,

        [Description("Billing & Paystack")]
        Billing = 2,

        [Description("API Integration")]
        ApiIntegration = 3,

        [Description("Account Lockout / Suspension")]
        AccountSuspended = 4,

        [Description("SMS Delivery Issue")]
        SmsDelivery = 5
    }
}
