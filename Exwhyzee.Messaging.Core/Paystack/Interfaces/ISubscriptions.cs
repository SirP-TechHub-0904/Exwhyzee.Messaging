using Exwhyzee.Messaging.Core.Paystack.Models.Subscription;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Core.Paystack
{
    public interface ISubscriptions
    {
        Task<SubscriptionModel> CreateSubscription(string customerEmail, string planCode, string authorization,string start_date);
    }
}

