using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
{
    public class XyzSenderID
    {
        public int Id { get;set;}
        public string SenderId { get;set; }
         public int ClientId { get; set; }
        public Client Client { get;set;}

        public string XYZ_status { get; set; }
        public string XYZ_error_code { get; set; }
        public string XYZ_msg { get; set; }


        public string Verify_msg { get; set; }
    }
}