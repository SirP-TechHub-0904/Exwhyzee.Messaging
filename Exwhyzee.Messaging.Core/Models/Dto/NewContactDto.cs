using System;
using System.Collections.Generic;
using System.Linq;


namespace Exwhyzee.Messaging.Core.Models.Dto
{
    public class NewContactDto
    {
        public string PhoneNumber { get; set; }
        public int? GroupId { get; set; }
        public string Surname { get; set; }
        public string Othernames { get; set; }
        public DateTime DateAddded { get; set; }
        public DateTime? DateOfBirth { get; set; }
        public string Note { get; set; }
    }
}

