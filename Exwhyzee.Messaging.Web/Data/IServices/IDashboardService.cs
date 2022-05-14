using Exwhyzee.Messaging.Web.Dtos;
using Exwhyzee.Messaging.Web.Models;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Web.Data.IServices
{
    public interface IDashboardService
    {
        Task<int> AllPendingTransactions();

        Task<int> ClientPendingTransactions(int? clientId);

        Task<int> TotalClients();

        Task<int> TotalClientsToday();

        Task<int> TotalMessagesToday();

        Task<int> TotalDailyTransactions();

        Task<int> TotalMessageSentToday();

        Task<decimal> TotalClientUnit();

        Task<List<Client>> ClientsWithUnitsBalance();

        Task<List<Transaction>> LastSuccessTransactions(int? displayCount);

        Task<List<Transaction>> LastFailedTransactions(int? displayCount);

        Task<List<Message>> LastSuccessMessages(int? displayCount);

        Task<List<Message>> LastFailedMessages(int? displayCount);

        Task<List<Message>> ScheduledMessages(int? displayCount);

        Task<List<Transaction>> ClientLastTransactions(int? clientId);

        Task<List<Message>> ClientLastMessages(int? displayCount, string userId);

        Task<List<Message>> ClientScheduledMessages(int? displayCount, string userId);

        Task<List<Client>> GetLastUsers(int? displayCount);

        Task<List<Message>> Messages();

        Task<MessageDetailDto> MessageDetails(int Id);

        Task<List<MessageChunkDto>> ChunkMessages(int Id);

        Task<ApiBalanceFirstDto> ApiBalanceFirstDto();

        Task<ApiBalanceSecondDto> ApiBalanceSecondDto();
    }
}