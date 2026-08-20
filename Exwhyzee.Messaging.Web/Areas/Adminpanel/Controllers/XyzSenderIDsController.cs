using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Models;

using X.PagedList;
using X.PagedList.Extensions;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    [Area("Adminpanel")]
    public class XyzSenderIDsController : Controller
    {
        private ApplicationDbContext db => HttpContext.RequestServices.GetService<ApplicationDbContext>();
        private IClientService _clientService = new ClientService();

        public XyzSenderIDsController(IClientService clientService)
        {
            _clientService = clientService ?? new ClientService();
        }

        // GET: Adminpanel/XyzSenderIDs
        public async Task<ActionResult> Index(string searchString, string statusFilter, int? page)
        {
            int pageNumber = page ?? 1;
            int pageSize = 25;

            var query = db.XyzSenderIDs
                .Include(x => x.Client)
                .ThenInclude(c => c.User)
                .AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchString))
            {
                string term = searchString.Trim().ToLower();
                query = query.Where(x => x.SenderId.ToLower().Contains(term)
                    || (x.Client != null && (x.Client.Surname.ToLower().Contains(term) || x.Client.FirstName.ToLower().Contains(term)))
                    || (x.Client != null && x.Client.User != null && x.Client.User.UserName.ToLower().Contains(term)));
            }

            if (!string.IsNullOrWhiteSpace(statusFilter))
            {
                string sf = statusFilter.Trim().ToLower();
                if (sf == "approved")
                {
                    query = query.Where(x => (x.XYZ_status != null && (x.XYZ_status.ToLower() == "approved" || x.XYZ_status.ToLower().Contains("active")))
                        || (x.XYZ_msg != null && x.XYZ_msg.ToLower().Contains("approved")));
                }
                else if (sf == "pending")
                {
                    query = query.Where(x => (x.XYZ_status == null || x.XYZ_status.ToLower().Contains("pending"))
                        && (x.XYZ_msg == null || x.XYZ_msg.ToLower().Contains("pending") || x.XYZ_msg.ToLower().Contains("promotional") || x.XYZ_msg.ToLower().Contains("review")));
                }
                else if (sf == "not_registered")
                {
                    query = query.Where(x => (x.XYZ_status != null && (x.XYZ_status.ToLower().Contains("exist") || x.XYZ_status.ToLower().Contains("not")))
                        || (x.XYZ_msg != null && (x.XYZ_msg.ToLower().Contains("exist") || x.XYZ_msg.ToLower().Contains("not verified"))));
                }
                else if (sf == "rejected")
                {
                    query = query.Where(x => (x.XYZ_status != null && (x.XYZ_status.ToLower().Contains("fail") || x.XYZ_status.ToLower().Contains("rej") || x.XYZ_status.ToLower().Contains("deni")))
                        || (x.XYZ_msg != null && (x.XYZ_msg.ToLower().Contains("fail") || x.XYZ_msg.ToLower().Contains("rej") || x.XYZ_msg.ToLower().Contains("deni"))));
                }
            }

            var pagedSenderIds = query.OrderByDescending(x => x.Id).ToPagedList(pageNumber, pageSize);
            ViewBag.SearchString = searchString;
            ViewBag.StatusFilter = statusFilter;

            return View(pagedSenderIds);
        }

        public async Task<ActionResult> SenderByUser(string userId)
        {
            var client = await _clientService.GetClientDetailsByUserId(userId);
            if (client != null)
            {
                ViewBag.name = $"{client.Surname} {client.FirstName} {client.OtherNames}".Trim();
            }
            return View(await _clientService.GetAllSenderIdById(userId));
        }

        // POST: Adminpanel/XyzSenderIDs/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(string SenderId, string Message)
        {
            if (string.IsNullOrWhiteSpace(SenderId))
            {
                TempData["error"] = "Please enter a valid alphanumeric Sender ID.";
                return RedirectToAction("Index");
            }

            SenderId = SenderId.Trim().ToUpper();
            if (SenderId.Length > 11)
            {
                TempData["error"] = "Sender ID cannot exceed 11 characters.";
                return RedirectToAction("Index");
            }

            string userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var response = await _clientService.AddSender(userId, SenderId, Message ?? "Account verification and transactional alerts.");
            TempData["success"] = response;
            return RedirectToAction("Index");
        }

        // POST: Adminpanel/XyzSenderIDs/CheckSenderIdAjax
        [HttpPost]
        public async Task<IActionResult> CheckSenderIdAjax(string senderId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(senderId))
                {
                    return Json(new { success = false, message = "Invalid Sender ID" });
                }

                var status = await _clientService.VerifySender(senderId);
                return Json(new { success = true, senderId = senderId, status = status });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Adminpanel/XyzSenderIDs/DeleteSenderAjax
        [HttpPost]
        public async Task<IActionResult> DeleteSenderAjax(string senderId)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(senderId))
                {
                    return Json(new { success = false, message = "Invalid Sender ID" });
                }

                var sid = await db.XyzSenderIDs.FirstOrDefaultAsync(x => x.SenderId == senderId);
                if (sid != null)
                {
                    db.XyzSenderIDs.Remove(sid);
                    await db.SaveChangesAsync();
                    return Json(new { success = true, senderId = senderId });
                }
                return Json(new { success = false, message = "Sender ID not found." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db?.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
