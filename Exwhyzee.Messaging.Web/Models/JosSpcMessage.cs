using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
{
    public class JosSpcMessage
    {
        public int msgID { get; set; }
        public int msgClientID { get; set; }
        public string senderID { get; set; }
        public string recipients { get; set; }
        public string message { get; set; }
        public string apiResponse { get; set; }
        public string schedule { get; set; }
        public string delivered { get; set; }
        public string msgStatus { get; set; }
        public decimal unitsUsed { get; set; }
        public string smsCreated { get; set; }
        public string channel { get; set; }
        public string delivery_report { get; set; }
        public string dlvrID { get; set; }
        public string apiDlvrID { get; set; }
      
    }
}