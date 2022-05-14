using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
{
    public class ApiSetting
    {
        public ApiSetting()
        {
            IsDefault = false;
        }

        public int ApiSettingId { get; set; }
        public string Name { get; set; }
        public string Sending { get; set; }
        public string CheckBalance { get; set; }
        public bool IsDefault { get; set; }
    }
}