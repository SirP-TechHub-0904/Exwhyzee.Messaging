using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
{
    public class BankDetail
    {
        public BankDetail()
        {
            Active = true;
        }

        public int BankDetailId { get; set; }
        public string BankName { get; set; }
        public string AccountName { get; set; }
        public string AccountNumber { get; set; }
        public bool Active { get; set; }
    }
}