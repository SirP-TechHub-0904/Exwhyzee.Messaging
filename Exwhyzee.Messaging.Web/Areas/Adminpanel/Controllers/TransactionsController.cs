using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Services;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Dtos;
using Exwhyzee.Messaging.Core.Paystack;
using Exwhyzee.Messaging.Core.Paystack.Models;
using X.PagedList;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using System.Linq;
using System.Net;
using System.Text;
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

        #region UPGRADED TRANSACTIONS LEDGER STUDIO & EXPORT

        private async Task<List<TransactionLedgerDto>> BuildTransactionLedgerAsync()
        {
            var txns = await db.Transactions.AsNoTracking()
                .OrderByDescending(t => t.TransactionId)
                .ToListAsync();

            var clients = await db.Clients.AsNoTracking()
                .Select(c => new Client
                {
                    ClientId = c.ClientId,
                    UserId = c.UserId,
                    FirstName = c.FirstName,
                    Surname = c.Surname,
                    OtherNames = c.OtherNames
                })
                .ToListAsync();

            var users = await db.Users.AsNoTracking()
                .Select(u => new ApplicationUser
                {
                    Id = u.Id,
                    UserName = u.UserName,
                    Email = u.Email,
                    PhoneNumber = u.PhoneNumber
                })
                .ToListAsync();

            var clientById = clients.ToDictionary(c => c.ClientId, c => c);
            var userById = users.ToDictionary(u => u.Id, u => u);

            var list = new List<TransactionLedgerDto>(txns.Count);
            foreach (var t in txns)
            {
                clientById.TryGetValue(t.ClientId, out var c);
                ApplicationUser u = null;
                if (!string.IsNullOrEmpty(t.UserId))
                {
                    userById.TryGetValue(t.UserId, out u);
                }
                if (u == null && c != null && !string.IsNullOrEmpty(c.UserId))
                {
                    userById.TryGetValue(c.UserId, out u);
                }

                string fullName = c != null
                    ? $"{c.Surname} {c.FirstName} {c.OtherNames}".Trim()
                    : "";
                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = u?.UserName ?? $"Client #{t.ClientId}";
                }

                string sourceCategory;
                if (!string.IsNullOrEmpty(t.PaymentSource) && t.PaymentSource.Equals("Paystack", StringComparison.OrdinalIgnoreCase))
                {
                    sourceCategory = "Paystack";
                }
                else if (t.TransactionType == TransactionType.OnlinePayment)
                {
                    sourceCategory = "Paystack";
                }
                else if (t.TransactionType == TransactionType.BankDeposit)
                {
                    sourceCategory = "BankDeposit";
                }
                else if (t.TransactionType == TransactionType.ByVoucher)
                {
                    sourceCategory = "Voucher";
                }
                else
                {
                    sourceCategory = "AdminTopUp";
                }

                decimal effectiveAmount = t.AmountPaid.HasValue && t.AmountPaid.Value > 0 ? t.AmountPaid.Value : t.Amount;

                list.Add(new TransactionLedgerDto
                {
                    TransactionId = t.TransactionId,
                    ClientId = t.ClientId,
                    UserId = u?.Id ?? t.UserId ?? c?.UserId,
                    ClientFullName = fullName,
                    Username = u?.UserName ?? "",
                    Email = u?.Email ?? "",
                    PhoneNumber = u?.PhoneNumber ?? "",
                    Units = t.Units,
                    Amount = t.Amount,
                    EffectiveAmountPaid = effectiveAmount,
                    TransactionType = t.TransactionType,
                    PaymentSource = t.PaymentSource,
                    SourceCategory = sourceCategory,
                    Status = t.Status,
                    DateCreated = t.DateCreated,
                    DateApproved = t.DateApproved,
                    ApprovedBy = t.ApprovedBy,
                    TransactionReference = t.TransactionReference,
                    GatewayResponse = t.GatewayResponse,
                    Note = t.Note
                });
            }

            return list;
        }

        private TransactionLedgerStatsDto ComputeTransactionStats(List<TransactionLedgerDto> list)
        {
            var approved = list.Where(x => x.Status == TransactionStatus.Approved || x.DateApproved.HasValue).ToList();
            var pending = list.Where(x => x.Status == TransactionStatus.Pending && !x.DateApproved.HasValue).ToList();
            var failed = list.Where(x => x.Status != TransactionStatus.Approved && x.Status != TransactionStatus.Pending && !x.DateApproved.HasValue).ToList();

            var paystack = list.Where(x => x.SourceCategory == "Paystack").ToList();
            var bank = list.Where(x => x.SourceCategory == "BankDeposit").ToList();
            var admin = list.Where(x => x.SourceCategory == "AdminTopUp" || x.SourceCategory == "Voucher").ToList();

            return new TransactionLedgerStatsDto
            {
                TotalTransactions = list.Count,
                ApprovedCount = approved.Count,
                ApprovedRevenueNgn = approved.Sum(x => x.EffectiveAmountPaid),
                ApprovedUnitsCredited = approved.Sum(x => x.Units),
                PendingCount = pending.Count,
                PendingAmountNgn = pending.Sum(x => x.EffectiveAmountPaid),
                FailedOrCancelledCount = failed.Count,
                PaystackCount = paystack.Count,
                PaystackRevenueNgn = paystack.Where(x => x.Status == TransactionStatus.Approved || x.DateApproved.HasValue).Sum(x => x.EffectiveAmountPaid),
                BankDepositCount = bank.Count,
                BankDepositRevenueNgn = bank.Where(x => x.Status == TransactionStatus.Approved || x.DateApproved.HasValue).Sum(x => x.EffectiveAmountPaid),
                AdminTopUpCount = admin.Count,
                AdminTopUpUnits = admin.Where(x => x.Status == TransactionStatus.Approved || x.DateApproved.HasValue).Sum(x => x.Units)
            };
        }

        private List<TransactionLedgerDto> ApplyTransactionFiltersAndSort(
            List<TransactionLedgerDto> source,
            string searchString,
            string cardFilter,
            string statusFilter,
            string sourceFilter,
            decimal? minAmount,
            decimal? maxAmount,
            decimal? minUnits,
            decimal? maxUnits,
            DateTime? dateFrom,
            DateTime? dateTo,
            string datePreset,
            string sortBy)
        {
            IEnumerable<TransactionLedgerDto> q = source;

            // Quick Date Preset
            if (!string.IsNullOrWhiteSpace(datePreset))
            {
                var today = DateTime.UtcNow.AddHours(1).Date;
                switch (datePreset.Trim().ToLowerInvariant())
                {
                    case "today":
                        q = q.Where(x => x.EffectiveDate.Date == today);
                        break;
                    case "7d":
                        q = q.Where(x => x.EffectiveDate.Date >= today.AddDays(-7));
                        break;
                    case "month":
                        q = q.Where(x => x.EffectiveDate.Year == today.Year && x.EffectiveDate.Month == today.Month);
                        break;
                }
            }

            // Card Quick Filter
            if (!string.IsNullOrWhiteSpace(cardFilter))
            {
                switch (cardFilter.Trim().ToLowerInvariant())
                {
                    case "approved":
                        q = q.Where(x => x.Status == TransactionStatus.Approved || x.DateApproved.HasValue);
                        break;
                    case "pending":
                        q = q.Where(x => x.Status == TransactionStatus.Pending && !x.DateApproved.HasValue);
                        break;
                    case "failed":
                        q = q.Where(x => x.Status != TransactionStatus.Approved && x.Status != TransactionStatus.Pending && !x.DateApproved.HasValue);
                        break;
                    case "paystack":
                        q = q.Where(x => x.SourceCategory == "Paystack");
                        break;
                    case "bank":
                        q = q.Where(x => x.SourceCategory == "BankDeposit");
                        break;
                    case "admin":
                        q = q.Where(x => x.SourceCategory == "AdminTopUp" || x.SourceCategory == "Voucher");
                        break;
                }
            }

            // Status Filter
            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                if (Enum.TryParse<TransactionStatus>(statusFilter.Trim(), true, out var parsedStatus))
                {
                    if (parsedStatus == TransactionStatus.Approved)
                        q = q.Where(x => x.Status == TransactionStatus.Approved || x.DateApproved.HasValue);
                    else if (parsedStatus == TransactionStatus.Pending)
                        q = q.Where(x => x.Status == TransactionStatus.Pending && !x.DateApproved.HasValue);
                    else
                        q = q.Where(x => x.Status == parsedStatus);
                }
            }

            // Source Filter
            if (!string.IsNullOrWhiteSpace(sourceFilter))
            {
                q = q.Where(x => x.SourceCategory.Equals(sourceFilter.Trim(), StringComparison.OrdinalIgnoreCase));
            }

            // Search String
            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string s = searchString.Trim().TrimStart('#');
                q = q.Where(x =>
                    x.TransactionId.ToString().Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (x.ClientFullName != null && x.ClientFullName.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Username != null && x.Username.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Email != null && x.Email.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    (x.TransactionReference != null && x.TransactionReference.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    (x.ApprovedBy != null && x.ApprovedBy.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    (x.PaymentSource != null && x.PaymentSource.Contains(s, StringComparison.OrdinalIgnoreCase)));
            }

            // Amount Range
            if (minAmount.HasValue) q = q.Where(x => x.EffectiveAmountPaid >= minAmount.Value);
            if (maxAmount.HasValue) q = q.Where(x => x.EffectiveAmountPaid <= maxAmount.Value);

            // Units Range
            if (minUnits.HasValue) q = q.Where(x => x.Units >= minUnits.Value);
            if (maxUnits.HasValue) q = q.Where(x => x.Units <= maxUnits.Value);

            // Custom Date Range
            if (dateFrom.HasValue) q = q.Where(x => x.EffectiveDate.Date >= dateFrom.Value.Date);
            if (dateTo.HasValue) q = q.Where(x => x.EffectiveDate.Date <= dateTo.Value.Date);

            // Sorting
            string sort = (sortBy ?? "date_desc").Trim().ToLowerInvariant();
            q = sort switch
            {
                "date_asc" => q.OrderBy(x => x.EffectiveDate).ThenBy(x => x.TransactionId),
                "amount_desc" => q.OrderByDescending(x => x.EffectiveAmountPaid).ThenByDescending(x => x.TransactionId),
                "amount_asc" => q.OrderBy(x => x.EffectiveAmountPaid).ThenByDescending(x => x.TransactionId),
                "units_desc" => q.OrderByDescending(x => x.Units).ThenByDescending(x => x.TransactionId),
                "units_asc" => q.OrderBy(x => x.Units).ThenByDescending(x => x.TransactionId),
                "client_asc" => q.OrderBy(x => x.ClientFullName).ThenByDescending(x => x.TransactionId),
                _ => q.OrderByDescending(x => x.EffectiveDate).ThenByDescending(x => x.TransactionId)
            };

            return q.ToList();
        }

        // GET: Adminpanel/Transactions
        public async Task<ActionResult> Index(
            string searchString,
            string cardFilter,
            string statusFilter,
            string sourceFilter,
            decimal? minAmount,
            decimal? maxAmount,
            decimal? minUnits,
            decimal? maxUnits,
            DateTime? dateFrom,
            DateTime? dateTo,
            string datePreset,
            string sortBy = "date_desc",
            int pageSize = 20,
            int? page = 1)
        {
            var allTxns = await BuildTransactionLedgerAsync();
            var filtered = ApplyTransactionFiltersAndSort(
                allTxns, searchString, cardFilter, statusFilter, sourceFilter,
                minAmount, maxAmount, minUnits, maxUnits, dateFrom, dateTo, datePreset, sortBy);

            var stats = ComputeTransactionStats(filtered);
            ViewBag.Stats = stats;

            ViewBag.SearchString = searchString;
            ViewBag.CardFilter = cardFilter;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.SourceFilter = sourceFilter;
            ViewBag.MinAmount = minAmount;
            ViewBag.MaxAmount = maxAmount;
            ViewBag.MinUnits = minUnits;
            ViewBag.MaxUnits = maxUnits;
            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.DatePreset = datePreset;
            ViewBag.SortBy = sortBy;
            ViewBag.PageSize = pageSize;

            int pageNumber = Math.Max(1, page ?? 1);
            int effectivePageSize = pageSize <= 0 ? Math.Max(1, filtered.Count) : pageSize;

            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = (int)Math.Ceiling((double)filtered.Count / effectivePageSize);
            ViewBag.TotalFilteredCount = filtered.Count;

            return View(filtered.ToPagedList(pageNumber, effectivePageSize));
        }

        [HttpGet]
        public async Task<IActionResult> ExportTransactions(
            string format = "excel",
            string searchString = null,
            string cardFilter = null,
            string statusFilter = null,
            string sourceFilter = null,
            decimal? minAmount = null,
            decimal? maxAmount = null,
            decimal? minUnits = null,
            decimal? maxUnits = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string datePreset = null,
            string sortBy = "date_desc")
        {
            var allTxns = await BuildTransactionLedgerAsync();
            var filtered = ApplyTransactionFiltersAndSort(
                allTxns, searchString, cardFilter, statusFilter, sourceFilter,
                minAmount, maxAmount, minUnits, maxUnits, dateFrom, dateTo, datePreset, sortBy);

            var stats = ComputeTransactionStats(filtered);
            string timestamp = DateTime.UtcNow.AddHours(1).ToString("yyyyMMdd_HHmm");

            if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.ReportTitle = "Platform Transactions Ledger Report";
                ViewBag.GeneratedAt = DateTime.UtcNow.AddHours(1).ToString("dd MMM yyyy, hh:mm tt");
                ViewBag.Stats = stats;
                return View("ExportTransactionsReport", filtered);
            }

            var sb = new StringBuilder();
            sb.AppendLine("<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns:x='urn:schemas-microsoft-com:office:excel' xmlns='http://www.w3.org/TR/REC-html40'>");
            sb.AppendLine("<head><meta charset='utf-8'><style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; font-size: 11px; }");
            sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
            sb.AppendLine("th { background-color: #0F172A; color: #FFFFFF; font-weight: bold; padding: 8px; border: 1px solid #CBD5E1; text-align: left; }");
            sb.AppendLine("td { padding: 6px 8px; border: 1px solid #CBD5E1; }");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine($"<h2>Exwhyzee Bulk SMS - Platform Transactions Ledger</h2>");
            sb.AppendLine($"<p><strong>Generated:</strong> {DateTime.UtcNow.AddHours(1):dd MMM yyyy, hh:mm tt} | <strong>Total Records:</strong> {filtered.Count:N0} | <strong>Approved Revenue:</strong> NGN {stats.ApprovedRevenueNgn:N2} | <strong>Approved Units:</strong> {stats.ApprovedUnitsCredited:N0}</p>");
            sb.AppendLine("<table><thead><tr>");
            sb.AppendLine("<th>S/N</th><th>Trans ID</th><th>Client Name</th><th>Username</th><th>Email</th><th>Source</th><th>Amount Paid (NGN)</th><th>Units</th><th>Status</th><th>Reference</th><th>Date</th>");
            sb.AppendLine("</tr></thead><tbody>");

            int sn = 1;
            foreach (var item in filtered)
            {
                string st = (item.Status == TransactionStatus.Approved || item.DateApproved.HasValue) ? "Approved" : item.Status.ToString();
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{sn++}</td>");
                sb.AppendLine($"<td>#{item.TransactionId}</td>");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(item.ClientFullName)}</td>");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(item.Username)}</td>");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(item.Email)}</td>");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(item.SourceCategory)}</td>");
                sb.AppendLine($"<td>{item.EffectiveAmountPaid:N2}</td>");
                sb.AppendLine($"<td>{item.Units:N0}</td>");
                sb.AppendLine($"<td>{st}</td>");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(item.TransactionReference ?? "-")}</td>");
                sb.AppendLine($"<td>{item.EffectiveDate:yyyy-MM-dd HH:mm}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table></body></html>");
            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());

            if (string.Equals(format, "word", StringComparison.OrdinalIgnoreCase))
            {
                return File(bytes, "application/msword", $"XYZSMS_Transactions_{timestamp}.doc");
            }
            return File(bytes, "application/vnd.ms-excel", $"XYZSMS_Transactions_{timestamp}.xls");
        }

        // GET: Adminpanel/Transactions/Pending
        public ActionResult Pending()
        {
            return RedirectToAction("Index", new { statusFilter = "Pending" });
        }

        public async Task<ActionResult> ClientUnits()
        {
            return RedirectToAction("ManageUnits", "ManageUsers", new { area = "Adminpanel" });
        }

        #endregion

        #region SMS PROFIT & LOSS ANALYTICS STUDIO

        private static decimal ParseDecimalSafe(string raw)
        {
            if (string.IsNullOrWhiteSpace(raw)) return 0m;
            var cleaned = new string(raw.Where(c => char.IsDigit(c) || c == '.' || c == '-').ToArray());
            if (decimal.TryParse(cleaned, NumberStyles.Any, CultureInfo.InvariantCulture, out var val))
            {
                return val;
            }
            return 0m;
        }

        private async Task<(List<ProfitAndLossItemDto> Items, decimal PricePerUnit, decimal FlatUnitsPerSms, string LatestGatewayBalance)> BuildProfitAndLossListAsync()
        {
            var adminSetting = await db.AdminSettings.AsNoTracking().FirstOrDefaultAsync();
            decimal pricePerUnit = adminSetting?.PricePerUnit ?? 1.0m;
            decimal flatUnitsPerSms = adminSetting != null && adminSetting.FlatUnitsPerSms > 0 ? adminSetting.FlatUnitsPerSms : 6.95m;

            // Project ONLY messages that have Kudi gateway cost recorded (excludes old pre-Kudi rows without Response_cost)
            var messages = await db.Messages.AsNoTracking()
                .Where(m => m.Response_cost != null && m.Response_cost != "")
                .OrderByDescending(m => m.MessageId)
                .Select(m => new
                {
                    m.MessageId,
                    m.UserId,
                    m.SenderId,
                    m.Response_page,
                    MessagePreview = m.MessageContent != null
                        ? (m.MessageContent.Length > 65 ? m.MessageContent.Substring(0, 65) + "..." : m.MessageContent)
                        : "",
                    m.DeliveredDate,
                    m.Scheduleddate,
                    m.Status,
                    m.UnitsUsed,
                    m.Response_cost,
                    m.Response_balance,
                    m.Response_status
                })
                .ToListAsync();

            var users = await db.Users.AsNoTracking()
                .Select(u => new { u.Id, u.UserName, u.Email })
                .ToListAsync();

            var clients = await db.Clients.AsNoTracking()
                .Where(c => c.UserId != null)
                .Select(c => new { c.ClientId, c.UserId, c.FirstName, c.Surname, c.OtherNames })
                .ToListAsync();

            var userById = users.ToDictionary(u => u.Id, u => u);
            var clientByUserId = clients
                .GroupBy(c => c.UserId)
                .ToDictionary(g => g.Key, g => g.First());

            string latestBalance = null;
            var items = new List<ProfitAndLossItemDto>(messages.Count);

            foreach (var m in messages)
            {
                bool hasCost = !string.IsNullOrWhiteSpace(m.Response_cost);
                decimal gatewayCost = ParseDecimalSafe(m.Response_cost);
                decimal? gatewayBal = !string.IsNullOrWhiteSpace(m.Response_balance) ? ParseDecimalSafe(m.Response_balance) : (decimal?)null;
                string formattedBal = gatewayBal.HasValue ? gatewayBal.Value.ToString("N2") : null;

                if (latestBalance == null && !string.IsNullOrWhiteSpace(formattedBal))
                {
                    latestBalance = formattedBal;
                }

                string userName = "";
                string userEmail = "";
                int clientId = 0;
                string fullName = "";

                if (!string.IsNullOrEmpty(m.UserId))
                {
                    if (userById.TryGetValue(m.UserId, out var u))
                    {
                        userName = u.UserName ?? "";
                        userEmail = u.Email ?? "";
                    }
                    if (clientByUserId.TryGetValue(m.UserId, out var c))
                    {
                        clientId = c.ClientId;
                        fullName = $"{c.Surname} {c.FirstName} {c.OtherNames}".Trim();
                    }
                }

                if (string.IsNullOrWhiteSpace(fullName))
                {
                    fullName = !string.IsNullOrWhiteSpace(userName) ? userName : "Unknown User";
                }

                int pages = m.Response_page > 0 ? m.Response_page : 1;
                decimal divisor = pages * flatUnitsPerSms;
                decimal basisUnits = m.UnitsUsed > 0 ? m.UnitsUsed : gatewayCost;
                int recipientCount = basisUnits > 0 && divisor > 0
                    ? Math.Max(1, (int)Math.Round(basisUnits / divisor))
                    : 1;

                items.Add(new ProfitAndLossItemDto
                {
                    MessageId = m.MessageId,
                    UserId = m.UserId,
                    ClientId = clientId,
                    Username = userName,
                    ClientFullName = fullName,
                    Email = userEmail,
                    SenderId = m.SenderId ?? "N/A",
                    RecipientsCount = recipientCount,
                    PageCount = pages,
                    MessagePreview = m.MessagePreview ?? "",
                    SentDate = m.DeliveredDate ?? m.Scheduleddate ?? DateTime.UtcNow,
                    Status = m.Status,
                    ClientUnitsCharged = Math.Round(m.UnitsUsed, 2),
                    GatewayCostUnits = Math.Round(gatewayCost, 2),
                    HasGatewayCost = hasCost,
                    GatewayBalanceAfter = gatewayBal,
                    RawGatewayBalance = formattedBal,
                    ResponseStatus = m.Response_status ?? m.Status.ToString()
                });
            }

            return (items, pricePerUnit, flatUnitsPerSms, latestBalance ?? "N/A");
        }

        private (DateTime? From, DateTime? To) ResolveDatePreset(string datePreset, DateTime? dateFrom, DateTime? dateTo)
        {
            if (string.IsNullOrWhiteSpace(datePreset)) return (dateFrom, dateTo);
            var today = DateTime.UtcNow.AddHours(1).Date;
            return datePreset.Trim().ToLowerInvariant() switch
            {
                "today" => (today, today),
                "yesterday" => (today.AddDays(-1), today.AddDays(-1)),
                "7d" => (today.AddDays(-7), today),
                "30d" => (today.AddDays(-30), today),
                "month" => (new DateTime(today.Year, today.Month, 1), today),
                "last_month" => (
                    new DateTime(today.AddMonths(-1).Year, today.AddMonths(-1).Month, 1),
                    new DateTime(today.Year, today.Month, 1).AddDays(-1)),
                _ => (dateFrom, dateTo)
            };
        }

        private List<ProfitAndLossItemDto> ApplyProfitAndLossFilters(
            List<ProfitAndLossItemDto> source,
            string searchString,
            string cardFilter,
            string outcomeFilter,
            DateTime? dateFrom,
            DateTime? dateTo,
            string datePreset,
            string sortBy)
        {
            IEnumerable<ProfitAndLossItemDto> q = source;

            var (effFrom, effTo) = ResolveDatePreset(datePreset, dateFrom, dateTo);
            if (effFrom.HasValue) q = q.Where(x => x.SentDate.Date >= effFrom.Value.Date);
            if (effTo.HasValue) q = q.Where(x => x.SentDate.Date <= effTo.Value.Date);

            string activeOutcome = !string.IsNullOrWhiteSpace(cardFilter) ? cardFilter : outcomeFilter;
            if (!string.IsNullOrWhiteSpace(activeOutcome))
            {
                switch (activeOutcome.Trim().ToLowerInvariant())
                {
                    case "profit":
                        q = q.Where(x => x.NetProfitUnits > 0);
                        break;
                    case "breakeven":
                        q = q.Where(x => x.NetProfitUnits == 0);
                        break;
                    case "loss":
                        q = q.Where(x => x.NetProfitUnits < 0);
                        break;
                    case "with_cost":
                        q = q.Where(x => x.HasGatewayCost && x.GatewayCostUnits > 0);
                        break;
                }
            }

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string s = searchString.Trim();
                q = q.Where(x =>
                    x.MessageId.ToString().Contains(s, StringComparison.OrdinalIgnoreCase) ||
                    (x.ClientFullName != null && x.ClientFullName.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Username != null && x.Username.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    (x.Email != null && x.Email.Contains(s, StringComparison.OrdinalIgnoreCase)) ||
                    (x.SenderId != null && x.SenderId.Contains(s, StringComparison.OrdinalIgnoreCase)));
            }

            string sort = (sortBy ?? "date_desc").Trim().ToLowerInvariant();
            q = sort switch
            {
                "date_asc" => q.OrderBy(x => x.SentDate).ThenBy(x => x.MessageId),
                "profit_desc" => q.OrderByDescending(x => x.NetProfitUnits).ThenByDescending(x => x.MessageId),
                "profit_asc" => q.OrderBy(x => x.NetProfitUnits).ThenByDescending(x => x.MessageId),
                "charged_desc" => q.OrderByDescending(x => x.ClientUnitsCharged).ThenByDescending(x => x.MessageId),
                "cost_desc" => q.OrderByDescending(x => x.GatewayCostUnits).ThenByDescending(x => x.MessageId),
                _ => q.OrderByDescending(x => x.SentDate).ThenByDescending(x => x.MessageId)
            };

            return q.ToList();
        }

        private ProfitAndLossStatsDto ComputeProfitAndLossStats(List<ProfitAndLossItemDto> list, decimal pricePerUnit, decimal flatUnitsPerSms, string latestGatewayBalance)
        {
            var profitable = list.Where(x => x.NetProfitUnits > 0).ToList();
            var breakEven = list.Where(x => x.NetProfitUnits == 0).ToList();
            var lossOnly = list.Where(x => x.NetProfitUnits < 0).ToList();

            return new ProfitAndLossStatsDto
            {
                TotalBroadcasts = list.Count,
                TotalRecipients = list.Sum(x => x.RecipientsCount),
                TotalClientUnitsCharged = list.Sum(x => x.ClientUnitsCharged),
                TotalGatewayCostUnits = list.Sum(x => x.GatewayCostUnits),
                ProfitableBroadcastsCount = profitable.Count,
                ProfitableUnitsSum = profitable.Sum(x => x.NetProfitUnits),
                BreakEvenBroadcastsCount = breakEven.Count,
                BreakEvenUnitsVolume = breakEven.Sum(x => x.ClientUnitsCharged),
                LossBroadcastsCount = lossOnly.Count,
                LossOrBreakEvenBroadcastsCount = lossOnly.Count,
                LossUnitsSum = lossOnly.Sum(x => x.NetProfitUnits),
                LatestGatewayBalance = latestGatewayBalance,
                PricePerUnit = pricePerUnit,
                FlatUnitsPerSms = flatUnitsPerSms
            };
        }

        // GET: Adminpanel/Transactions/ProfitAndLoss
        [HttpGet]
        public async Task<IActionResult> ProfitAndLoss(
            string searchString,
            string cardFilter,
            string outcomeFilter,
            DateTime? dateFrom,
            DateTime? dateTo,
            string datePreset,
            string sortBy = "date_desc",
            int pageSize = 20,
            int? page = 1)
        {
            var (allItems, pricePerUnit, flatUnitsPerSms, latestBalance) = await BuildProfitAndLossListAsync();
            var (effFrom, effTo) = ResolveDatePreset(datePreset, dateFrom, dateTo);
            var filtered = ApplyProfitAndLossFilters(allItems, searchString, cardFilter, outcomeFilter, dateFrom, dateTo, datePreset, sortBy);
            var stats = ComputeProfitAndLossStats(filtered, pricePerUnit, flatUnitsPerSms, latestBalance);

            ViewBag.Stats = stats;
            ViewBag.SearchString = searchString;
            ViewBag.CardFilter = cardFilter;
            ViewBag.OutcomeFilter = outcomeFilter;
            ViewBag.DateFrom = effFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = effTo?.ToString("yyyy-MM-dd");
            ViewBag.DatePreset = datePreset;
            ViewBag.SortBy = sortBy;
            ViewBag.PageSize = pageSize;

            int pageNumber = Math.Max(1, page ?? 1);
            int effectivePageSize = pageSize <= 0 ? Math.Max(1, filtered.Count) : pageSize;

            ViewBag.CurrentPage = pageNumber;
            ViewBag.TotalPages = (int)Math.Ceiling((double)filtered.Count / effectivePageSize);
            ViewBag.TotalFilteredCount = filtered.Count;

            return View(filtered.ToPagedList(pageNumber, effectivePageSize));
        }

        [HttpGet]
        public async Task<IActionResult> ExportProfitAndLoss(
            string format = "excel",
            string searchString = null,
            string cardFilter = null,
            string outcomeFilter = null,
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string datePreset = null,
            string sortBy = "date_desc")
        {
            var (allItems, pricePerUnit, flatUnitsPerSms, latestBalance) = await BuildProfitAndLossListAsync();
            var filtered = ApplyProfitAndLossFilters(allItems, searchString, cardFilter, outcomeFilter, dateFrom, dateTo, datePreset, sortBy);
            var stats = ComputeProfitAndLossStats(filtered, pricePerUnit, flatUnitsPerSms, latestBalance);
            string timestamp = DateTime.UtcNow.AddHours(1).ToString("yyyyMMdd_HHmm");

            if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.ReportTitle = "SMS Gateway Profit & Loss Analytics Report";
                ViewBag.GeneratedAt = DateTime.UtcNow.AddHours(1).ToString("dd MMM yyyy, hh:mm tt");
                ViewBag.Stats = stats;
                return View("ExportProfitAndLossReport", filtered);
            }

            var sb = new StringBuilder();
            sb.AppendLine("<html xmlns:o='urn:schemas-microsoft-com:office:office' xmlns:w='urn:schemas-microsoft-com:office:word' xmlns:x='urn:schemas-microsoft-com:office:excel' xmlns='http://www.w3.org/TR/REC-html40'>");
            sb.AppendLine("<head><meta charset='utf-8'><style>");
            sb.AppendLine("body { font-family: Arial, sans-serif; font-size: 11px; }");
            sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
            sb.AppendLine("th { background-color: #0F172A; color: #FFFFFF; font-weight: bold; padding: 8px; border: 1px solid #CBD5E1; text-align: left; }");
            sb.AppendLine("td { padding: 6px 8px; border: 1px solid #CBD5E1; }");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine($"<h2>Exwhyzee Bulk SMS - SMS Profit &amp; Loss Analytics Report</h2>");
            sb.AppendLine($"<p><strong>Generated:</strong> {DateTime.UtcNow.AddHours(1):dd MMM yyyy, hh:mm tt} | <strong>Broadcasts:</strong> {stats.TotalBroadcasts:N0} | <strong>Client Charged:</strong> {stats.TotalClientUnitsCharged:N2} Units | <strong>Gateway Cost:</strong> {stats.TotalGatewayCostUnits:N2} | <strong>Net Profit:</strong> {stats.TotalNetProfitUnits:N2} Units ({stats.OverallMarginPercent}%)</p>");
            sb.AppendLine("<table><thead><tr>");
            sb.AppendLine("<th>S/N</th><th>Msg ID</th><th>Date</th><th>Client</th><th>Sender ID</th><th>Recipients</th><th>Charged (Units)</th><th>Kudi Cost</th><th>Net Profit/Loss</th><th>Margin %</th><th>Gateway Balance After</th>");
            sb.AppendLine("</tr></thead><tbody>");

            int sn = 1;
            foreach (var item in filtered)
            {
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{sn++}</td>");
                sb.AppendLine($"<td>#{item.MessageId}</td>");
                sb.AppendLine($"<td>{item.SentDate:yyyy-MM-dd HH:mm}</td>");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(item.ClientFullName)} (@{WebUtility.HtmlEncode(item.Username)})</td>");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(item.SenderId)}</td>");
                sb.AppendLine($"<td>{item.RecipientsCount:N0}</td>");
                sb.AppendLine($"<td>{item.ClientUnitsCharged:N2}</td>");
                sb.AppendLine($"<td>{item.GatewayCostUnits:N2}</td>");
                sb.AppendLine($"<td>{(item.NetProfitUnits >= 0 ? "+" : "")}{item.NetProfitUnits:N2}</td>");
                sb.AppendLine($"<td>{item.MarginPercent}%</td>");
                sb.AppendLine($"<td>{WebUtility.HtmlEncode(item.RawGatewayBalance ?? "-")}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table></body></html>");
            byte[] bytes = Encoding.UTF8.GetBytes(sb.ToString());

            if (string.Equals(format, "word", StringComparison.OrdinalIgnoreCase))
            {
                return File(bytes, "application/msword", $"XYZSMS_ProfitAndLoss_{timestamp}.doc");
            }
            return File(bytes, "application/vnd.ms-excel", $"XYZSMS_ProfitAndLoss_{timestamp}.xls");
        }

        #endregion

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

                    foreach (var txnId in ids)
                    {
                        var txn = await db.Transactions.FindAsync(txnId);
                        if (txn != null)
                        {
                            txn.IsAdminTransferred = false;
                            txn.DateTransferred = null;
                            txn.TransferredBy = null;
                            restoredCount++;
                        }
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

                foreach (var txnId in ids)
                {
                    var t = await db.Transactions.Include(x => x.User).FirstOrDefaultAsync(x => x.TransactionId == txnId);
                    if (t != null)
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