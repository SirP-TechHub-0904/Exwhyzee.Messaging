using AutoMapper.Attributes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
{
    [MapsTo(typeof(Group))]
    public class Group
    {
        public Group()
        {
            DateCreated = DateTime.UtcNow;
            SendBirthDayMessages = false;
        }

        public int GroupId { get; set; }
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime DateCreated { get; set; }
        public bool? SendBirthDayMessages { get; set; }
        public int? GpId { get; set; }

        [MaxLength(11)]
        public string SenderId { get; set; }

        public string Message { get; set; }

        public virtual ICollection<Contact> Contacts { get; set; }
    }
}