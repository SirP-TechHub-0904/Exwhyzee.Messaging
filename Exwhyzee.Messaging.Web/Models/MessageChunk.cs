using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
{
    public class MessageChunk
    {
        public int MessageChunkId { get; set; }
        public int MessageId { get; set; }
        public string Numbers { get; set; }
        public string Response { get; set; }
    }
}