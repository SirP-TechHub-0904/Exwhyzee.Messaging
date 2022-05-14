using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models.Dto
{
    public class GroupModelDto
    {
        public int GroupId { get; set; }
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime DateCreated { get; set; }
        public bool? SendBirthDayMessages { get; set; }
        public int? GpId { get; set; }


        public string SenderId { get; set; }

        public string Message { get; set; }
    }
}