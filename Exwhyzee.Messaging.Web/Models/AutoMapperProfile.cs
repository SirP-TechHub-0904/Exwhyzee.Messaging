using AutoMapper;
using Exwhyzee.Messaging.Web.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace Exwhyzee.Messaging.Web.Models
{
    public class AutoMapperProfile:Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Group, GroupDto>();
        }
    }
}