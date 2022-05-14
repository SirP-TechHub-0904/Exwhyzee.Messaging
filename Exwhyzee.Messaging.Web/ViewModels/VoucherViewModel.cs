using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.ViewModels
{
    public class VoucherViewModel
    {
        public object ObjectLength { get; set; }
        public int Quantity { get; set; }
        public decimal Units { get; set; }
        public string BatchNumber { get; set; }
    }
}