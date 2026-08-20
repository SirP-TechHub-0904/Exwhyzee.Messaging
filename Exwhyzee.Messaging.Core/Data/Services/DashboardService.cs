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
            var messages = db.Messages.Where(x => x.Status == MessageStatus.Pending || x.Status == MessageStatus.Failed).OrderByDescending(x => x.MessageId).Take(displayCount.Value);
            return await messages.ToListAsync();
        }

        public async Task<List<Transaction>> LastSuccessTransactions(int? displayCount)
        {
            var transactions = db.Transactions.Where(x => x.Status == TransactionStatus.Approved || x.DateApproved != null).OrderByDescending(x => x.TransactionId).AsNoTracking().Take(displayCount.Value);
            return await transactions.ToListAsync();
        }

        public async Task<List<Transaction>> LastFailedTransactions(int? displayCount)
        {
            var transactions = db.Transactions.Where(x => x.Status == TransactionStatus.Pending).OrderByDescending(x => x.TransactionId).AsNoTracking().Take(displayCount.Value);
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
            if (getApi == null || string.IsNullOrWhiteSpace(getApi.Token))
            {
                return new ApiBalanceFirstDto 
                { 
                    Balance = "0.00", 
                    Name = getApi?.Name ?? "Promotional Gateway", 
                    IsSuccess = false, 
                    StatusMessage = "Token Not Configured",
                    RouteType = "Promotional"
                };
            }

            try
            {
                using var clientbal = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                var request = new HttpRequestMessage(HttpMethod.Post, "https://my.kudisms.net/api/balance");
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(getApi.Token.Trim()), "token");
                request.Content = content;
                var balresponse = await clientbal.SendAsync(request);
                var balanceresponse = await balresponse.Content.ReadAsStringAsync();
                var balresponsed = JsonConvert.DeserializeObject<BalanceResponse>(balanceresponse);

                // Check if msg is numeric balance
                if (balresponsed != null && decimal.TryParse(balresponsed.msg, out var numBal))
                {
                    return new ApiBalanceFirstDto
                    {
                        Balance = numBal.ToString("N2"),
                        Name = string.IsNullOrWhiteSpace(getApi.Name) ? "Promotional Gateway" : getApi.Name,
                        IsSuccess = true,
                        StatusMessage = "Connected",
                        RouteType = "Promotional"
                    };
                }

                return new ApiBalanceFirstDto
                {
                    Balance = "0.00",
                    Name = getApi.Name,
                    IsSuccess = false,
                    StatusMessage = balresponsed?.msg ?? "Invalid Response",
                    RouteType = "Promotional"
                };
            }
            catch (Exception ex)
            {
                return new ApiBalanceFirstDto 
                { 
                    Balance = "0.00", 
                    Name = getApi.Name, 
                    IsSuccess = false, 
                    StatusMessage = "Connection Timeout",
                    RouteType = "Promotional"
                };
            }
        }

        public async Task<ApiBalanceSecondDto> ApiBalanceSecondDto()
        {
            var getApi = await db.ApiSettings.Where(x => x.IsDefault == false).OrderByDescending(x => x.ApiSettingId).FirstOrDefaultAsync();
            if (getApi == null || string.IsNullOrWhiteSpace(getApi.Token))
            {
                return new ApiBalanceSecondDto 
                { 
                    Balance = "0.00", 
                    Name = getApi?.Name ?? "Transactional Gateway", 
                    IsSuccess = false, 
                    StatusMessage = "Token Not Configured",
                    RouteType = "Transactional"
                };
            }

            try
            {
                using var clientbal = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
                var request = new HttpRequestMessage(HttpMethod.Post, "https://my.kudisms.net/api/balance");
                var content = new MultipartFormDataContent();
                content.Add(new StringContent(getApi.Token.Trim()), "token");
                request.Content = content;
                var balresponse = await clientbal.SendAsync(request);
                var balanceresponse = await balresponse.Content.ReadAsStringAsync();
                var balresponsed = JsonConvert.DeserializeObject<BalanceResponse>(balanceresponse);

                if (balresponsed != null && decimal.TryParse(balresponsed.msg, out var numBal))
                {
                    return new ApiBalanceSecondDto
                    {
                        Balance = numBal.ToString("N2"),
                        Name = string.IsNullOrWhiteSpace(getApi.Name) ? "Transactional Gateway" : getApi.Name,
                        IsSuccess = true,
                        StatusMessage = "Connected",
                        RouteType = "Transactional"
                    };
                }

                return new ApiBalanceSecondDto
                {
                    Balance = "0.00",
                    Name = getApi.Name,
                    IsSuccess = false,
                    StatusMessage = balresponsed?.msg ?? "Token Invalid",
                    RouteType = "Transactional"
                };
            }
            catch (Exception ex)
            {
                return new ApiBalanceSecondDto 
                { 
                    Balance = "0.00", 
                    Name = getApi.Name, 
                    IsSuccess = false, 
                    StatusMessage = "Connection Timeout",
                    RouteType = "Transactional"
                };
            }
        }

        // =========================================================================
        // =========================================================================
        // ADVANCED EXECUTIVE ANALYTICS IMPLEMENTATIONS
        // =========================================================================

        public async Task<List<TopUserUsageDto>> GetTopUsersUsageAsync(DateTime? startDate, DateTime? endDate, int count = 20)
        {
            var query = db.Messages.AsQueryable();

            if (startDate.HasValue)
                query = query.Where(m => (m.DeliveredDate.HasValue && m.DeliveredDate >= startDate.Value) || (m.Scheduleddate.HasValue && m.Scheduleddate >= startDate.Value) || !m.DeliveredDate.HasValue);

            if (endDate.HasValue)
                query = query.Where(m => (m.DeliveredDate.HasValue && m.DeliveredDate <= endDate.Value) || (m.Scheduleddate.HasValue && m.Scheduleddate <= endDate.Value) || !m.DeliveredDate.HasValue);

            var rawMessages = await query
                .Where(m => !string.IsNullOrEmpty(m.UserId))
                .Select(m => new { m.UserId, m.UnitsUsed, m.Status })
                .ToListAsync();

            var grouped = rawMessages
                .GroupBy(m => m.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    TotalMessages = g.Count(),
                    TotalUnits = g.Sum(m => m.UnitsUsed),
                    SentCount = g.Count(m => m.Status == MessageStatus.Sent || m.Status == MessageStatus.Pending)
                })
                .OrderByDescending(x => x.TotalMessages)
                .Take(count)
                .ToList();

            var userIds = grouped.Select(g => g.UserId).Distinct().ToList();
            var clients = await db.Clients.Where(c => userIds.Contains(c.UserId) || userIds.Contains(c.ClientId.ToString())).ToListAsync();
            var users = await db.Users.Where(u => userIds.Contains(u.Id) || userIds.Contains(u.UserName)).ToListAsync();

            var result = new List<TopUserUsageDto>();
            foreach (var item in grouped)
            {
                var client = clients.FirstOrDefault(c => c.UserId == item.UserId || c.ClientId.ToString() == item.UserId);
                var user = users.FirstOrDefault(u => u.Id == item.UserId || u.UserName == item.UserId);

                string displayName = client != null ? $"{client.FirstName} {client.Surname}".Trim() : (user?.UserName ?? item.UserId);
                if (string.IsNullOrWhiteSpace(displayName)) displayName = user?.UserName ?? item.UserId;

                double rate = item.TotalMessages > 0 ? Math.Round(((double)item.SentCount / item.TotalMessages) * 100, 1) : 100.0;

                result.Add(new TopUserUsageDto
                {
                    UserId = item.UserId,
                    Username = user?.UserName ?? (client?.UserId ?? item.UserId),
                    FullName = displayName,
                    MessagesCount = item.TotalMessages,
                    UnitsBurned = item.TotalUnits,
                    DeliveryRate = rate
                });
            }

            return result;
        }

        public async Task<List<TopFunderDto>> GetTopFundersAsync(int count = 10)
        {
            var transactionsList = await db.Transactions
                .Where(t => t.AmountPaid > 0 || t.Amount > 0 || t.Units > 0)
                .Select(t => new { t.ClientId, t.UserId, t.AmountPaid, t.Amount, t.Units, t.DateCreated })
                .ToListAsync();

            var grouped = transactionsList
                .GroupBy(t => t.ClientId > 0 ? t.ClientId.ToString() : (t.UserId ?? "Unknown"))
                .Select(g => new
                {
                    Key = g.Key,
                    TotalPaid = g.Sum(t => (t.AmountPaid.HasValue && t.AmountPaid > 0 ? t.AmountPaid.Value : t.Amount)),
                    TotalUnits = g.Sum(t => t.Units),
                    TxCount = g.Count(),
                    LastDate = g.Max(t => t.DateCreated),
                    FirstClientId = g.FirstOrDefault(t => t.ClientId > 0)?.ClientId ?? 0,
                    FirstUserId = g.FirstOrDefault(t => !string.IsNullOrEmpty(t.UserId))?.UserId ?? ""
                })
                .OrderByDescending(x => x.TotalPaid)
                .Take(count)
                .ToList();

            var clientIds = grouped.Where(g => g.FirstClientId > 0).Select(g => g.FirstClientId).ToList();
            var clients = await db.Clients.Where(c => clientIds.Contains(c.ClientId)).ToListAsync();

            var userIds = grouped.Where(g => !string.IsNullOrEmpty(g.FirstUserId)).Select(g => g.FirstUserId).ToList();
            var users = await db.Users.Where(u => userIds.Contains(u.Id) || userIds.Contains(u.UserName)).ToListAsync();

            var result = new List<TopFunderDto>();
            foreach (var item in grouped)
            {
                var client = clients.FirstOrDefault(c => c.ClientId == item.FirstClientId);
                var user = users.FirstOrDefault(u => u.Id == item.FirstUserId || u.UserName == item.FirstUserId);

                string displayName = client != null ? $"{client.FirstName} {client.Surname}".Trim() : (user?.UserName ?? item.Key);
                if (string.IsNullOrWhiteSpace(displayName)) displayName = user?.UserName ?? item.Key;

                result.Add(new TopFunderDto
                {
                    ClientId = item.FirstClientId,
                    UserId = item.FirstUserId,
                    Username = user?.UserName ?? (client?.UserId ?? item.Key),
                    FullName = displayName,
                    TotalAmountPaid = item.TotalPaid,
                    TotalUnitsBought = item.TotalUnits,
                    TransactionCount = item.TxCount,
                    LastFundingDate = item.LastDate
                });
            }

            return result;
        }

        public async Task<List<TopUserUsageDto>> GetTopUnitBurnersAsync(int count = 10)
        {
            var rawMessages = await db.Messages
                .Where(m => !string.IsNullOrEmpty(m.UserId))
                .Select(m => new { m.UserId, m.UnitsUsed, m.Status })
                .ToListAsync();

            var grouped = rawMessages
                .GroupBy(m => m.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    TotalMessages = g.Count(),
                    TotalUnits = g.Sum(m => m.UnitsUsed),
                    SentCount = g.Count(m => m.Status == MessageStatus.Sent || m.Status == MessageStatus.Pending)
                })
                .OrderByDescending(x => x.TotalUnits)
                .Take(count)
                .ToList();

            var userIds = grouped.Select(g => g.UserId).Distinct().ToList();
            var clients = await db.Clients.Where(c => userIds.Contains(c.UserId) || userIds.Contains(c.ClientId.ToString())).ToListAsync();
            var users = await db.Users.Where(u => userIds.Contains(u.Id) || userIds.Contains(u.UserName)).ToListAsync();

            var result = new List<TopUserUsageDto>();
            foreach (var item in grouped)
            {
                var client = clients.FirstOrDefault(c => c.UserId == item.UserId || c.ClientId.ToString() == item.UserId);
                var user = users.FirstOrDefault(u => u.Id == item.UserId || u.UserName == item.UserId);

                string displayName = client != null ? $"{client.FirstName} {client.Surname}".Trim() : (user?.UserName ?? item.UserId);
                if (string.IsNullOrWhiteSpace(displayName)) displayName = user?.UserName ?? item.UserId;

                double rate = item.TotalMessages > 0 ? Math.Round(((double)item.SentCount / item.TotalMessages) * 100, 1) : 100.0;

                result.Add(new TopUserUsageDto
                {
                    UserId = item.UserId,
                    Username = user?.UserName ?? (client?.UserId ?? item.UserId),
                    FullName = displayName,
                    MessagesCount = item.TotalMessages,
                    UnitsBurned = item.TotalUnits,
                    DeliveryRate = rate
                });
            }

            return result;
        }

        public async Task<List<HeavyBlastSenderDto>> GetHeavyBlastSendersAsync(int count = 10)
        {
            var recentMsgs = await db.Messages
                .Where(m => !string.IsNullOrEmpty(m.UserId) && !string.IsNullOrEmpty(m.Recipients))
                .OrderByDescending(m => m.MessageId)
                .Take(1000)
                .ToListAsync();

            var userGroups = recentMsgs.GroupBy(m => m.UserId);
            var list = new List<HeavyBlastSenderDto>();

            foreach (var group in userGroups)
            {
                int maxRecipients = 0;
                int totalRecipients = 0;
                DateTime? lastDate = null;

                foreach (var msg in group)
                {
                    int recCount = msg.Recipients.Split(new[] { ',', ';', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries).Length;
                    if (recCount > maxRecipients) maxRecipients = recCount;
                    totalRecipients += recCount;
                    var msgDate = msg.DeliveredDate ?? msg.Scheduleddate;
                    if (lastDate == null || (msgDate.HasValue && msgDate > lastDate)) lastDate = msgDate;
                }

                var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == group.Key || c.ClientId.ToString() == group.Key);
                var user = await db.Users.FirstOrDefaultAsync(u => u.Id == group.Key || u.UserName == group.Key);

                string displayName = client != null ? $"{client.FirstName} {client.Surname}".Trim() : (user?.UserName ?? group.Key);
                if (string.IsNullOrWhiteSpace(displayName)) displayName = user?.UserName ?? group.Key;

                list.Add(new HeavyBlastSenderDto
                {
                    UserId = group.Key,
                    Username = user?.UserName ?? group.Key,
                    FullName = displayName,
                    MaxRecipientsInSingleMsg = maxRecipients,
                    TotalRecipientsReached = totalRecipients,
                    TotalBroadcasts = group.Count(),
                    LastBroadcastDate = lastDate
                });
            }

            return list.OrderByDescending(x => x.MaxRecipientsInSingleMsg).ThenByDescending(x => x.TotalRecipientsReached).Take(count).ToList();
        }

        public async Task<PaystackLiquidityDto> GetPaystackLiquidityAsync()
        {
            var allTxns = await db.Transactions
                .Where(t => t.AmountPaid > 0 || t.Amount > 0 || t.Units > 0)
                .ToListAsync();

            var pendingCount = await db.Transactions.CountAsync(t => t.Status == TransactionStatus.Pending);

            decimal totalRev = allTxns.Sum(t => (t.AmountPaid.HasValue && t.AmountPaid > 0 ? t.AmountPaid.Value : t.Amount));
            decimal pendingTransferAmt = allTxns.Where(t => t.IsAdminTransferred == false).Sum(t => (t.AmountPaid.HasValue && t.AmountPaid > 0 ? t.AmountPaid.Value : t.Amount));
            decimal transferredAmt = allTxns.Where(t => t.IsAdminTransferred == true).Sum(t => (t.AmountPaid.HasValue && t.AmountPaid > 0 ? t.AmountPaid.Value : t.Amount));

            return new PaystackLiquidityDto
            {
                TotalPaystackRevenue = totalRev,
                TotalApprovedPaystackTxns = allTxns.Count,
                TotalUnitsCreditedViaPaystack = allTxns.Sum(t => t.Units),
                TotalPendingTransferAmount = pendingTransferAmt,
                TotalAdminTransferredAmount = transferredAmt,
                PendingTransfersCount = allTxns.Count(t => t.IsAdminTransferred == false),
                PendingPaystackTxns = pendingCount,
                AppTag = "Exwhyzee.Messaging"
            };
        }

        public async Task<int> GetLiveActiveUsersCountAsync(int minutesThreshold = 15)
        {
            var cutoff = DateTime.UtcNow.AddMinutes(-minutesThreshold);
            var activeMsgUsers = await db.Messages
                .Where(m => m.DeliveredDate >= cutoff && !string.IsNullOrEmpty(m.UserId))
                .Select(m => m.UserId)
                .Distinct()
                .CountAsync();

            return activeMsgUsers > 0 ? activeMsgUsers : 1; // At least admin logged in
        }
    }
}






