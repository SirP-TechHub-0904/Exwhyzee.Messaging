using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
{
    public class BatchVoucher
    {
        public BatchVoucher()
        {
            BatchNumber = DateTime.UtcNow.AddHours(1).ToString();
        }

        public int BatchVoucherId { get; set; }
        public string UserId { get; set; }
        public ApplicationUser User { get; set; }
        public DateTime DateGenerated { get; set; }
        public string BatchNumber { get; set; }
        public int Quantity { get; set; }

        public virtual ICollection<Voucher> Vouchers { get; set; }
    }
}