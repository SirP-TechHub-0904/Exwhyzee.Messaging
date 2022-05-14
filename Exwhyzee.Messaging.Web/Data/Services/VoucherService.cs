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
    public class VoucherService : IVoucherService
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        private static Random rng = new Random(Environment.TickCount);

        public async Task GenerateVouchers(object objectLenght, int quantityOfVouchers, decimal units, string batchNumber, string userId)
        {
            BatchVoucher batchVoucher = new BatchVoucher();
            batchVoucher.BatchNumber = batchNumber;
            batchVoucher.DateGenerated = DateTime.UtcNow;
            batchVoucher.Quantity = quantityOfVouchers;
            batchVoucher.UserId = userId;

            db.BatchVouchers.Add(batchVoucher);

            for (int index = 0; index < quantityOfVouchers; index++)
            {
                Voucher voucher = new Voucher();
                int lenght = Convert.ToInt32(objectLenght);
                var number = rng.NextDouble().ToString("0.000000000000").Substring(2, lenght);
                voucher.Code = number.ToString().ToUpper();
                voucher.Units = units;
                voucher.BatchVoucherId = batchVoucher.BatchVoucherId;

                db.Vouchers.Add(voucher);
            }

            await db.SaveChangesAsync();
        }

        public async Task<Voucher> GetVoucher(int? id)
        {
            var voucher = await db.Vouchers.FindAsync(id);
            return voucher;
        }

        public async Task<List<Voucher>> GetVouchers()
        {
            var vouchers = db.Vouchers.Include(v => v.BactchVoucher).Include(x => x.BactchVoucher.User);
            return await vouchers.ToListAsync();
        }

        public async Task<List<BatchVoucher>> GetBatchVouchers()
        {
            var batches = db.BatchVouchers.Include(v => v.Vouchers).Include(x => x.User);
            return await batches.ToListAsync();
        }

        public async Task<List<Voucher>> GetVouchersInBatch(int? id)
        {
            var voucherInBatches = db.Vouchers.Include(v => v.BactchVoucher).Include(x => x.BactchVoucher.User).Where(x => x.BatchVoucherId == id);
            return await voucherInBatches.ToListAsync();
        }
    }
}