using System;
using System.Collections.Generic;
using System.Linq;


namespace Exwhyzee.Messaging.Core.Models
{
    public class DialCode
    {
        public int DialCodeId { get; set; }
        public string NumberPrefix { get; set; }
        public int PriceSettingId { get; set; }

        public PriceSetting PriceSetting { get; set; }
    }
}

