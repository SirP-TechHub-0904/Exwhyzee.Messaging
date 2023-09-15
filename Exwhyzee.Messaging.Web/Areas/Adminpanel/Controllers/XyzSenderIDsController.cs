using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Entity;
using System.Linq;
using System.Threading.Tasks;
using System.Net;
using System.Web;
using System.Web.Mvc;
using Exwhyzee.Messaging.Web.Models;
using Exwhyzee.Messaging.Web.Data.IServices;
using Exwhyzee.Messaging.Web.Data.Services;
using Microsoft.AspNet.Identity;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    public class XyzSenderIDsController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        private IClientService _clientService = new ClientService();

        public XyzSenderIDsController(IClientService clientService)
        {
            _clientService = clientService;
        }

        // GET: Adminpanel/XyzSenderIDs
        public async Task<ActionResult> Index()
        {
            return View(await _clientService.GetAllSenderId());
        }

        public async Task<ActionResult> SenderByUser(string userId)
        {
            var client = await _clientService.GetClientDetailsByUserId(userId);
            ViewBag.name = client.Surname +" "+ client.FirstName + " "+ client.OtherNames;
            return View(await _clientService.GetAllSenderIdById(userId));
        }

         
        // GET: Adminpanel/XyzSenderIDs/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Adminpanel/XyzSenderIDs/Create
        // To protect from overposting attacks, enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(string SenderId, string Message)
        {
            if (ModelState.IsValid)
            {
                string userId = User.Identity.GetUserId();
                var response = await _clientService.AddSender(userId, SenderId, Message);
                TempData["success"] = response;
                return RedirectToAction("Index");
            }
            TempData["error"] = "unable to process";
            return View();
        }
  

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}
