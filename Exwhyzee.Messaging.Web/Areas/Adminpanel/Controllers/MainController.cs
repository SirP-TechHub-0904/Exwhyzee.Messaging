using Exwhyzee.Messaging.Web.Data.IServices;
using Exwhyzee.Messaging.Web.Data.Services;
using Exwhyzee.Messaging.Web.Models;
using PagedList;
using System;
using System.Linq;
using System.Data.Entity;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class MainController : Controller
    {


        private ApplicationDbContext db = new ApplicationDbContext();

        private IClientService _clientService = new ClientService();
        private ITransactionService _transactions = new TransactionService();
        private IDashboardService _dashboardService = new DashboardService();
        private System.Random randomInteger = new System.Random();
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


            //last ten transaction

            var lastTenSuccessTransaction = await _dashboardService.LastSuccessTransactions(10);
            ViewBag.lastTenSuccessTransaction = lastTenSuccessTransaction.ToArray();

            var lastTenFailedTransaction = await _dashboardService.LastFailedTransactions(10);
            ViewBag.lastTenFailedTransaction = lastTenFailedTransaction.ToArray();

            var successmessage = await _dashboardService.LastSuccessMessages(10);
            ViewBag.successmessage = successmessage;

            var failmessage = await _dashboardService.LastFailedMessages(10);
            ViewBag.failmessage = failmessage;

            var firstbalance = await _dashboardService.ApiBalanceFirstDto();
            ViewBag.firstbalance = firstbalance;

            var secondbalance = await _dashboardService.ApiBalanceSecondDto();
            ViewBag.secondbalance = secondbalance;

           
            return View();
        }


        public async Task<ActionResult> Messages(string searchString, string currentFilter, int? page)
        {

            var items = await _dashboardService.Messages();
            items = items.ToList();
            if (!String.IsNullOrEmpty(searchString))
            {

                items = items.Where(s => s.MessageContent.ToUpper().Contains(searchString.ToUpper())
            || s.SenderId.ToString().ToUpper().Contains(searchString.ToUpper())
            || s.Status.ToString().ToUpper().Contains(searchString.ToUpper())
            || s.Response.ToString().ToUpper().Contains(searchString.ToUpper())
            || s.Recipients.ToString().ToUpper().Contains(searchString.ToUpper())
            || s.Resent.ToString().ToUpper().Contains(searchString.ToUpper())
            || s.UnitsUsed.ToString().ToUpper().Contains(searchString.ToUpper())).ToList();
            }
            items = items.OrderByDescending(x => x.DeliveredDate).ToList();
            int pageSize = 50;
            int pageNumber = (page ?? 1);
            return View(items.ToPagedList(pageNumber, pageSize));

        }

        public async Task<ActionResult> MessageDetails(int id)
        {
            var message = await _dashboardService.MessageDetails(id);

            var chunkmessage = await _dashboardService.ChunkMessages(id);
            ViewBag.chunk = chunkmessage;
            return View(message);
        }
    }
}