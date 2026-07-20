using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Dtos;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Services;

using System;
using System.Collections.Generic;
using Microsoft.EntityFrameworkCore;

using System.IO;
using System.Linq;
using System.Net;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

using System.Net.Http;
using Newtonsoft.Json;

namespace Exwhyzee.Messaging.Core.Data.Services
{
    public class DashboardService : IDashboardService
    {
        public ApplicationDbContext db = new ApplicationDbContext();

        public async Task<int> AllPendingTransactions()
        {
            var transactions = db.Transactions.AsNoTracking().Where(x => x.Status == TransactionStatus.Pending);
            return await transactions.CountAsync();
        }

        public async Task<List<Message>> ClientLastMessages(int? displayCount, string userId)
        {
            if (displayCount == null)
            {
                displayCount = 0;
            }
            var message = db.Messages.Where(x => x.UserId == userId).OrderByDescending(x => x.MessageId).Take(displayCount.Value);
            return await message.ToListAsync();
        }


        public async Task<List<Transaction>> ClientLastTransactions(int? clientId)
        {

            var transactions = db.Transactions.Where(x => x.ClientId == clientId).OrderByDescending(x => x.TransactionId).ToList();
            if (transactions.Count() > 0)
            {
                return transactions = transactions.Take(10).ToList();
            }
            else
            {
                return transactions = null;
            }

        }

        public async Task<int> ClientPendingTransactions(int? clientId)
        {
            var transactions = db.Transactions.Where(x => x.Status == TransactionStatus.Pending && x.ClientId == clientId);
            if (transactions != null)
            {
                return await transactions.CountAsync();
            }
            else
            {
                return 0;
            }

        }

        public async Task<List<Message>> ClientScheduledMessages(int? displayCount, string userId)
        {
            if (displayCount == null)
            {
                displayCount = 0;
            }
            var messages = db.Messages.Where(x => x.UserId == userId && x.Status == MessageStatus.Scheduled).OrderByDescending(x => x.MessageId).Take(displayCount.Value);
            return await messages.ToListAsync();
        }

        public async Task<List<Client>> GetLastUsers(int? displayCount)
        {
            var users = db.Clients.OrderByDescending(x => x.ClientId).Include(m => m.User).Take(displayCount.Value);
            return await users.ToListAsync();
        }

        public async Task<List<Message>> LastSuccessMessages(int? displayCount)
        {
            if (displayCount == null)
            {
                displayCount = 0;
            }
            var messages = db.Messages.Where(x => x.Status == MessageStatus.Sent).OrderByDescending(x => x.MessageId).Take(displayCount.Value);
            return await messages.ToListAsync();
        }

        public async Task<List<Message>> LastFailedMessages(int? displayCount)
        {
            if (displayCount == null)
            {
                displayCount = 0;
            }
            var messages = db.Messages.Where(x => x.Status != MessageStatus.Sent).OrderByDescending(x => x.MessageId).Take(displayCount.Value);
            return await messages.ToListAsync();
        }


        public async Task<List<Transaction>> LastSuccessTransactions(int? displayCount)
        {
            var transactions = db.Transactions.Where(x => x.Status == TransactionStatus.Approved).OrderByDescending(x => x.TransactionId).AsNoTracking().Take(displayCount.Value);
            return await transactions.ToListAsync();
        }

        public async Task<List<Transaction>> LastFailedTransactions(int? displayCount)
        {
            var transactions = db.Transactions.Where(x => x.Status != TransactionStatus.Approved).OrderByDescending(x => x.TransactionId).AsNoTracking().Take(displayCount.Value);
            return await transactions.ToListAsync();
        }

        public async Task<List<Message>> ScheduledMessages(int? displayCount)
        {
            if (displayCount == null)
            {
                displayCount = 0;
            }
            var messages = db.Messages.Where(x => x.Status == MessageStatus.Scheduled).OrderByDescending(x => x.MessageId).Take(displayCount.Value);
            return await messages.ToListAsync();
        }


        public async Task<int> TotalClients()
        {
            var users = db.Users.CountAsync();
            return await users;
        }

        public async Task<int> TotalClientsToday()
        {
            var currenttime2 = DateTime.Today;
            var users = db.Users.AsNoTracking().Where(x => x.DateRegitered.Date == currenttime2).CountAsync();
            return await users;
        }

        public async Task<int> TotalDailyTransactions()
        {
            var currenttime = DateTime.Today;
            var transactions = db.Transactions.AsNoTracking().Where(x => x.DateCreated.Date == currenttime);
            return await transactions.CountAsync();
        }

        public async Task<int> TotalMessageSentToday()
        {
            var currenttime2 = DateTime.Today;
            var message = db.Messages.AsNoTracking().Where(x => x.DeliveredDate.Value.Date == currenttime2);
            return await message.CountAsync();
        }

