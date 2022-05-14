using Exwhyzee.Messaging.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Web.Data.IServices
{
    public interface IVoucherService
    {
        Task<List<Voucher>> GetVouchers();

        Task GenerateVouchers(object objectLenght, int quantityOfVouchers, decimal units, string batchNumber, string userId);

        Task<Voucher> GetVoucher(int? id);

        Task<List<BatchVoucher>> GetBatchVouchers();

        Task<List<Voucher>> GetVouchersInBatch(int? id);
    }
}