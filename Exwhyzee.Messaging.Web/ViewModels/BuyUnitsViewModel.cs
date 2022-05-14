using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.ViewModels
{
    public class BuyUnitsViewModel
    {
        public decimal PricePerUnit { get; set; }
        public int Units { get; set; }

        public string PaymentMethod { get; set; }
    }
}