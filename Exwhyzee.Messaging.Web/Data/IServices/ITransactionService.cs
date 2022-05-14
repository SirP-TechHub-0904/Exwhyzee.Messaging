using Exwhyzee.Messaging.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Web.Data.IServices
{
    public interface ITransactionService
    {
        Task<List<Transaction>> GetTransactions();

        Task<Transaction> GetTransaction(int? id);

        Task<List<Transaction>> GetTransactionsByClient(int? id);

        Task<decimal> TotalUnitPurchasedByClient(int? id);

        Task<decimal> TotalAmountPaidByClient(int? id);

        Task<decimal> TotalAmountOfUnitsByClient(int? id);

        Task UpdateTransactionStatus(Transaction item);

        Task AddTransaction(Transaction item);
    }
}