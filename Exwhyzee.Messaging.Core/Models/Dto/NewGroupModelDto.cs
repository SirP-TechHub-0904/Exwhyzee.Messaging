using System;
using System.Collections.Generic;
using System.Linq;


namespace Exwhyzee.Messaging.Core.Models.Dto
{
    public class NewGroupModelDto
    
    {
        public int GroupId { get; set; }
        public string Username { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime DateCreated { get; set; }
        public bool? SendBirthDayMessages { get; set; }

        public string SenderId { get; set; }

        public string Message { get; set; }
    }
}

