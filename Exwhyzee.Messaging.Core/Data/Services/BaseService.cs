using AutoMapper;
using System;
using System.Collections.Generic;
using System.Linq;


namespace Exwhyzee.Messaging.Core.Data.Services
{
    public class BaseService
    {
        private readonly IMapper mapper;

        public BaseService(IMapper mapper)
        {
            this.mapper = mapper;
        }
    }
}

