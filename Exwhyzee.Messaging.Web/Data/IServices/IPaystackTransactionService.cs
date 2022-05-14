
using System;
using System.Net.Http;
using System.Collections.Generic;
using System.Text;
using System.Threading.Tasks;
using Exwhyzee.Messaging.Web.Dtos.Paystack;

namespace Exwhyzee.Messaging.Web.Data.Services
{
    public interface IPaystackTransactionService
    {
        Task<PaymentInitalizationResponse> InitializeTransaction(string secretKey,string email, int amount, long transactionId, string firstName = null,
            string lastName = null, string callbackUrl = null, string reference = null, bool makeReferenceUnique = false);

        Task<TransactionResponseModel> VerifyTransaction(string reference, string secretKey);

        // HttpClient CreateClient(string secretKey);
    }
}
