using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Exwhyzee.Messaging.Core.Models;
using X.PagedList;
using X.PagedList.Extensions;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Area("Adminpanel")]
    public class EmailLogsController : Controller
    {
        private ApplicationDbContext db => HttpContext.RequestServices.GetService<ApplicationDbContext>();

        // GET: Adminpanel/EmailLogs
        public async Task<IActionResult> Index(string searchString, string statusFilter, int? page)
        {
            var query = db.EmailLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();
                query = query.Where(e => e.RecipientEmail.Contains(searchString) 
                                      || e.Subject.Contains(searchString) 
                                      || e.ErrorMessage.Contains(searchString));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                if (statusFilter == "Success") query = query.Where(e => e.IsSuccess);
                else if (statusFilter == "Failed") query = query.Where(e => !e.IsSuccess);
            }

            ViewBag.TotalCount = await db.EmailLogs.CountAsync();
            ViewBag.SuccessCount = await db.EmailLogs.CountAsync(e => e.IsSuccess);
            ViewBag.FailedCount = await db.EmailLogs.CountAsync(e => !e.IsSuccess);

            ViewBag.SearchString = searchString;
            ViewBag.StatusFilter = statusFilter;

            int pageNumber = page ?? 1;
            int pageSize = 25;

            var logs = query.OrderByDescending(e => e.DateSent).ToPagedList(pageNumber, pageSize);
            return View(logs);
        }

        // GET: Adminpanel/EmailLogs/GetBody/5
        [HttpGet]
        public async Task<IActionResult> GetBody(int id)
        {
            try
            {
                var log = await db.EmailLogs.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
                if (log == null) return Json(new { success = false, error = "Email log not found." });
                return Json(new { success = true, subject = log.Subject, recipient = log.RecipientEmail, date = log.DateSent.ToString("g"), html = log.BodyHtml });
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
                    TempData["success"] = logsToDelete.Count + " email log entries deleted successfully.";
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
                    TempData["Message"] = $"No email logs older than {days} days found.";
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
