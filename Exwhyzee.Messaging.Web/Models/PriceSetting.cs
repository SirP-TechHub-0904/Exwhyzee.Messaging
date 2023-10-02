using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
{
    public class PriceSetting
    {
        [DisplayName("Price Setting")]
        public int PriceSettingId { get; set; }
        public string Country { get; set; }
        public string NetworkProvider { get; set; }
        public int DigitCount { get; set; }
        public decimal UnitsPerSms { get; set; }
        public string InternationalDialCode { get; set; }

        public virtual ICollection<DialCode> DialCodes { get; set; }
    }
}