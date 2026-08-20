using System;
using System.Collections.Generic;
using System.Linq;


namespace Exwhyzee.Messaging.Core.Dtos
{
    public class ApiBalanceSecondDto
    {
        public string Name { get; set; }
        public string Balance { get; set; }
        public bool IsSuccess { get; set; }
        public string StatusMessage { get; set; }
        public string RouteType { get; set; } = "Transactional";
    }
}

