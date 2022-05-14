using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
{
    public class JosSpcClient
    {
        public int mID { get; set; }
        public int ClientId { get; set; }
        public string GSM { get; set; }
        public decimal startingUnits { get; set; }
        public decimal Units { get; set; }
        public string facebookID { get; set; }
        public int? sendDelivery { get; set; }
        public string specialCost { get; set; }
        public int? specialAPI { get; set; }
        public string signature { get; set; }
        public string pendingNotifications { get; set; }
        public string jobCategories { get; set; }
        public string extraNos { get; set; }
        public string clientStatus { get; set; }
        public string clientModified { get; set; }
        public string dob { get; set; }
        public string specialRoute { get; set; }
        public string specialBlockCountry { get; set; }
        public string resellerURL { get; set; }
        public string notifyMe { get; set; }
    }
}