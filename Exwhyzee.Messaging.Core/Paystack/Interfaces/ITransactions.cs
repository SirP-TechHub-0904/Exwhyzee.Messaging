
using Exwhyzee.Messaging.Core.Paystack.Models;
using Exwhyzee.Messaging.Core.Paystack.Models.Authorizations;
using Exwhyzee.Messaging.Core.Paystack.Models.Exports;
using Exwhyzee.Messaging.Core.Paystack.Models.TransTotal;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Core.Paystack
{
    public interface ITransactions
    {
        Task<PaymentInitalizationResponseModel> InitializeTransaction(string email, int amount,string firstName = null, 
            string lastName = null, string callbackUrl =null, string reference = null, bool makeReferenceUnique = false);

        Task<PaymentInitalizationResponseModel> InitializeTransaction(TransactionInitializationRequestModel requestObj);

        Task<TransactionResponseModel> VerifyTransaction(string reference);

        Task<TransactionListViewModel> ListTransactions();

        Task<TransactionResponseModel> FetchTransaction(int id);

        Task<TransactionResponseModel> ChargeAuthorization(string authorization_code, string email, int amount);

        Task<TransactionResponseModel> TransactionTimeline(string reference);

        Task<TransactionTotal> TransactionTotals();

        Task<ExportResponseModel> ExportTransactions();

        Task<TransactionResponseModel> CheckAuthorization(string authorization_code, string email, int amount);

        Task<RequestAuthorizationModel> RequestReAuthorization(string authorization_code, string email, int amount);

    }
}

