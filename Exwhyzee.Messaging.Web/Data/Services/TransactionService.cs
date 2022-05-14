using Exwhyzee.Messaging.Web.Data.IServices;
using Exwhyzee.Messaging.Web.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Web.Data.Services
{
    public class TransactionService : ITransactionService
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        public async Task AddTransaction(Transaction item)
        {
            db.Transactions.Add(item);
            await db.SaveChangesAsync();
        }

        public async Task<Transaction> GetTransaction(int? id)
        {
            var tranx = await db.Transactions.Include(transaction => transaction.User).FirstOrDefaultAsync(x => x.TransactionId == id);
            return tranx;
        }

        public async Task<List<Transaction>> GetTransactions()
        {
            return await db.Transactions.Include(t => t.User).ToListAsync();
        }

        public async Task<List<Transaction>> GetTransactionsByClient(int? id)
        {
            var tranx = db.Transactions.Include(transaction => transaction.User).Where(x => x.ClientId == id);
            return await tranx.ToListAsync();
        }

        public async Task<decimal> TotalAmountOfUnitsByClient(int? id)
        {
            var tranx = db.Transactions.Where(x => x.ClientId == id);

            return await tranx.SumAsync(x => x.Amount);
        }

        public async Task<decimal> TotalAmountPaidByClient(int? id)
        {
            var tranx = db.Transactions.Where(x => x.ClientId == id);

            return await tranx.SumAsync(x => x.AmountPaid.Value);
        }

        public async Task<decimal> TotalUnitPurchasedByClient(int? id)
        {
            var tranx = db.Transactions.Where(x => x.ClientId == id);

            return await tranx.SumAsync(x => x.Units);
        }

        public async Task UpdateTransactionStatus(Transaction item)
        {
            db.Entry(item).State = EntityState.Modified;
            await db.SaveChangesAsync();
        }
    }
}