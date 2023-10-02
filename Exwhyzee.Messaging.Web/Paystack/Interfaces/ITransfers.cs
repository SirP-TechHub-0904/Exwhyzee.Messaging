
using Exwhyzee.Messaging.Web.PayStack.Models.Transfers.Initiation;
using Exwhyzee.Messaging.Web.PayStack.Models.Transfers.Recipient;
using Exwhyzee.Messaging.Web.PayStack.Models.Transfers.TransferDetails;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Web.PayStack
{
    public interface ITransfers
    {
        Task<TransferRecipientModel> CreateTransferRecipient(string type,string name,string account_number,
                                        string bank_code,string currency = "NGN",string description =null);

        Task<TransferRecipientListModel> ListTransferRecipients();

        Task<TransferInitiationModel> InitiateTransfer(int amount, string recipient_code, string source = "balance", string currency = "NGN", string reason = null);

        Task<TransferDetailsModel> FetchTransfer(string transfer_code);

        Task<TransferDetailsListModel> ListTransfers();

        Task<string> FinalizeTransfer(string transfer_code, string otp);
    }
}