        public async Task<int> TotalMessagesToday()
        {
            var messages = 9;
            return messages;
        }

        public async Task<decimal> TotalClientUnit()
        {
            var units = await db.Clients.Select(x => x.Units).SumAsync();
            return units;
        }

        public async Task<List<Client>> ClientsWithUnitsBalance()
        {
            var clients = await db.Clients.Include(x => x.User).OrderByDescending(x => x.Units).ToListAsync();
            return clients;
        }

        public async Task<List<Message>> Messages()
        {
            var messages = await db.Messages.OrderByDescending(x => x.MessageId).ToListAsync();
            return messages;
        }

        public async Task<MessageDetailDto> MessageDetails(int Id)
        {
            var message = await db.Messages.Include(x => x.User).FirstOrDefaultAsync(x => x.MessageId == Id);
            string n = message.Recipients.Replace("\r\n", ",");
            IList<string> numbers = n.Split(new string[] { ",", " " }, StringSplitOptions.RemoveEmptyEntries);
            numbers = numbers.Distinct().ToList();
            if (message != null)
            {

                var output = new MessageDetailDto
                {
                    MessageId = message.MessageId,
                    SenderId = message.SenderId,
                    Recipients = message.Recipients,
                    MessageContent = message.MessageContent,
                    Response = message.Response,
                    UnitsUsed = message.UnitsUsed,
                    Scheduleddate = message.Scheduleddate,
                    DeliveredDate = message.DeliveredDate,
                    Status = message.Status,
                    Username = message.User.UserName,
                    RecipientsCount = numbers.Count(),


                    Response_status = message.Response_status,
                    Response_error_code = message.Response_error_code,
                    Response_cost = message.Response_cost,
                    Response_data = message.Response_data,
                    Response_msg = message.Response_msg,
                    Response_length = message.Response_length,
                    Response_page = message.Response_page,
                    Response_balance = message.Response_balance,
                    Response_BalanceResponse = message.Response_BalanceResponse,
                };

                return output;
            }
            return null;
        }

        public async Task<List<MessageChunkDto>> ChunkMessages(int Id)
        {
            var chunk = await db.MessageChunks.Where(x => x.MessageId == Id).ToListAsync();

            var mchunk = chunk.Select(c => new MessageChunkDto()
            {
                MessageChunkId = c.MessageChunkId,
                MessageId = c.MessageId,
                Numbers = c.Numbers,
                Response = c.Response,
                NumbersCount = GeneralServices.NumberCount(c.Numbers)
            }).ToList();


            return mchunk;
        }

        public async Task<ApiBalanceFirstDto> ApiBalanceFirstDto()
        {
            var getApi = await db.ApiSettings.FirstOrDefaultAsync(x => x.IsDefault == true);
            if (getApi == null) return new ApiBalanceFirstDto { Balance = "0", Name = "No Default API" };

            try
            {
                var clientbal = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post, "https://my.kudisms.net/api/balance");
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(getApi.Token ?? ""), "token");
                request.Content = content;
                var balresponse = await clientbal.SendAsync(request);
                balresponse.EnsureSuccessStatusCode();
                var balanceresponse = await balresponse.Content.ReadAsStringAsync();
                var balresponsed = JsonConvert.DeserializeObject<BalanceResponse>(balanceresponse);

                return new ApiBalanceFirstDto()
                {
                    Balance = balresponsed.msg,
                    Name = getApi.Name
                };
            }
            catch
            {
                return new ApiBalanceFirstDto() { Balance = "Error", Name = getApi.Name };
            }
        }

        public async Task<ApiBalanceSecondDto> ApiBalanceSecondDto()
        {
            var getApi = await db.ApiSettings.Where(x => x.IsDefault == false).OrderByDescending(x => x.ApiSettingId).FirstOrDefaultAsync();
            if (getApi == null) return new ApiBalanceSecondDto { Balance = "0", Name = "N/A" };

            try
            {
                var clientbal = new HttpClient();
                var request = new HttpRequestMessage(HttpMethod.Post, "https://my.kudisms.net/api/balance");
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(getApi.Token ?? ""), "token");
                request.Content = content;
                var balresponse = await clientbal.SendAsync(request);
                balresponse.EnsureSuccessStatusCode();
                var balanceresponse = await balresponse.Content.ReadAsStringAsync();
                var balresponsed = JsonConvert.DeserializeObject<BalanceResponse>(balanceresponse);

                return new ApiBalanceSecondDto()
                {
                    Balance = balresponsed.msg,
                    Name = getApi.Name
                };
            }
            catch
            {
                return new ApiBalanceSecondDto() { Balance = "0", Name = getApi.Name };
            }
        }
    }
}






