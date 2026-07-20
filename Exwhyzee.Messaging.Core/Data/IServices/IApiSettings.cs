using Exwhyzee.Messaging.Core.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Core.Data.IServices
{
    public interface IApiSettings
    {
        Task<List<ApiSetting>> GetApis();

        Task AddApi(ApiSetting item);

        Task UpdateApi(ApiSetting item);

        Task<ApiSetting> GetApi(int? id);

        Task DeleteApi(ApiSetting item);
    }
}
