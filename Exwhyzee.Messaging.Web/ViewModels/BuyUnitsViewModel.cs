using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.ViewModels
{
    public class BuyUnitsViewModel
    {
        [DisplayName("Price Per Unit")]
        public decimal PricePerUnit { get; set; }
        public int Units { get; set; }
        [DisplayName("Payment Method")]
        public string PaymentMethod { get; set; }
    }
}