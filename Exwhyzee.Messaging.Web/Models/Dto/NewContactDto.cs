using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models.Dto
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