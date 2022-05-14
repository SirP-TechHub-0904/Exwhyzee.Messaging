using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models.Dto
{
    public class TransactionDto
    {
        public int TransactionId { get; set; }
        public string UserId { get; set; }
        public int ClientId { get; set; }
        public decimal Amount { get; set; }
        public decimal? AmountPaid { get; set; }
        public decimal Units { get; set; }
        public TransactionType TransactionType { get; set; }
        public string GatewayResponse { get; set; }
        public DateTime DateCreated { get; set; }
        public DateTime? DateApproved { get; set; }
        public TransactionStatus Status { get; set; }
        public string Note { get; set; }
       // public ApplicationUser User { get; set; }
    }
}