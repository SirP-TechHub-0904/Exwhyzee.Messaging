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
                    TempData["success"] = $"Purged {count} email logs older than {days} days.";
                }
                else
                {
                    TempData["Message"] = $"No email logs older than {days} days found.";
                }
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
