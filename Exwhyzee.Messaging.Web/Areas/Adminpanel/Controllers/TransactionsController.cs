using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Services;
using Exwhyzee.Messaging.Core.Models;
using X.PagedList;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using X.PagedList.Extensions;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    [Area("Adminpanel")]
    public class TransactionsController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        private ITransactionService _transactionService = new TransactionService();
        private IDashboardService _dashboardService = new DashboardService();
        private Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        public TransactionsController(Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> userManager)
        {
            _userManager = userManager;
        }

        public Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> UserManager
        {
            get => _userManager;
            private set => _userManager = value;
        }

        // GET: Adminpanel/Transactions
        public async Task<ActionResult> Index(string searchString, string currentFilter, int? page)
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

            var query = db.Transactions
                .AsNoTracking()
                .Include(t => t.User)
                .AsQueryable();

            if (!string.IsNullOrEmpty(searchString))
            {
                searchString = searchString.Trim();
                query = query.Where(s => 
                    (s.User != null && s.User.UserName.Contains(searchString)) ||
                    (s.TransactionReference != null && s.TransactionReference.Contains(searchString)) ||
                    (s.ApprovedBy != null && s.ApprovedBy.Contains(searchString)) ||
                    (s.PaymentSource != null && s.PaymentSource.Contains(searchString)) ||
                    s.TransactionId.ToString().Contains(searchString));
            }

            int pageSize = 25;
            int pageNumber = page ?? 1;
            var items = query.OrderByDescending(x => x.DateCreated).ThenByDescending(x => x.TransactionId).ToPagedList(pageNumber, pageSize);
            return View(items);
        }

        public async Task<ActionResult> ClientUnits()
        {
            var clients = await _dashboardService.ClientsWithUnitsBalance();
            ViewBag.totalunits = await _dashboardService.TotalClientUnit();
            return View(clients);
        }

        // GET: Adminpanel/Transactions/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null) return BadRequest();
            Transaction transaction = await _transactionService.GetTransaction(id);
            if (transaction == null) return NotFound();
            return View(transaction);
        }

        [HttpPost]
        public async Task<ActionResult> UpdateStatus(int? id, string status)
        {
            if (id == null) return BadRequest();
            Transaction transaction = await _transactionService.GetTransaction(id);
            if (transaction == null) return NotFound();

            try
            {
                if (status == "Approve")
                {
                    bool wasAlreadyApproved = transaction.Status == TransactionStatus.Approved;
                    transaction.Status = TransactionStatus.Approved;
                    transaction.DateApproved = DateTime.UtcNow;
                    transaction.ApprovedBy = User.Identity?.Name ?? "Admin";

                    if (!wasAlreadyApproved)
                    {
                        var client = await db.Clients.Include(c => c.User).FirstOrDefaultAsync(c => c.ClientId == transaction.ClientId);
                        if (client != null)
                        {
                            decimal unitsToAdd = transaction.Units > 0 ? transaction.Units : transaction.Amount;
                            client.Units += unitsToAdd;
                            await db.SaveChangesAsync();

                            if (client.User != null && !string.IsNullOrWhiteSpace(client.User.Email))
                            {
                                try
                                {
                                    var zeptoMail = new ZeptoMailService();
                                    await zeptoMail.SendPaymentSuccessReceiptAsync(
                                        recipientEmail: client.User.Email,
                                        username: client.User.UserName ?? client.FirstName,
                                        unitsPurchased: unitsToAdd,
                                        totalNewBalance: (decimal)client.Units,
                                        amountPaid: transaction.Amount,
                                        transactionRef: transaction.TransactionReference ?? $"TXN-{transaction.TransactionId}",
                                        paymentMethod: transaction.TransactionType == TransactionType.BankDeposit ? "Bank Deposit / Manual Transfer" : transaction.TransactionType.ToString()
                                    );
                                }
                                catch { }
                            }
                        }
                    }
                }
                else if (status == "Revoke")
                {
                    transaction.Status = TransactionStatus.Revoked;
                }
                else if (status == "Cancel")
                {
                    transaction.Status = TransactionStatus.Cancelled;
                }
                else
                {
                    transaction.Status = TransactionStatus.Pending;
                }

                if (User.Identity != null && !string.IsNullOrWhiteSpace(User.Identity.Name))
                {
                    var user = await db.Users.FirstOrDefaultAsync(u => u.UserName == User.Identity.Name);
                    if (user != null)
                    {
                        transaction.UserId = user.Id;
                    }
                }

                await _transactionService.UpdateTransactionStatus(transaction);
                TempData["success"] = $"Transaction status successfully updated to {status}.";
            }
            catch (Exception ex)
            {
                TempData["error"] = "Error: " + ex.Message;
            }

            return RedirectToAction("Details", new { id = id });
        }

        // GET: Adminpanel/Transactions/PaystackOutflow
        public async Task<ActionResult> PaystackOutflow(string tab = "available", int? page = 1)
        {
            var todayStart = DateTime.Today;

            var allPaystackTxns = await db.Transactions
                .Include(t => t.User)
                .Where(t => t.IsPaystackOutflow && t.Status == TransactionStatus.Approved)
                .OrderByDescending(t => t.TransactionId)
                .ToListAsync();

            ViewBag.OverallRevenue = allPaystackTxns.Sum(t => t.AmountPaid ?? t.Amount);
            ViewBag.TotalUncleared = allPaystackTxns.Where(t => !t.IsAdminTransferred).Sum(t => t.AmountPaid ?? t.Amount);
            
            // Available Fund for Transfer (Cleared): approved BEFORE today 12:00 AM & untransferred
            ViewBag.AvailableFund = allPaystackTxns.Where(t => !t.IsAdminTransferred && (t.DateApproved ?? t.DateCreated) < todayStart).Sum(t => t.AmountPaid ?? t.Amount);
            
            // Pending 24h Settlement: approved TODAY (>= today 12:00 AM) & untransferred
            ViewBag.Pending24hSettlement = allPaystackTxns.Where(t => !t.IsAdminTransferred && (t.DateApproved ?? t.DateCreated) >= todayStart).Sum(t => t.AmountPaid ?? t.Amount);
            
            ViewBag.TotalTransferred = allPaystackTxns.Where(t => t.IsAdminTransferred).Sum(t => t.AmountPaid ?? t.Amount);
            ViewBag.ActiveTab = tab;

            IEnumerable<Transaction> filtered = allPaystackTxns;
            if (tab == "available")
            {
                filtered = allPaystackTxns.Where(t => !t.IsAdminTransferred && (t.DateApproved ?? t.DateCreated) < todayStart);
            }
            else if (tab == "pending")
            {
                filtered = allPaystackTxns.Where(t => !t.IsAdminTransferred && (t.DateApproved ?? t.DateCreated) >= todayStart);
            }
            else if (tab == "transferred")
            {
                filtered = allPaystackTxns.Where(t => t.IsAdminTransferred);
            }

            int pageSize = 50;
            int pageNumber = page ?? 1;
            return View(filtered.ToPagedList(pageNumber, pageSize));
        }

        [HttpPost]
        public async Task<ActionResult> BatchTransferAvailable(string returnTab = "available")
        {
            var todayStart = DateTime.Today;

            var clearedTxns = await db.Transactions
                .Where(t => t.IsPaystackOutflow && !t.IsAdminTransferred && t.Status == TransactionStatus.Approved && (t.DateApproved ?? t.DateCreated) < todayStart)
                .ToListAsync();

            if (!clearedTxns.Any())
            {
                TempData["warning"] = "No cleared available funds to transfer.";
                return RedirectToAction("PaystackOutflow", new { tab = returnTab });
            }

            decimal totalAmountTransferred = clearedTxns.Sum(t => t.AmountPaid ?? t.Amount);
            int count = clearedTxns.Count;

            foreach (var txn in clearedTxns)
            {
                txn.IsAdminTransferred = true;
                txn.DateTransferred = DateTime.UtcNow.AddHours(1);
                txn.TransferredBy = User.Identity.Name;
            }

            await db.SaveChangesAsync();

            TempData["Success"] = $"Batch transfer successful! Total ₦{totalAmountTransferred:N2} across {count} cleared transaction(s) marked as Transferred Out by Admin ({User.Identity.Name}).";
            return RedirectToAction("PaystackOutflow", new { tab = returnTab });
        }

        // GET: Adminpanel/Transactions/PaystackTransfer
        public async Task<ActionResult> PaystackTransfer()
        {
            var todayStart = DateTime.Today;
            var adminSetting = await db.AdminSettings.FirstOrDefaultAsync() ?? new AdminSetting();

            var clearedTxns = await db.Transactions
                .Where(t => t.IsPaystackOutflow && !t.IsAdminTransferred && t.Status == TransactionStatus.Approved && (t.DateApproved ?? t.DateCreated) < todayStart)
                .ToListAsync();

            decimal availableFund = clearedTxns.Sum(t => t.AmountPaid ?? t.Amount);

            ViewBag.AdminSetting = adminSetting;
            ViewBag.AvailableFund = availableFund;
            ViewBag.ClearedTxnCount = clearedTxns.Count;

            return View(adminSetting);
        }

        // POST: Adminpanel/Transactions/VerifyBankAccountAjax
        [HttpPost]
        public async Task<IActionResult> VerifyBankAccountAjax(string accountNumber, string bankCode)
        {
            var service = new PaystackTransferService();
            var res = await service.ResolveBankAccountAsync(accountNumber, bankCode);
            if (res.Success)
            {
                return Json(new { success = true, accountName = res.AccountName, message = res.Message });
            }
            return Json(new { success = false, message = res.Message });
        }

        // POST: Adminpanel/Transactions/SaveOutflowBankDetails
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveOutflowBankDetails(string bankName, string bankCode, string accountNumber, string accountName, string adminPassword)
        {
            var currentUser = await UserManager.FindByNameAsync(User.Identity.Name);
            bool isPasswordValid = await UserManager.CheckPasswordAsync(currentUser, adminPassword);
            if (!isPasswordValid)
            {
                TempData["error"] = "Security Check Failed: Invalid Admin Password. Bank configuration was not saved.";
                return RedirectToAction("PaystackTransfer");
            }

            if (string.IsNullOrWhiteSpace(accountNumber) || string.IsNullOrWhiteSpace(bankCode))
            {
                TempData["error"] = "Bank code and 10-digit account number are required.";
                return RedirectToAction("PaystackTransfer");
            }

            var paystackService = new PaystackTransferService();

            // If accountName wasn't supplied, resolve via Paystack
            if (string.IsNullOrWhiteSpace(accountName))
            {
                var resolveRes = await paystackService.ResolveBankAccountAsync(accountNumber, bankCode);
                if (!resolveRes.Success)
                {
                    TempData["error"] = $"Paystack Account Verification Failed: {resolveRes.Message}";
                    return RedirectToAction("PaystackTransfer");
                }
                accountName = resolveRes.AccountName;
            }

            // Generate Paystack Recipient Code
            var recipientRes = await paystackService.CreateTransferRecipientAsync(accountName, accountNumber, bankCode);
            if (!recipientRes.Success)
            {
                TempData["error"] = $"Paystack Recipient Registration Failed: {recipientRes.Message}";
                return RedirectToAction("PaystackTransfer");
            }

            var adminSetting = await db.AdminSettings.FirstOrDefaultAsync();
            if (adminSetting == null)
            {
                adminSetting = new AdminSetting();
                db.AdminSettings.Add(adminSetting);
            }

            adminSetting.OutflowBankName = bankName?.Trim();
            adminSetting.OutflowBankCode = bankCode?.Trim();
            adminSetting.OutflowAccountNumber = accountNumber?.Trim();
            adminSetting.OutflowAccountName = accountName?.Trim();
            adminSetting.PaystackRecipientCode = recipientRes.RecipientCode;
            adminSetting.OutflowBankLastUpdatedBy = User.Identity.Name;
            adminSetting.OutflowBankLastUpdatedDate = DateTime.UtcNow.AddHours(1);

            await db.SaveChangesAsync();

            TempData["Success"] = $"Destination Bank Details Verified & Saved! Recipient Code: {recipientRes.RecipientCode}. (Updated by {User.Identity.Name})";
            return RedirectToAction("PaystackTransfer");
        }

        // POST: Adminpanel/Transactions/ExecutePaystackTransfer
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecutePaystackTransfer(string adminPassword)
        {
            var currentUser = await UserManager.FindByNameAsync(User.Identity.Name);
            bool isPasswordValid = await UserManager.CheckPasswordAsync(currentUser, adminPassword);
            if (!isPasswordValid)
            {
                TempData["error"] = "Security Check Failed: Invalid Admin Password. Automated transfer aborted.";
                return RedirectToAction("PaystackTransfer");
            }

            var adminSetting = await db.AdminSettings.FirstOrDefaultAsync();
            if (adminSetting == null || string.IsNullOrWhiteSpace(adminSetting.PaystackRecipientCode))
            {
                TempData["error"] = "Destination Bank Account Not Configured. Please set up and verify bank details above first.";
                return RedirectToAction("PaystackTransfer");
            }

            var todayStart = DateTime.Today;
            var clearedTxns = await db.Transactions
                .Where(t => t.IsPaystackOutflow && !t.IsAdminTransferred && t.Status == TransactionStatus.Approved && (t.DateApproved ?? t.DateCreated) < todayStart)
                .ToListAsync();

            if (!clearedTxns.Any())
            {
                TempData["warning"] = "No cleared available funds to transfer.";
                return RedirectToAction("PaystackTransfer");
            }

            decimal availableFund = clearedTxns.Sum(t => t.AmountPaid ?? t.Amount);

            var paystackService = new PaystackTransferService();

            // Ensure recipient code exists or generate a fresh one
            if (string.IsNullOrWhiteSpace(adminSetting.PaystackRecipientCode))
            {
                var recipientRes = await paystackService.CreateTransferRecipientAsync(
                    adminSetting.OutflowAccountName,
                    adminSetting.OutflowAccountNumber,
                    adminSetting.OutflowBankCode);

                if (recipientRes.Success)
                {
                    adminSetting.PaystackRecipientCode = recipientRes.RecipientCode;
                    await db.SaveChangesAsync();
                }
            }

            var transferRes = await paystackService.InitiateTransferAsync(availableFund, adminSetting.PaystackRecipientCode, $"Gateway Outflow Settlement ({User.Identity.Name})");

            // Auto-recovery: If recipient code was invalid/expired on current Paystack environment, re-create and retry
            if (!transferRes.Success && transferRes.Message != null && transferRes.Message.ToLower().Contains("recipient"))
            {
                var recipientRes = await paystackService.CreateTransferRecipientAsync(
                    adminSetting.OutflowAccountName,
                    adminSetting.OutflowAccountNumber,
                    adminSetting.OutflowBankCode);

                if (recipientRes.Success)
                {
                    adminSetting.PaystackRecipientCode = recipientRes.RecipientCode;
                    await db.SaveChangesAsync();

                    transferRes = await paystackService.InitiateTransferAsync(availableFund, adminSetting.PaystackRecipientCode, $"Gateway Outflow Settlement ({User.Identity.Name})");
                }
            }

            if (!transferRes.Success)
            {
                TempData["error"] = $"Paystack API Transfer Failed: {transferRes.Message}";
                return RedirectToAction("PaystackTransfer");
            }

            // Batch-mark all cleared transactions as transferred out
            foreach (var txn in clearedTxns)
            {
                txn.IsAdminTransferred = true;
                txn.DateTransferred = DateTime.UtcNow.AddHours(1);
                txn.TransferredBy = $"{User.Identity.Name} (Ref: {transferRes.TransferCode})";
            }

            await db.SaveChangesAsync();

            TempData["Success"] = $"Automated Paystack Transfer Initiated Successfully! ₦{availableFund:N2} sent to {adminSetting.OutflowAccountName} ({adminSetting.OutflowBankName}). Ref Code: {transferRes.TransferCode}.";
            return RedirectToAction("PaystackTransfer");
        }

        [HttpPost]
        public async Task<ActionResult> MarkAsTransferred(int id, string returnTab = "available")
        {
            var transaction = await db.Transactions.FindAsync(id);
            if (transaction != null)
            {
                transaction.IsAdminTransferred = true;
                transaction.DateTransferred = DateTime.UtcNow.AddHours(1);
                transaction.TransferredBy = User.Identity.Name;
                await db.SaveChangesAsync();
                TempData["Success"] = $"Paystack Transaction #{id} (₦{(transaction.AmountPaid ?? transaction.Amount):N2}) marked as Transferred Out by Admin ({User.Identity.Name}).";
            }
            return RedirectToAction("PaystackOutflow", new { tab = returnTab });
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