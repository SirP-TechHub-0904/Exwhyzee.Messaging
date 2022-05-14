using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Dtos
{
    public class MessageChunkDto
    {
        public int MessageChunkId { get; set; }
        public int MessageId { get; set; }
        public string Numbers { get; set; }
        public string Response { get; set; }
        public int NumbersCount { get; set; }
    }
}