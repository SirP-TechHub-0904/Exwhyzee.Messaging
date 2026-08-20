using System;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Services;
using Exwhyzee.Messaging.Web.Controllers;

namespace Exwhyzee.Messaging.Web.Areas.ClientPanel.Controllers
{
    [Authorize(Roles = "Client")]
    [Area("ClientPanel")]
    public class TicketsController : BaseController
    {
        private readonly ITicketService _ticketService = new TicketService();
        private readonly GoogleReCaptchaService _reCaptchaService = new GoogleReCaptchaService();

        // GET: ClientPanel/Tickets
        public async Task<IActionResult> Index()
        {
            string userId = UserManager.GetUserId(User);
            var tickets = await _ticketService.GetTicketsForUserAsync(userId);
            return View(tickets);
        }

        // GET: ClientPanel/Tickets/Create
        public async Task<IActionResult> Create()
        {
            var userId = UserManager.GetUserId(User);
            var user = await UserManager.FindByIdAsync(userId);

            var ticket = new SupportTicket
            {
                SenderName = user?.UserName ?? "Client",
                SenderEmail = user?.Email ?? "",
                SenderPhone = user?.PhoneNumber ?? ""
            };

            ViewBag.ReCaptchaSiteKey = _reCaptchaService.SiteKey;
            return View(ticket);
        }

        // POST: ClientPanel/Tickets/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SupportTicket model)
        {
            string recaptchaToken = Request.Form["g-recaptcha-response"].ToString();
            bool isCaptchaValid = await _reCaptchaService.VerifyTokenAsync(recaptchaToken);
            if (!isCaptchaValid)
            {
                TempData["error"] = "Google reCAPTCHA bot validation failed. Please check the checkbox and try again.";
                ViewBag.ReCaptchaSiteKey = _reCaptchaService.SiteKey;
                return View(model);
            }

            ModelState.Remove("UserId");
            ModelState.Remove("TicketNumber");
            ModelState.Remove("SenderName");
            ModelState.Remove("SenderEmail");
            ModelState.Remove("SenderPhone");

            if (!ModelState.IsValid)
            {
                TempData["error"] = "Please fill in all required ticket fields correctly.";
                ViewBag.ReCaptchaSiteKey = _reCaptchaService.SiteKey;
                return View(model);
            }

            string userId = UserManager.GetUserId(User);
            var user = await UserManager.FindByIdAsync(userId);

            model.UserId = userId;
            model.SenderName = user?.UserName ?? model.SenderName;
            model.SenderEmail = user?.Email ?? model.SenderEmail;
            model.SenderPhone = user?.PhoneNumber ?? model.SenderPhone;

            var ticket = await _ticketService.CreateTicketAsync(model);
            TempData["success"] = $"Support Ticket #{ticket.TicketNumber} created successfully! Notification email dispatched to support.";

            return RedirectToAction("Details", new { id = ticket.TicketId });
        }

        // GET: ClientPanel/Tickets/Details/5
        public async Task<IActionResult> Details(int id)
        {
            if (id <= 0) return RedirectToAction("Index");

            string userId = UserManager.GetUserId(User);
            var ticket = await _ticketService.GetTicketByIdAsync(id);

            if (ticket == null || ticket.UserId != userId)
            {
                TempData["error"] = "Support ticket not found or access denied.";
                return RedirectToAction("Index");
            }

            return View(ticket);
        }

        // POST: ClientPanel/Tickets/PostReply
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> PostReply(int ticketId, string message)
        {
            if (ticketId <= 0 || string.IsNullOrWhiteSpace(message))
            {
                TempData["error"] = "Reply message cannot be empty.";
                return RedirectToAction("Details", new { id = ticketId });
            }

            string userId = UserManager.GetUserId(User);
            var ticket = await _ticketService.GetTicketByIdAsync(ticketId);

            if (ticket == null || ticket.UserId != userId)
            {
                TempData["error"] = "Support ticket not found or access denied.";
                return RedirectToAction("Index");
            }

            var user = await UserManager.FindByIdAsync(userId);
            string clientName = user?.UserName ?? "Client";

            await _ticketService.AddResponseAsync(ticketId, message.Trim(), userId, clientName, isAdminReply: false);

            TempData["success"] = "Response posted successfully and sent to support team.";
            return RedirectToAction("Details", new { id = ticketId });
        }
    }
}
