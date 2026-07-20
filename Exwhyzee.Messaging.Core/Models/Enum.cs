using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Linq;


namespace Exwhyzee.Messaging.Core.Models
{
    public enum Status
    {
        [Description("Published")]
        Published = 1,
        [Description("Deleted")]
        Deleted = 2,

    }
}
