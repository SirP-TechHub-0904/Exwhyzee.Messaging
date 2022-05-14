using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
{
    public class DialCode
    {
        public int DialCodeId { get; set; }
        public string NumberPrefix { get; set; }
        public int PriceSettingId { get; set; }

        public PriceSetting PriceSetting { get; set; }
    }
}