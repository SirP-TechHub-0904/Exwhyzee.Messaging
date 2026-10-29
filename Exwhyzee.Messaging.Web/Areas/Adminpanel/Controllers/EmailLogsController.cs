using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Dtos;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Area("Adminpanel")]
    public class EmailLogsController : Controller
    {
        private ApplicationDbContext db => HttpContext.RequestServices.GetService<ApplicationDbContext>();

        private static string ClassifyEmailCategory(string subject)
        {
            string s = (subject ?? "").ToLowerInvariant();
            if (s.Contains("otp") || s.Contains("verification") || s.Contains("verify") || s.Contains("password") || s.Contains("reset") || s.Contains("security") || s.Contains("2fa") || s.Contains("code"))
                return "OTP & Security";
            if (s.Contains("payment") || s.Contains("receipt") || s.Contains("credit") || s.Contains("wallet") || s.Contains("unit") || s.Contains("top-up") || s.Contains("topup") || s.Contains("invoice") || s.Contains("transaction"))
                return "Payment Receipt";
            if (s.Contains("welcome") || s.Contains("register") || s.Contains("account") || s.Contains("kyc") || s.Contains("onboard"))
                return "Welcome & Account";
            if (s.Contains("ticket") || s.Contains("support") || s.Contains("help") || s.Contains("reply"))
                return "Support Ticket";
            return "System Alert";
        }

        private async Task<(List<EmailLogItemDto> Items, EmailLogStatsDto Stats)> BuildFilteredEmailLogsAsync(
            string search,
            string statusFilter,
            string categoryFilter,
            string cardFilter,
            DateTime? dateFrom,
            DateTime? dateTo,
            string sortOrder)
        {
            // Project lightweight fields (exclude BodyHtml from list query for speed)
            var rawLogs = await db.EmailLogs.AsNoTracking()
                .Select(e => new
                {
                    e.Id,
                    e.RecipientEmail,
                    e.Subject,
                    e.DateSent,
                    e.IsSuccess,
                    e.ErrorMessage
                })
                .ToListAsync();

            // Load users & clients to match recipient emails to User Dossiers
            var users = await db.Users.AsNoTracking()
                .Where(u => u.Email != null)
                .Select(u => new { u.Id, u.UserName, u.Email })
                .ToListAsync();

            var clients = await db.Clients.AsNoTracking()
                .Where(c => c.UserId != null)
                .Select(c => new { c.UserId, c.FirstName, c.Surname, c.OtherNames })
                .ToListAsync();

            var clientByUserId = clients
                .GroupBy(c => c.UserId)
                .ToDictionary(g => g.Key, g => g.First());

            var userByEmail = new Dictionary<string, (string UserId, string UserName, string FullName)>(StringComparer.OrdinalIgnoreCase);
            foreach (var u in users)
            {
                string emailKey = u.Email.Trim();
                if (!userByEmail.ContainsKey(emailKey))
                {
                    string fullName = u.UserName;
                    if (clientByUserId.TryGetValue(u.Id, out var c))
                    {
                        string built = $"{c.Surname} {c.FirstName} {c.OtherNames}".Trim();
                        if (!string.IsNullOrWhiteSpace(built)) fullName = built;
                    }
                    userByEmail[emailKey] = (u.Id, u.UserName, fullName);
                }
            }

            var items = new List<EmailLogItemDto>(rawLogs.Count);
            foreach (var log in rawLogs)
            {
                string firstEmail = (log.RecipientEmail ?? "")
                    .Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                    .Select(x => x.Trim())
                    .FirstOrDefault() ?? "";

                string matchedUserId = null;
                string matchedUserName = null;
                string matchedFullName = null;
                if (!string.IsNullOrEmpty(firstEmail) && userByEmail.TryGetValue(firstEmail, out var matched))
                {
                    matchedUserId = matched.UserId;
                    matchedUserName = matched.UserName;
                    matchedFullName = matched.FullName;
                }

                items.Add(new EmailLogItemDto
                {
                    Id = log.Id,
                    RecipientEmail = log.RecipientEmail ?? "",
                    Subject = log.Subject ?? "(No Subject)",
                    DateSent = log.DateSent,
                    IsSuccess = log.IsSuccess,
                    ErrorMessage = log.ErrorMessage,
                    Category = ClassifyEmailCategory(log.Subject),
                    MatchedUserId = matchedUserId,
                    MatchedUserName = matchedUserName,
                    MatchedFullName = matchedFullName
                });
            }

            IEnumerable<EmailLogItemDto> filtered = items;

            if (!string.IsNullOrWhiteSpace(search))
            {
                string s = search.Trim().ToLowerInvariant();
                string cleanId = s.TrimStart('#');
                filtered = filtered.Where(x =>
                    x.Id.ToString().Contains(cleanId) ||
                    (x.RecipientEmail ?? "").ToLowerInvariant().Contains(s) ||
                    (x.Subject ?? "").ToLowerInvariant().Contains(s) ||
                    (x.ErrorMessage ?? "").ToLowerInvariant().Contains(s) ||
                    (x.MatchedUserName ?? "").ToLowerInvariant().Contains(s) ||
                    (x.MatchedFullName ?? "").ToLowerInvariant().Contains(s));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter) && !statusFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                if (statusFilter.Equals("Success", StringComparison.OrdinalIgnoreCase) || statusFilter.Equals("Delivered", StringComparison.OrdinalIgnoreCase))
                    filtered = filtered.Where(x => x.IsSuccess);
                else if (statusFilter.Equals("Failed", StringComparison.OrdinalIgnoreCase))
                    filtered = filtered.Where(x => !x.IsSuccess);
            }

            if (!string.IsNullOrWhiteSpace(categoryFilter) && !categoryFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                filtered = filtered.Where(x => string.Equals(x.Category, categoryFilter, StringComparison.OrdinalIgnoreCase));
            }

            if (dateFrom.HasValue)
            {
                var fromDate = dateFrom.Value.Date;
                filtered = filtered.Where(x => x.DateSent >= fromDate);
            }

            if (dateTo.HasValue)
            {
                var toDate = dateTo.Value.Date.AddDays(1).AddTicks(-1);
                filtered = filtered.Where(x => x.DateSent <= toDate);
            }

            if (!string.IsNullOrWhiteSpace(cardFilter) && !cardFilter.Equals("all", StringComparison.OrdinalIgnoreCase))
            {
                var todayUtc = DateTime.UtcNow.Date;
                switch (cardFilter.ToLowerInvariant())
                {
                    case "delivered":
                        filtered = filtered.Where(x => x.IsSuccess);
                        break;
                    case "failed":
                        filtered = filtered.Where(x => !x.IsSuccess);
                        break;
                    case "today":
                        filtered = filtered.Where(x => x.DateSent.Date == todayUtc);
                        break;
                    case "otp":
                        filtered = filtered.Where(x => x.Category == "OTP & Security");
                        break;
                    case "receipt":
                        filtered = filtered.Where(x => x.Category == "Payment Receipt");
                        break;
                    case "welcome":
                        filtered = filtered.Where(x => x.Category == "Welcome & Account");
                        break;
                }
            }

            var resultList = filtered.ToList();
            var todayDate = DateTime.UtcNow.Date;

            int total = resultList.Count;
            int delivered = resultList.Count(x => x.IsSuccess);
            int failed = resultList.Count(x => !x.IsSuccess);

            var stats = new EmailLogStatsDto
            {
                TotalFiltered = total,
                DeliveredCount = delivered,
                FailedCount = failed,
                DeliveryRatePercent = total > 0 ? Math.Round((decimal)delivered / total * 100m, 1) : 0m,
                SentTodayCount = resultList.Count(x => x.DateSent.Date == todayDate),
                OtpSecurityCount = resultList.Count(x => x.Category == "OTP & Security"),
                PaymentReceiptCount = resultList.Count(x => x.Category == "Payment Receipt"),
                WelcomeNoticeCount = resultList.Count(x => x.Category == "Welcome & Account"),
                UniqueRecipientsCount = resultList
                    .Select(x => (x.RecipientEmail ?? "").Trim().ToLowerInvariant())
                    .Where(x => !string.IsNullOrEmpty(x))
                    .Distinct()
                    .Count()
            };

            resultList = (sortOrder ?? "date_desc") switch
            {
                "date_asc" => resultList.OrderBy(x => x.DateSent).ThenBy(x => x.Id).ToList(),
                "recipient_asc" => resultList.OrderBy(x => x.RecipientEmail).ThenByDescending(x => x.DateSent).ToList(),
                "subject_asc" => resultList.OrderBy(x => x.Subject).ThenByDescending(x => x.DateSent).ToList(),
                "failed_first" => resultList.OrderBy(x => x.IsSuccess).ThenByDescending(x => x.DateSent).ToList(),
                _ => resultList.OrderByDescending(x => x.DateSent).ThenByDescending(x => x.Id).ToList()
            };

            return (resultList, stats);
        }

        // GET: Adminpanel/EmailLogs
        public async Task<IActionResult> Index(
            string search = null,
            string searchString = null,
            string statusFilter = "all",
            string categoryFilter = "all",
            string cardFilter = "all",
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string sortOrder = "date_desc",
            int page = 1,
            int pageSize = 20)
        {
            string effectiveSearch = !string.IsNullOrWhiteSpace(search) ? search : searchString;

            var (allItems, stats) = await BuildFilteredEmailLogsAsync(
                effectiveSearch, statusFilter, categoryFilter, cardFilter, dateFrom, dateTo, sortOrder);

            int totalCount = allItems.Count;
            if (page < 1) page = 1;
            int effectivePageSize = pageSize <= 0 ? (totalCount > 0 ? totalCount : 1) : pageSize;
            int totalPages = (int)Math.Ceiling((double)totalCount / effectivePageSize);
            if (totalPages < 1) totalPages = 1;
            if (page > totalPages) page = totalPages;

            var pagedItems = pageSize <= 0
                ? allItems
                : allItems.Skip((page - 1) * effectivePageSize).Take(effectivePageSize).ToList();

            ViewBag.Stats = stats;
            ViewBag.Search = effectiveSearch;
            ViewBag.StatusFilter = statusFilter;
            ViewBag.CategoryFilter = categoryFilter;
            ViewBag.CardFilter = cardFilter;
            ViewBag.DateFrom = dateFrom?.ToString("yyyy-MM-dd");
            ViewBag.DateTo = dateTo?.ToString("yyyy-MM-dd");
            ViewBag.SortOrder = sortOrder;
            ViewBag.CurrentPage = page;
            ViewBag.PageSize = pageSize;
            ViewBag.TotalPages = totalPages;
            ViewBag.TotalCount = totalCount;

            return View(pagedItems);
        }

        // GET: Adminpanel/EmailLogs/ExportEmailLogs
        [HttpGet]
        public async Task<IActionResult> ExportEmailLogs(
            string format = "excel",
            string search = null,
            string statusFilter = "all",
            string categoryFilter = "all",
            string cardFilter = "all",
            DateTime? dateFrom = null,
            DateTime? dateTo = null,
            string sortOrder = "date_desc")
        {
            var (items, stats) = await BuildFilteredEmailLogsAsync(
                search, statusFilter, categoryFilter, cardFilter, dateFrom, dateTo, sortOrder);

            if (string.Equals(format, "pdf", StringComparison.OrdinalIgnoreCase))
            {
                ViewBag.Stats = stats;
                ViewBag.GeneratedAt = DateTime.UtcNow.AddHours(1);
                return View("ExportEmailLogsReport", items);
            }

            var sb = new StringBuilder();
            sb.AppendLine("<html><head><meta charset='utf-8'/><style>");
            sb.AppendLine("body { font-family: Calibri, Arial, sans-serif; font-size: 11pt; }");
            sb.AppendLine("h2 { color: #0f172a; margin-bottom: 4px; }");
            sb.AppendLine(".meta { color: #475569; font-size: 10pt; margin-bottom: 14px; }");
            sb.AppendLine("table { border-collapse: collapse; width: 100%; }");
            sb.AppendLine("th { background: #0f172a; color: #ffffff; font-weight: bold; padding: 8px; border: 1px solid #cbd5e1; text-align: left; }");
            sb.AppendLine("td { padding: 7px 8px; border: 1px solid #cbd5e1; vertical-align: top; }");
            sb.AppendLine("</style></head><body>");
            sb.AppendLine("<h2>Exwhyzee Messaging — Outbound Email Audit Report</h2>");
            sb.AppendLine($"<div class='meta'>Generated: {DateTime.UtcNow.AddHours(1):dd MMM yyyy, hh:mm tt} | Total Filtered: <strong>{stats.TotalFiltered:N0}</strong> | Delivered: <strong>{stats.DeliveredCount:N0} ({stats.DeliveryRatePercent:N1}%)</strong> | Failed: <strong>{stats.FailedCount:N0}</strong></div>");
            sb.AppendLine("<table><thead><tr>");
            sb.AppendLine("<th>#</th><th>Log ID</th><th>Date &amp; Time (UTC)</th><th>Recipient Email</th><th>Matched User</th><th>Category</th><th>Subject</th><th>Status</th><th>Error Details</th>");
            sb.AppendLine("</tr></thead><tbody>");

            int sn = 1;
            foreach (var item in items)
            {
                string statusText = item.IsSuccess ? "DELIVERED" : "FAILED";
                string statusColor = item.IsSuccess ? "#15803d" : "#dc2626";
                sb.AppendLine("<tr>");
                sb.AppendLine($"<td>{sn++}</td>");
                sb.AppendLine($"<td>#{item.Id}</td>");
                sb.AppendLine($"<td>{item.DateSent:yyyy-MM-dd HH:mm:ss}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.RecipientEmail)}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.MatchedUserName ?? "-")}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.Category)}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.Subject)}</td>");
                sb.AppendLine($"<td style='font-weight:bold;color:{statusColor};'>{statusText}</td>");
                sb.AppendLine($"<td>{System.Net.WebUtility.HtmlEncode(item.ErrorMessage ?? "-")}</td>");
                sb.AppendLine("</tr>");
            }

            sb.AppendLine("</tbody></table></body></html>");
            var bytes = Encoding.UTF8.GetBytes(sb.ToString());
            string timestamp = DateTime.UtcNow.AddHours(1).ToString("yyyyMMdd_HHmm");

            if (string.Equals(format, "word", StringComparison.OrdinalIgnoreCase))
            {
                return File(bytes, "application/msword", $"EmailLogs_Report_{timestamp}.doc");
            }

            return File(bytes, "application/vnd.ms-excel", $"EmailLogs_Report_{timestamp}.xls");
        }

        // GET: Adminpanel/EmailLogs/GetBody/5
        [HttpGet]
        public async Task<IActionResult> GetBody(int id)
        {
            try
            {
                var log = await db.EmailLogs.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
                if (log == null) return Json(new { success = false, error = "Email log not found." });
                return Json(new { success = true, subject = log.Subject, recipient = log.RecipientEmail, date = log.DateSent.ToString("dd MMM yyyy, hh:mm:ss tt"), html = log.BodyHtml });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, error = "Failed to load email body: " + ex.Message });
            }
        }

        // POST: Adminpanel/EmailLogs/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var log = await db.EmailLogs.FindAsync(id);
            if (log != null)
            {
                db.EmailLogs.Remove(log);
                await db.SaveChangesAsync();
                TempData["success"] = "Email log entry deleted successfully.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Adminpanel/EmailLogs/BatchDelete
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> BatchDelete(int[] selectedIds)
        {
            if (selectedIds != null && selectedIds.Length > 0)
            {
                var idList = selectedIds.ToList();
                var logsToDelete = await db.EmailLogs.Where(e => idList.Contains(e.Id)).ToListAsync();
                if (logsToDelete.Any())
                {
                    db.EmailLogs.RemoveRange(logsToDelete);
                    await db.SaveChangesAsync();
                    TempData["success"] = $"{logsToDelete.Count:N0} email log entries deleted successfully.";
                }
            }
            else
            {
                TempData["error"] = "No email log items were selected for deletion.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Adminpanel/EmailLogs/PurgeOlderThan
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PurgeOlderThan(int days)
        {
            if (days > 0)
            {
                var cutoff = DateTime.UtcNow.AddDays(-days);
                var oldLogs = await db.EmailLogs.Where(e => e.DateSent < cutoff).ToListAsync();
                int count = oldLogs.Count;

                if (count > 0)
                {
                    db.EmailLogs.RemoveRange(oldLogs);
                    await db.SaveChangesAsync();
                    TempData["success"] = $"Purged {count:N0} email log(s) older than {days} days.";
                }
                else
                {
                    TempData["error"] = $"No email logs older than {days} days were found.";
                }
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Adminpanel/EmailLogs/SendTestEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SendTestEmail(string testRecipientEmail)
        {
            if (string.IsNullOrWhiteSpace(testRecipientEmail))
            {
                TempData["error"] = "Please provide a valid test recipient email address.";
                return RedirectToAction(nameof(Index));
            }

            var zeptoMail = HttpContext.RequestServices.GetService<Exwhyzee.Messaging.Core.Services.IZeptoMailService>();
            if (zeptoMail == null)
            {
                TempData["error"] = "ZeptoMail service is not registered.";
                return RedirectToAction(nameof(Index));
            }

            try
            {
                bool success = await zeptoMail.SendTestEmailAsync(testRecipientEmail.Trim());
                if (success)
                {
                    TempData["success"] = $"Test email sent successfully to {testRecipientEmail}! Check the logs below.";
                }
                else
                {
                    TempData["error"] = $"Failed to send test email to {testRecipientEmail}. See the error log below for details.";
                }
            }
            catch (Exception ex)
            {
                TempData["error"] = $"Error sending test email: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        // POST: Adminpanel/EmailLogs/ResendEmail
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ResendEmail(int id)
        {
            var log = await db.EmailLogs.FindAsync(id);
            if (log == null)
            {
                TempData["error"] = "Email log entry not found.";
                return RedirectToAction(nameof(Index));
            }

            var zeptoMail = HttpContext.RequestServices.GetService<Exwhyzee.Messaging.Core.Services.IZeptoMailService>();
            if (zeptoMail == null)
            {
                TempData["error"] = "ZeptoMail service not available.";
                return RedirectToAction(nameof(Index));
            }

            var recipients = log.RecipientEmail.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).Select(e => e.Trim()).ToList();
            bool sent = await zeptoMail.SendEmailWithCustomRecipientAsync(log.BodyHtml, recipients, log.Subject);

            if (sent)
            {
                TempData["success"] = $"Email '{log.Subject}' was successfully re-dispatched to {log.RecipientEmail}.";
            }
            else
            {
                TempData["error"] = $"Failed to re-send email to {log.RecipientEmail}. Check latest log for failure reason.";
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
