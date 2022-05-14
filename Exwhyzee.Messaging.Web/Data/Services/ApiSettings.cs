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
    public class ApiSettings : IApiSettings
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        public async Task AddApi(ApiSetting item)
        {
            db.ApiSettings.Add(item);
            await db.SaveChangesAsync();
        }

        public async Task DeleteApi(ApiSetting item)
        {
            db.ApiSettings.Remove(item);
            await db.SaveChangesAsync();
        }

        public async Task<ApiSetting> GetApi(int? id)
        {
            var api = await db.ApiSettings.FindAsync(id);
            return api;
        }

        public async Task<List<ApiSetting>> GetApis()
        {
            var apis = await db.ApiSettings.ToListAsync();
            return apis;
        }

        public async Task UpdateApi(ApiSetting item)
        {
            db.Entry(item).State = EntityState.Modified;
            await db.SaveChangesAsync();
        }
    }
}