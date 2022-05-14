using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Areas.Content.Models
{
    public enum Status
    {
        [Description("Published")]
        Published = 1,
        [Description("Deleted")]
        Deleted = 2,

    }
}