using Exwhyzee.Messaging.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Web.Data.IServices
{
    public interface IPriceSettingsService
    {
        Task<List<PriceSetting>> GetPriceSettings();

        Task<List<DialCode>> GetDialCodes();

        Task<PriceSetting> GetPriceSetting(int? id);

        Task<DialCode> GetDialCode(int? id);

        Task AddPriceSetting(PriceSetting item);

        Task AddDialCode(DialCode item);

        Task UpdatePriceSetting(PriceSetting item);

        Task UpdateDialCode(DialCode item);

        Task DeletePriceSettings(PriceSetting item);

        Task DeleteDialCode(DialCode item);
    }
}