using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Services;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Paystack;
using Exwhyzee.Messaging.Core.Paystack.Models;
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
        private IPayStackApi _paystack => new PayStackApi(AppConfig.PayStackSecretKey);
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
                    bool isPaystack = string.Equals(transaction.PaymentSource, "Paystack", StringComparison.OrdinalIgnoreCase)
                        || transaction.TransactionType == TransactionType.OnlinePayment;

                    if (isPaystack)
                    {
                        if (string.IsNullOrWhiteSpace(transaction.TransactionReference))
                        {
                            TempData["error"] = "Cannot approve: This Paystack transaction has no transaction reference and has not been verified.";
                            return RedirectToAction("Details", new { id = id });
                        }

                        if (transaction.TransactionReference.StartsWith("EXZ_", StringComparison.OrdinalIgnoreCase))
                        {
                            var parts = transaction.TransactionReference.Split('_');
                            if (parts.Length >= 2 && int.TryParse(parts[1], out int refTxId) && refTxId != transaction.TransactionId)
                            {
                                TempData["error"] = $"Approval rejected: Reference mismatch! Reference {transaction.TransactionReference} belongs to Transaction #{refTxId}, not #{transaction.TransactionId}.";
                                return RedirectToAction("Details", new { id = id });
                            }
                        }

                        var verifyResult = await _paystack.Transactions.VerifyTransaction(transaction.TransactionReference.Trim());
                        if (verifyResult == null || !verifyResult.status || verifyResult.data == null || !string.Equals(verifyResult.data.status, "success", StringComparison.OrdinalIgnoreCase))
                        {
                            string failMsg = verifyResult?.data?.gateway_response ?? verifyResult?.message ?? "Transaction not completed on Paystack";
                            TempData["error"] = $"Approval rejected: Paystack payment verification failed ({failMsg}).";
                            return RedirectToAction("Details", new { id = id });
                        }

                        transaction.GatewayResponse = verifyResult.data.gateway_response ?? "Successful";
                        if (verifyResult.data.amount > 0)
                        {
                            transaction.AmountPaid = (decimal)verifyResult.data.amount / 100m;
                        }
                        transaction.IsPaystackOutflow = true;
                        transaction.PaymentSource = "Paystack";
                    }

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
                                        amountPaid: transaction.AmountPaid ?? transaction.Amount,
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

        [HttpPost]
        public async Task<IActionResult> VerifyPaystackTransaction(int id, string reference)
        {
            try
            {
                var transaction = await _transactionService.GetTransaction(id);
                if (transaction == null)
                {
                    return Json(new { success = false, message = "Transaction record not found." });
                }

                string refToVerify = !string.IsNullOrWhiteSpace(reference) ? reference.Trim() : transaction.TransactionReference?.Trim();
                if (string.IsNullOrWhiteSpace(refToVerify))
                {
                    return Json(new { success = false, message = "Please provide a valid Paystack reference to verify." });
                }

                // Reference ID Mismatch Protection (e.g. EXZ_17342_585500006)
                if (refToVerify.StartsWith("EXZ_", StringComparison.OrdinalIgnoreCase))
                {
                    var parts = refToVerify.Split('_');
                    if (parts.Length >= 2 && int.TryParse(parts[1], out int refTxId))
                    {
                        if (refTxId != transaction.TransactionId)
                        {
                            return Json(new
                            {
                                success = false,
                                message = $"Reference Mismatch! This reference belongs to Transaction #{refTxId}, but you are currently auditing Transaction #{transaction.TransactionId}. Please verify the reference from Paystack."
                            });
                        }
                    }
                }

                var response = await _paystack.Transactions.VerifyTransaction(refToVerify);
                if (response == null || !response.status || response.data == null)
                {
                    return Json(new
                    {
                        success = false,
                        message = response?.message ?? "Unable to find transaction on Paystack with this reference."
                    });
                }

                bool isPaid = string.Equals(response.data.status, "success", StringComparison.OrdinalIgnoreCase);

                // Update transaction with verified details
                transaction.TransactionReference = refToVerify;
                transaction.GatewayResponse = response.data.gateway_response ?? response.data.status;
                transaction.PaymentSource = "Paystack";

                if (isPaid)
                {
                    decimal paidAmount = (decimal)response.data.amount / 100m;
                    transaction.AmountPaid = paidAmount;
                    transaction.IsPaystackOutflow = true;
                    await _transactionService.UpdateTransactionStatus(transaction);

                    return Json(new
                    {
                        success = true,
                        isPaid = true,
                        status = response.data.status,
                        reference = refToVerify,
                        amountPaid = paidAmount,
                        channel = response.data.channel ?? "Paystack",
                        gatewayResponse = transaction.GatewayResponse,
                        paidAt = response.data.transaction_date.ToString("MMM dd, yyyy hh:mm tt"),
                        message = $"Payment verified successfully! Paystack confirmed ₦{paidAmount:N2} via {response.data.channel}. You can now approve this transaction."
                    });
                }
                else
                {
                    await _transactionService.UpdateTransactionStatus(transaction);
                    return Json(new
                    {
                        success = true,
                        isPaid = false,
                        status = response.data.status,
                        reference = refToVerify,
                        channel = response.data.channel,
                        gatewayResponse = transaction.GatewayResponse,
                        message = $"Paystack status is '{response.data.status}'. Gateway message: {response.data.gateway_response ?? "Payment was not completed"}."
                    });
                }
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Verification error: " + ex.Message });
            }
        }

        // GET: Adminpanel/Transactions/PaystackOutflow
        public async Task<ActionResult> PaystackOutflow(string tab = "available", int? page = 1)
        {
            await EnsureOutflowTransfersBackfilledAsync();

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

            // Dedicated Paystack Outflow Transfers History
            var transferOutflows = await db.PaystackTransferOutflows
                .OrderByDescending(x => x.DateInitiated)
                .ToListAsync();
            ViewBag.TransferOutflows = transferOutflows;

            IEnumerable<Transaction> filtered = allPaystackTxns;
            if (tab == "available")
            {
                filtered = allPaystackTxns.Where(t => !t.IsAdminTransferred && (t.DateApproved ?? t.DateCreated) < todayStart);
            }
            else if (tab == "pending")
            {
                filtered = allPaystackTxns.Where(t => !t.IsAdminTransferred && (t.DateApproved ?? t.DateCreated) >= todayStart);
            }
            else if (tab == "deposits")
            {
                filtered = allPaystackTxns.Where(t => t.IsAdminTransferred);
            }

            int pageSize = 50;
            int pageNumber = page ?? 1;
            return View(filtered.ToPagedList(pageNumber, pageSize));
        }

        private async Task EnsureOutflowTransfersBackfilledAsync()
        {
            try
            {
                var existingCodes = await db.PaystackTransferOutflows.Select(x => x.TransferCode).ToListAsync();

                var txnsWithRef = await db.Transactions
                    .Where(t => t.IsPaystackOutflow && t.IsAdminTransferred && t.TransferredBy != null && t.TransferredBy.Contains("TRF_"))
                    .ToListAsync();

                if (txnsWithRef.Any())
                {
                    var adminSetting = await db.AdminSettings.FirstOrDefaultAsync();

                    var groups = txnsWithRef.GroupBy(t =>
                    {
                        var match = System.Text.RegularExpressions.Regex.Match(t.TransferredBy ?? "", @"TRF_[A-Za-z0-9]+");
                        return match.Success ? match.Value : null;
                    }).Where(g => !string.IsNullOrEmpty(g.Key) && !existingCodes.Contains(g.Key)).ToList();

                    foreach (var g in groups)
                    {
                        string code = g.Key;
                        var sample = g.First();
                        var outflow = new PaystackTransferOutflow
                        {
                            TransferCode = code,
                            TransferReference = code,
                            Amount = g.Sum(x => x.AmountPaid ?? x.Amount),
                            RecipientCode = adminSetting?.PaystackRecipientCode,
                            AccountName = adminSetting?.OutflowAccountName ?? "Corporate Outflow Account",
                            AccountNumber = adminSetting?.OutflowAccountNumber,
                            BankName = adminSetting?.OutflowBankName,
                            Status = "PendingApproval",
                            InitiatedBy = sample.TransferredBy,
                            DateInitiated = g.Min(x => x.DateTransferred ?? x.DateApproved ?? x.DateCreated),
                            TransactionsCount = g.Count(),
                            TransactionIdsJson = string.Join(",", g.Select(x => x.TransactionId)),
                            PaystackMessage = "Transfer imported from transaction ledger. Click 'Verify with Paystack' to check live status."
                        };
                        db.PaystackTransferOutflows.Add(outflow);
                    }

                    if (groups.Any())
                    {
                        await db.SaveChangesAsync();
                    }
                }
            }
            catch { }
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

            // Save official PaystackTransferOutflow payout record
            var outflowRecord = new PaystackTransferOutflow
            {
                TransferCode = transferRes.TransferCode,
                TransferReference = transferRes.TransferCode,
                Amount = availableFund,
                RecipientCode = adminSetting.PaystackRecipientCode,
                AccountName = adminSetting.OutflowAccountName,
                AccountNumber = adminSetting.OutflowAccountNumber,
                BankName = adminSetting.OutflowBankName,
                Status = "PendingApproval",
                InitiatedBy = User.Identity.Name,
                DateInitiated = DateTime.UtcNow.AddHours(1),
                TransactionIdsJson = string.Join(",", clearedTxns.Select(x => x.TransactionId)),
                TransactionsCount = clearedTxns.Count,
                PaystackMessage = transferRes.Message ?? "Transfer initiated with Paystack. Awaiting final delivery / approval."
            };
            db.PaystackTransferOutflows.Add(outflowRecord);

            // Batch-mark all cleared transactions as transferred out
            foreach (var txn in clearedTxns)
            {
                txn.IsAdminTransferred = true;
                txn.DateTransferred = DateTime.UtcNow.AddHours(1);
                txn.TransferredBy = $"{User.Identity.Name} (Ref: {transferRes.TransferCode})";
            }

            await db.SaveChangesAsync();

            TempData["Success"] = $"Automated Paystack Transfer Initiated! ₦{availableFund:N2} sent to {adminSetting.OutflowAccountName} ({adminSetting.OutflowBankName}). Ref Code: {transferRes.TransferCode}. Status: Awaiting Paystack Delivery/Approval.";
            return RedirectToAction("PaystackOutflow", new { tab = "transfers" });
        }

        // POST: Adminpanel/Transactions/VerifyTransferOutflow
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> VerifyTransferOutflow(int id, string returnTab = "transfers")
        {
            var transfer = await db.PaystackTransferOutflows.FindAsync(id);
            if (transfer == null)
            {
                TempData["error"] = "Transfer record not found.";
                return RedirectToAction("PaystackOutflow", new { tab = returnTab });
            }

            var service = new PaystackTransferService();
            var res = await service.FetchTransferDetailsAsync(transfer.TransferCode);

            if (res.Success)
            {
                transfer.Status = res.Status; // e.g. success, pending, otp, failed, reversed
                transfer.DateVerified = DateTime.UtcNow.AddHours(1);
                transfer.VerifiedBy = User.Identity.Name;
                transfer.PaystackMessage = $"Status: {res.Status}. {res.Message} {(string.IsNullOrEmpty(res.FailReason) ? "" : $"Reason: {res.FailReason}")}".Trim();

                // Update real destination account directly from Paystack's official recipient data
                if (!string.IsNullOrWhiteSpace(res.RecipientName))
                {
                    transfer.AccountName = res.RecipientName;
                }
                if (!string.IsNullOrWhiteSpace(res.BankName))
                {
                    transfer.BankName = res.BankName;
                }
                if (!string.IsNullOrWhiteSpace(res.AccountNumber))
                {
                    transfer.AccountNumber = res.AccountNumber;
                }

                await db.SaveChangesAsync();

                if (res.Status.Equals("success", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["Success"] = $"Paystack Transfer #{transfer.TransferCode} verified: SUCCESS! Funds delivered to recipient bank.";
                }
                else if (res.Status.Equals("otp", StringComparison.OrdinalIgnoreCase) || res.Status.Equals("pending", StringComparison.OrdinalIgnoreCase))
                {
                    TempData["warning"] = $"Paystack Transfer #{transfer.TransferCode} status is '{res.Status.ToUpper()}'. Awaiting authorization/approval on Paystack.";
                }
                else
                {
                    TempData["error"] = $"Paystack Transfer #{transfer.TransferCode} status is '{res.Status.ToUpper()}'. Reason: {res.FailReason ?? res.Message}. Click 'Rollback' to restore customer funds.";
                }
            }
            else
            {
                transfer.DateVerified = DateTime.UtcNow.AddHours(1);
                transfer.VerifiedBy = User.Identity.Name;
                transfer.PaystackMessage = res.Message;
                await db.SaveChangesAsync();
                TempData["error"] = $"Paystack Query: {res.Message}";
            }

            return RedirectToAction("PaystackOutflow", new { tab = returnTab });
        }

        // POST: Adminpanel/Transactions/RollbackTransferOutflow
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> RollbackTransferOutflow(int id, string adminPassword, string returnTab = "transfers")
        {
            try
            {
                if (string.IsNullOrWhiteSpace(adminPassword))
                {
                    TempData["error"] = "Security Check Failed: Please enter your admin password.";
                    return RedirectToAction("PaystackOutflow", new { tab = returnTab });
                }

                ApplicationUser currentUser = null;
                if (UserManager != null)
                {
                    string userId = UserManager.GetUserId(User);
                    if (!string.IsNullOrEmpty(userId))
                    {
                        currentUser = await UserManager.FindByIdAsync(userId);
                    }
                    if (currentUser == null && !string.IsNullOrWhiteSpace(User.Identity?.Name))
                    {
                        currentUser = await UserManager.FindByNameAsync(User.Identity.Name);
                    }
                    if (currentUser == null && !string.IsNullOrWhiteSpace(User.Identity?.Name))
                    {
                        currentUser = await UserManager.FindByEmailAsync(User.Identity.Name);
                    }
                }

                if (currentUser != null && UserManager != null)
                {
                    bool isPasswordValid = await UserManager.CheckPasswordAsync(currentUser, adminPassword);
                    if (!isPasswordValid)
                    {
                        TempData["error"] = "Security Check Failed: Invalid Admin Password. Rollback cancelled.";
                        return RedirectToAction("PaystackOutflow", new { tab = returnTab });
                    }
                }

                var transfer = await db.PaystackTransferOutflows.FindAsync(id);
                if (transfer == null)
                {
                    TempData["error"] = "Transfer record not found.";
                    return RedirectToAction("PaystackOutflow", new { tab = returnTab });
                }

                if (transfer.IsRolledBack)
                {
                    TempData["warning"] = $"Transfer #{transfer.TransferCode} was already rolled back previously.";
                    return RedirectToAction("PaystackOutflow", new { tab = returnTab });
                }

                // Restore the linked customer transactions back to available pool
                int restoredCount = 0;
                if (!string.IsNullOrWhiteSpace(transfer.TransactionIdsJson))
                {
                    var ids = transfer.TransactionIdsJson
                        .Split(new[] { ',', '[', ']', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => int.TryParse(s, out var v) ? v : 0)
                        .Where(v => v > 0)
                        .ToList();

                    var linkedTxns = await db.Transactions.Where(t => ids.Contains(t.TransactionId)).ToListAsync();
                    foreach (var txn in linkedTxns)
                    {
                        txn.IsAdminTransferred = false;
                        txn.DateTransferred = null;
                        txn.TransferredBy = null;
                        restoredCount++;
                    }
                }

                transfer.IsRolledBack = true;
                transfer.DateRolledBack = DateTime.UtcNow.AddHours(1);
                transfer.RolledBackBy = User.Identity?.Name ?? "Admin";
                transfer.Status = "RolledBack";
                transfer.PaystackMessage = $"Rolled back by {User.Identity?.Name ?? "Admin"} on {DateTime.UtcNow.AddHours(1):yyyy-MM-dd HH:mm}. Funds restored to Available pool.";

                await db.SaveChangesAsync();

                TempData["Success"] = $"Transfer #{transfer.TransferCode} successfully ROLLED BACK! ₦{transfer.Amount:N2} restored to Available Funds across {restoredCount} customer transaction(s).";
                return RedirectToAction("PaystackOutflow", new { tab = "available" });
            }
            catch (Exception ex)
            {
                TempData["error"] = $"Rollback Operation Error: {ex.Message}";
                return RedirectToAction("PaystackOutflow", new { tab = returnTab });
            }
        }

        // GET: Adminpanel/Transactions/GetTransferOutflowDetailsAjax
        [HttpGet]
        public async Task<IActionResult> GetTransferOutflowDetailsAjax(int id)
        {
            var transfer = await db.PaystackTransferOutflows.FindAsync(id);
            if (transfer == null)
            {
                return Json(new { success = false, message = "Transfer record not found." });
            }

            var linkedTxns = new List<object>();
            if (!string.IsNullOrWhiteSpace(transfer.TransactionIdsJson))
            {
                var ids = transfer.TransactionIdsJson
                    .Split(new[] { ',', '[', ']', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(s => int.TryParse(s, out var v) ? v : 0)
                    .Where(v => v > 0)
                    .ToList();

                var txns = await db.Transactions
                    .Include(t => t.User)
                    .Where(t => ids.Contains(t.TransactionId))
                    .OrderByDescending(t => t.TransactionId)
                    .ToListAsync();

                foreach (var t in txns)
                {
                    linkedTxns.Add(new
                    {
                        transactionId = t.TransactionId,
                        clientName = GeneralServices.ClientName(t.ClientId),
                        userName = GeneralServices.UserName(t.UserId),
                        reference = t.TransactionReference ?? "N/A",
                        amount = (t.AmountPaid.HasValue && t.AmountPaid > 0 ? t.AmountPaid.Value : t.Amount).ToString("N2"),
                        units = t.Units.ToString("N0"),
                        date = t.DateApproved.HasValue ? t.DateApproved.Value.ToString("yyyy-MM-dd HH:mm") : t.DateCreated.ToString("yyyy-MM-dd")
                    });
                }
            }

            return Json(new
            {
                success = true,
                id = transfer.Id,
                transferCode = transfer.TransferCode,
                amount = transfer.Amount.ToString("N2"),
                recipientName = transfer.AccountName ?? "Corporate Outflow Account",
                bankName = transfer.BankName ?? "N/A",
                accountNumber = transfer.AccountNumber ?? "N/A",
                status = transfer.Status ?? "PendingApproval",
                initiatedBy = transfer.InitiatedBy ?? "Admin",
                dateInitiated = transfer.DateInitiated.ToString("dd MMM yyyy, hh:mm tt"),
                dateVerified = transfer.DateVerified.HasValue ? transfer.DateVerified.Value.ToString("dd MMM yyyy, hh:mm tt") : "Not Verified",
                verifiedBy = transfer.VerifiedBy ?? "N/A",
                paystackMessage = string.IsNullOrWhiteSpace(transfer.PaystackMessage) ? "No message recorded." : transfer.PaystackMessage,
                isRolledBack = transfer.IsRolledBack,
                dateRolledBack = transfer.DateRolledBack.HasValue ? transfer.DateRolledBack.Value.ToString("dd MMM yyyy, hh:mm tt") : null,
                rolledBackBy = transfer.RolledBackBy,
                depositsCount = linkedTxns.Count,
                transactions = linkedTxns
            });
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