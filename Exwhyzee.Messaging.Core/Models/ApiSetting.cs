using System;
using System.Collections.Generic;
using System.Linq;


namespace Exwhyzee.Messaging.Core.Models
{
    public class ApiSetting
    {
        public ApiSetting()
        {
            IsDefault = false;
        }

        public int ApiSettingId { get; set; }
        public string Name { get; set; }
        public string Token { get; set; }
        public string Sending { get; set; }
        public string CheckBalance { get; set; }
        public bool IsDefault { get; set; }
    }
}

