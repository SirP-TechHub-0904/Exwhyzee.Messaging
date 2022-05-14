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
    public class PriceSettingService : IPriceSettingsService
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        public async Task AddDialCode(DialCode item)
        {
            db.DialCodes.Add(item);
            await db.SaveChangesAsync();
        }

        public async Task AddPriceSetting(PriceSetting item)
        {
            db.PriceSettings.Add(item);
            await db.SaveChangesAsync();
        }

        public async Task DeleteDialCode(DialCode item)
        {
            db.DialCodes.Remove(item);
            await db.SaveChangesAsync();
        }

        public async Task DeletePriceSettings(PriceSetting item)
        {
            db.PriceSettings.Remove(item);
            await db.SaveChangesAsync();
        }

        public async Task<DialCode> GetDialCode(int? id)
        {
            var dialCode = await db.DialCodes.FindAsync(id);
            return dialCode;
        }

        public async Task<List<DialCode>> GetDialCodes()
        {
            var dialCodes = db.DialCodes.Include(x => x.PriceSetting);
            return await dialCodes.ToListAsync();
        }

        public async Task<PriceSetting> GetPriceSetting(int? id)
        {
            var priceSetting = await db.PriceSettings.FindAsync(id);
            return priceSetting;
        }

        public async Task<List<PriceSetting>> GetPriceSettings()
        {
            var priceLists = db.PriceSettings.Include(x => x.DialCodes).OrderBy(x => x.Country).ThenBy(x => x.NetworkProvider);
            return await priceLists.ToListAsync();
        }

        public async Task UpdateDialCode(DialCode item)
        {
            db.Entry(item).State = EntityState.Modified;
            await db.SaveChangesAsync();
        }

        public async Task UpdatePriceSetting(PriceSetting item)
        {
            db.Entry(item).State = EntityState.Modified;
            await db.SaveChangesAsync();
        }
    }
}