using Exwhyzee.Messaging.Web.Data.IServices;
using Exwhyzee.Messaging.Web.Data.Services;
using Exwhyzee.Messaging.Web.Models;
using Microsoft.AspNet.Identity.Owin;
using PagedList;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using System.Web;
using System.Web.Mvc;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class TransactionsController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        private ITransactionService _transactionService = new TransactionService();
        private IDashboardService _dashboardService = new DashboardService();
        private ApplicationUserManager _userManager;

        public TransactionsController()
        {
        }

        public TransactionsController(TransactionService transactionService, ApplicationUserManager userManager
            , DashboardService dashboardService)
        {
            _transactionService = transactionService;
            _userManager = userManager;
            _dashboardService = dashboardService;
        }

        public ApplicationUserManager UserManager
        {
            get
            {
                return _userManager ?? HttpContext.GetOwinContext().GetUserManager<ApplicationUserManager>();
            }
            private set
            {
                _userManager = value;
            }
        }

        // GET: Adminpanel/Transactions
        public async Task<ActionResult> Index(string searchString, string currentFilter, int? page)
        {

            var items = await _transactionService.GetTransactions();
            items = items.ToList();
            if (!String.IsNullOrEmpty(searchString))
            {


                items = items.Where(s =>
 //    s.ApprovedBy.ToUpper().Contains(searchString.ToUpper())

 s.DateApproved.ToString().ToUpper().Contains(searchString.ToUpper())
            || s.Status.ToString().ToUpper().Contains(searchString.ToUpper())
            || (!String.IsNullOrEmpty(s.ApprovedBy) && s.ApprovedBy.ToString().ToUpper().Contains(searchString.ToUpper()))
            || s.Units.ToString().ToUpper().Contains(searchString.ToUpper())).ToList();
            }
            items = items.OrderByDescending(x => x.TransactionId).ToList();
            int pageSize = 100;
            int pageNumber = (page ?? 1);
            return View(items.ToPagedList(pageNumber, pageSize));

        }

        //

        public async Task<ActionResult> ClientUnits()
        {

            var clients = await _dashboardService.ClientsWithUnitsBalance();

            ViewBag.totalunits = await _dashboardService.TotalClientUnit();
            return View(clients);

        }

        // GET: Adminpanel/Transactions/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Transaction transaction = await _transactionService.GetTransaction(id);
            if (transaction == null)
            {
                return HttpNotFound();
            }
            return View(transaction);
        }

        [HttpPost]
        public async Task<ActionResult> UpdateStatus(int? id, string status)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            Transaction transaction = await _transactionService.GetTransaction(id);
            if (transaction == null)
            {
                return HttpNotFound();
            }

            try
            {
                if (status == "Approve")
                {
                    transaction.Status = TransactionStatus.Approved;
                }
                else if (status == "Cancel")
                {
                    transaction.Status = TransactionStatus.Cancelled;
                }
                else
                {
                    transaction.Status = TransactionStatus.Pending;
                }

                var user = await UserManager.FindByNameAsync(User.Identity.Name);
                transaction.UserId = user.Id;
                await _transactionService.UpdateTransactionStatus(transaction);
            }
            catch (Exception ex)
            {
                TempData["error"] = "Error: " + ex.Message;
            }

            return RedirectToAction("Details", new { id = id });
        }

        // GET: Adminpanel/Transactions
        public async Task<ActionResult> UsersTransaction(int id)
        {
            ViewBag.TotalUnits = await _transactionService.TotalUnitPurchasedByClient(id);
            ViewBag.TotalAmount = await _transactionService.TotalAmountOfUnitsByClient(id);
            ViewBag.TotalAmountPaid = await _transactionService.TotalAmountPaidByClient(id);

            return View(await _transactionService.GetTransactionsByClient(id));
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && _userManager != null)
            {
                _userManager.Dispose();
                db.Dispose();
                _userManager = null;
            }

            base.Dispose(disposing);
        }
    }
}