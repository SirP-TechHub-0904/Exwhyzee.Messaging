using System;
using System.Collections.Generic;
using System.Linq;


namespace Exwhyzee.Messaging.Core.Models
{
    public class Voucher
    {
        public Voucher()
        {
            Status = VoucherStatus.UnUsed;
        }

        public int VoucherId { get; set; }
        public string UserId { get; set; }
        public int BatchVoucherId { get; set; }
        public string Code { get; set; }
        public DateTime? DateUsed { get; set; }
        public decimal Units { get; set; }
        public VoucherStatus Status { get; set; }

        public BatchVoucher BactchVoucher { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("UserId")]
        public ApplicationUser User { get; set; }
    }
}

