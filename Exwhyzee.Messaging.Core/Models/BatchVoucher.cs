using System;
using System.Collections.Generic;
using System.Linq;


namespace Exwhyzee.Messaging.Core.Models
{
    public class BatchVoucher
    {
        public BatchVoucher()
        {
            BatchNumber = DateTime.UtcNow.AddHours(1).ToString();
        }

        public int BatchVoucherId { get; set; }
        public string UserId { get; set; }
        [System.ComponentModel.DataAnnotations.Schema.ForeignKey("UserId")]
        public ApplicationUser User { get; set; }
        public DateTime DateGenerated { get; set; }
        public string BatchNumber { get; set; }
        public int Quantity { get; set; }

        public virtual ICollection<Voucher> Vouchers { get; set; }
    }
}

