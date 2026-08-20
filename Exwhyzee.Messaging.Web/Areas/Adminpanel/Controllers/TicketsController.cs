using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Services;
using Exwhyzee.Messaging.Web.Controllers;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    [Area("Adminpanel")]
    public class TicketsController : BaseController
    {
        private readonly ITicketService _ticketService = new TicketService();

        // GET: Adminpanel/Tickets
        public async Task<IActionResult> Index(TicketStatus? status, TicketPriority? priority, string search)
        {
            var tickets = await _ticketService.GetAllTicketsAsync(status, priority, search);
            ViewBag.CurrentStatus = status;
            ViewBag.CurrentPriority = priority;
            ViewBag.SearchTerm = search;

            return View(tickets);
        }

        // GET: Adminpanel/Tickets/Details/5
        public async Task<IActionResult> Details(int id)
        {
            if (id <= 0) return RedirectToAction("Index");

            var ticket = await _ticketService.GetTicketByIdAsync(id);
            if (ticket == null)
            {
                TempData["error"] = "Support ticket not found.";
                return RedirectToAction("Index");
            }

            return View(ticket);
        }

        // POST: Adminpanel/Tickets/PostReply
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostReply(int ticketId, string message)
        {
            if (ticketId <= 0 || string.IsNullOrWhiteSpace(message))
            {
                TempData["error"] = "Reply message cannot be empty.";
                return RedirectToAction("Details", new { id = ticketId });
            }

            var ticket = await _ticketService.GetTicketByIdAsync(ticketId);
            if (ticket == null)
            {
                TempData["error"] = "Support ticket not found.";
                return RedirectToAction("Index");
            }

            string adminUserId = UserManager.GetUserId(User);
            string adminStaffName = User.Identity.Name ?? "Admin Staff";

            await _ticketService.AddResponseAsync(ticketId, message.Trim(), adminUserId, adminStaffName, isAdminReply: true, adminRepliedBy: adminStaffName);

            TempData["success"] = $"Response posted successfully by {adminStaffName} and emailed to client.";
            return RedirectToAction("Details", new { id = ticketId });
        }

        // POST: Adminpanel/Tickets/UpdateStatus
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateStatus(int ticketId, TicketStatus status)
        {
            if (ticketId <= 0) return RedirectToAction("Index");

            bool updated = await _ticketService.UpdateStatusAsync(ticketId, status);
            if (updated)
            {
                TempData["success"] = $"Ticket status updated to {status}.";
            }
            else
            {
                TempData["error"] = "Failed to update ticket status.";
            }

            return RedirectToAction("Details", new { id = ticketId });
        }
    }
}
