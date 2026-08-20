using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using X.PagedList;
using X.PagedList.Extensions;
using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Dtos;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    [Area("Adminpanel")]
    public class MainController : Controller
    {
        private ApplicationDbContext db => HttpContext.RequestServices.GetService<ApplicationDbContext>();
        private readonly IClientService _clientService = new ClientService();
        private readonly ITransactionService _transactions = new TransactionService();
        private readonly IDashboardService _dashboardService = new DashboardService();

        // GET: Adminpanel/Main
        public async Task<ActionResult> Index()
        {
            int allpendingtransaction = await _dashboardService.AllPendingTransactions();
            ViewBag.AllpendingTransactions = allpendingtransaction;

            // getting all daily transaction
            var dailytransaction = await _dashboardService.TotalDailyTransactions();
            ViewBag.Daily = dailytransaction;

            // all sent messages
            var totalmsgperday = await _dashboardService.TotalMessageSentToday();
            ViewBag.Allmessage = totalmsgperday;

            var allclienttoday = await _dashboardService.TotalClientsToday();
            ViewBag.allclienttoday = allclienttoday;

            // Platform aggregate KPIs
            try
            {
                ViewBag.TotalClientsCount = await db.Clients.CountAsync();
                ViewBag.TotalClientUnitsLiability = await db.Clients.SumAsync(c => (decimal?)c.Units) ?? 0;
                ViewBag.PendingSenderIdsCount = await db.XyzSenderIDs.CountAsync(x => x.XYZ_status == "Pending" || string.IsNullOrEmpty(x.XYZ_status));
            }
            catch
            {
                ViewBag.TotalClientsCount = 0;
                ViewBag.TotalClientUnitsLiability = 0;
                ViewBag.PendingSenderIdsCount = 0;
            }

            // last ten transactions
            var lastTenSuccessTransaction = await _dashboardService.LastSuccessTransactions(10);
            ViewBag.lastTenSuccessTransaction = lastTenSuccessTransaction.ToArray();

            var lastTenFailedTransaction = await _dashboardService.LastFailedTransactions(10);
            ViewBag.lastTenFailedTransaction = lastTenFailedTransaction.ToArray();

            // last ten messages
            var successmessage = await _dashboardService.LastSuccessMessages(10);
            ViewBag.successmessage = successmessage;

            var failmessage = await _dashboardService.LastFailedMessages(10);
            ViewBag.failmessage = failmessage;

            // Kudi SMS Gateway Balances
            var firstbalance = await _dashboardService.ApiBalanceFirstDto();
            ViewBag.firstbalance = firstbalance;

            var secondbalance = await _dashboardService.ApiBalanceSecondDto();
            ViewBag.secondbalance = secondbalance;

            // Executive Analytics & Rankings
            try
            {
                ViewBag.TopUsersUsage = await _dashboardService.GetTopUsersUsageAsync(null, null, 20);
                ViewBag.TopFunders = await _dashboardService.GetTopFundersAsync(10);
                ViewBag.TopUnitBurners = await _dashboardService.GetTopUnitBurnersAsync(10);
                ViewBag.HeavyBlastSenders = await _dashboardService.GetHeavyBlastSendersAsync(10);
                ViewBag.PaystackLiquidity = await _dashboardService.GetPaystackLiquidityAsync();
                ViewBag.LiveActiveUsers = await _dashboardService.GetLiveActiveUsersCountAsync(15);
            }
            catch
            {
                ViewBag.TopUsersUsage = new List<TopUserUsageDto>();
                ViewBag.TopFunders = new List<TopFunderDto>();
                ViewBag.TopUnitBurners = new List<TopUserUsageDto>();
                ViewBag.HeavyBlastSenders = new List<HeavyBlastSenderDto>();
                ViewBag.PaystackLiquidity = new PaystackLiquidityDto();
                ViewBag.LiveActiveUsers = 1;
            }

            return View();
        }

        // GET: Adminpanel/Main/GetTopUsersAnalytics (AJAX filter for Chart.js)
        [HttpGet]
        public async Task<IActionResult> GetTopUsersAnalytics(string range, DateTime? fromDate, DateTime? toDate)
        {
            try
            {
                DateTime? start = null;
                DateTime? end = null;
                DateTime now = DateTime.UtcNow;

                if (range == "today")
                {
                    start = now.Date;
                }
                else if (range == "weekly")
                {
                    start = now.AddDays(-7);
                }
                else if (range == "monthly")
                {
                    start = now.AddDays(-30);
                }
                else if (range == "custom" && fromDate.HasValue && toDate.HasValue)
                {
                    start = fromDate.Value;
                    end = toDate.Value.AddDays(1).AddTicks(-1);
                }

                var topUsers = await _dashboardService.GetTopUsersUsageAsync(start, end, 20);

                var labels = topUsers.Select(u => !string.IsNullOrEmpty(u.FullName) ? u.FullName : u.Username).ToArray();
                var messageCounts = topUsers.Select(u => u.MessagesCount).ToArray();
                var unitsBurned = topUsers.Select(u => u.UnitsBurned).ToArray();

                return Json(new
                {
                    success = true,
                    labels = labels,
                    messageCounts = messageCounts,
                    unitsBurned = unitsBurned,
                    data = topUsers
                });
            }
            catch (Exception ex)
            {
                return Json(new
                {
                    success = false,
                    message = ex.Message,
                    labels = new string[0],
                    messageCounts = new int[0],
                    unitsBurned = new decimal[0]
                });
            }
        }

        public async Task<ActionResult> Messages(string searchString, string currentFilter, int? page)
        {
            if (searchString != null)
            {
                page = 1;
            }
            else
            {
                searchString = currentFilter;
            }
            ViewBag.CurrentFilter = searchString;

            var query = db.Messages
                .AsNoTracking()
                .Include(m => m.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.Trim();
                query = query.Where(s => 
                    (s.MessageContent != null && s.MessageContent.Contains(searchString))
                    || (s.SenderId != null && s.SenderId.Contains(searchString))
                    || (s.Recipients != null && s.Recipients.Contains(searchString))
                    || (s.User != null && s.User.UserName.Contains(searchString))
                    || s.MessageId.ToString().Contains(searchString));
            }

            int pageSize = 25;
            int pageNumber = page ?? 1;
            var items = query.OrderByDescending(x => x.MessageId).ToPagedList(pageNumber, pageSize);

            return View(items);
        }

        public async Task<ActionResult> MessageDetails(int id)
        {
            var message = await _dashboardService.MessageDetails(id);
            var chunkmessage = await _dashboardService.ChunkMessages(id);
            ViewBag.chunk = chunkmessage;
            return View(message);
        }

        [HttpPost]
        public async Task<IActionResult> RevokeMessage(int id)
        {
            var msg = await db.Messages.FindAsync(id);
            if (msg != null)
            {
                msg.Status = MessageStatus.Revoked;
                await db.SaveChangesAsync();
                TempData["Success"] = $"Broadcast message #{id} has been Revoked and cleared from queue.";
            }
            return RedirectToAction("Index");
        }
    }
}
