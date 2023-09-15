using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Dtos
{


    public class SmsResponse
    {
        public string status { get; set; }
        public string error_code { get; set; }
        public string cost { get; set; }
        public string[] data { get; set; }
        public string msg { get; set; }
        public int length { get; set; }
        public int page { get; set; }
        public string balance { get; set; }

        public string BalanceResponse { get; set; }

    }

    public class BalanceResponse
    {
        public string status { get; set; }
        public string error_code { get; set; }
        public string msg { get; set; }
    }


    public class GeneralResponse
    {
        public string status { get; set; }
        public string error_code { get; set; }
        public string msg { get; set; }
    }
}