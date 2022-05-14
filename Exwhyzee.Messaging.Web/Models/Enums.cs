using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
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
        Failed = 4
    }

    public enum MessageStatus
    {
        Pending = 1,
        Failed = 2,
        Sent = 3,
        Draft = 4,
        Scheduled = 5
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
}