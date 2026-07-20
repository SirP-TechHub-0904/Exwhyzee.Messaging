using System;
using System.Collections.Generic;
using System.Linq;


namespace Exwhyzee.Messaging.Core.Models
{
    public class MessageChunk
    {
        public int MessageChunkId { get; set; }
        public int MessageId { get; set; }
        public string Numbers { get; set; }
        public string Response { get; set; }
    }
}

