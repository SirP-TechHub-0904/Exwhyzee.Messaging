using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Dtos
{
    public class SendMessageResponseDto
    {
        public bool Success { get; set; }

        public string Message { get; set; }

        public string Description { get; set; }

        public string ResponseCode { get; set; }
    }
}