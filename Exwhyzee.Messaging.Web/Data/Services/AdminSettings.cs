using Exwhyzee.Messaging.Web.Data.IServices;
using Exwhyzee.Messaging.Web.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Exwhyzee.Messaging.Web.Data.Services
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