using AutoMapper;
using Exwhyzee.Messaging.Core.Dtos;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Exwhyzee.Messaging.Core.Models
{
    public class AutoMapperProfile:Profile
    {
        public AutoMapperProfile()
        {
            CreateMap<Group, GroupDto>();
        }
    }
}

