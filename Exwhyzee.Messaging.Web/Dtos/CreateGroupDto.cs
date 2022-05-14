using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Dtos
{
    public class CreateGroupDto
    {
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
    }
}