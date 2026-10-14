using System;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Services;
using X.PagedList.Extensions;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Area("Adminpanel")]
    public class ErrorLogsController : Controller
    {
        private ApplicationDbContext db => HttpContext.RequestServices.GetService<ApplicationDbContext>();
        private IErrorLogService errorLogService => HttpContext.RequestServices.GetService<IErrorLogService>() ?? new ErrorLogService();

        // GET: Adminpanel/ErrorLogs
        public async Task<IActionResult> Index(string searchString, string levelFilter, int? page)
        {
            var query = db.SystemErrorLogs.AsNoTracking().AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                searchString = searchString.Trim();
                query = query.Where(e => e.Source.Contains(searchString)
                                      || e.Message.Contains(searchString)
                                      || e.InnerErrorMessage.Contains(searchString)
                                      || e.UserName.Contains(searchString)
                                      || e.RequestUrl.Contains(searchString));
            }

            if (!string.IsNullOrWhiteSpace(levelFilter))
            {
                query = query.Where(e => e.LogLevel == levelFilter);
            }

            ViewBag.TotalCount = await db.SystemErrorLogs.CountAsync();
            ViewBag.CriticalCount = await db.SystemErrorLogs.CountAsync(e => e.LogLevel == "Critical" || e.LogLevel == "Fatal");
            ViewBag.EmailedCount = await db.SystemErrorLogs.CountAsync(e => e.EmailAlertSent);
            ViewBag.UnresolvedCount = await db.SystemErrorLogs.CountAsync(e => !e.IsResolved);

            ViewBag.SearchString = searchString;
            ViewBag.LevelFilter = levelFilter;

            int pageNumber = page ?? 1;
            int pageSize = 25;

            var logs = query.OrderByDescending(e => e.TimestampUtc).ToPagedList(pageNumber, pageSize);
            return View(logs);
        }

        // GET: Adminpanel/ErrorLogs/GetDetails/5
        [HttpGet]
        public async Task<IActionResult> GetDetails(int id)
        {
            try
            {
                var log = await db.SystemErrorLogs.AsNoTracking().FirstOrDefaultAsync(e => e.Id == id);
                if (log == null) return NotFound(new { success = false, message = "Log record not found." });

                return Json(new
                {
                    success = true,
                    id = log.Id,
                    timestamp = log.TimestampUtc.ToString("yyyy-MM-dd HH:mm:ss UTC"),
                    source = log.Source,
                    level = log.LogLevel,
                    message = log.Message,
                    innerError = log.InnerErrorMessage,
                    stackTrace = log.StackTrace,
                    user = log.UserName ?? log.UserId ?? "N/A",
                    url = log.RequestUrl,
                    method = log.RequestMethod,
                    ip = log.ClientIp,
                    payload = log.PayloadData,
                    emailSent = log.EmailAlertSent,
                    emailError = log.EmailAlertError,
                    dbError = log.DbLoggingError,
                    isResolved = log.IsResolved,
                    resolvedDate = log.ResolvedDate?.ToString("yyyy-MM-dd HH:mm:ss"),
                    resolvedBy = log.ResolvedBy,
                    notes = log.ResolutionNotes
                });
            }
            catch (Exception ex)
            {
                return BadRequest(new { success = false, message = ex.Message });
            }
        }

        // POST: Adminpanel/ErrorLogs/MarkResolved/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> MarkResolved(int id, string notes)
        {
            var log = await db.SystemErrorLogs.FirstOrDefaultAsync(e => e.Id == id);
            if (log != null)
            {
                log.IsResolved = true;
                log.ResolvedDate = DateTime.UtcNow;
                log.ResolvedBy = User.Identity.Name;
                log.ResolutionNotes = notes;
                await db.SaveChangesAsync();
                TempData["success"] = $"Error log #{id} has been marked as resolved.";
            }
            return RedirectToAction(nameof(Index));
        }

        // POST: Adminpanel/ErrorLogs/TestAlert
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> TestAlert()
        {
            try
            {
                // Trigger a synthetic test exception to verify DB logging and email alert to exwhyzeesms@gmail.com
                var testEx = new InvalidOperationException("This is a live diagnostic test exception to verify system error logging and emergency email dispatch.");
                
                string root = await errorLogService.LogErrorAndAlertAsync(
                    exception: testEx,
                    source: "Adminpanel.ErrorLogs.TestAlert",
                    logLevel: "Critical",
                    userName: User.Identity.Name,
                    requestUrl: Request.Path.Value,
                    requestMethod: "POST",
                    payload: new { Note = "Manual diagnostic test initiated from Admin Portal", InitiatedBy = User.Identity.Name }
                );

                TempData["success"] = "Diagnostic test executed! Error logged to database and dispatched via email to exwhyzeesms@gmail.com.";
            }
            catch (Exception ex)
            {
                TempData["error"] = "Diagnostic test failed: " + ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }
    }
}
