using AutoMapper.Attributes;
using Exwhyzee.Messaging.Web.Models;
using System;

namespace Exwhyzee.Messaging.Web.Dtos
{
    
    public class GroupDto
    {
        public int GroupId { get; set; }
        public string UserId { get; set; }
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime DateCreated { get; set; }
        public bool? SendBirthDayMessages { get; set; }
        public int Count { get; set; }
    }
}