using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Core.Services
{
    public interface ILowUnitAlertService
    {
        Task CheckAndTriggerLowUnitAlertAsync(string userId, decimal currentUnits);
        Task RunDailyMorningLowUnitCheckAsync();
    }
}
