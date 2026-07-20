using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Models;
using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;


namespace Exwhyzee.Messaging.Core.Data.Services
{
    public class AdminSettings : IAdminSettings
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        public async Task UpdateClient(AdminSetting item)
        {
            db.Entry(item).State = EntityState.Modified;
            await db.SaveChangesAsync();
        }
    }
}


