using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.ViewModels;

using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Net;
using System.Threading.Tasks;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Authorize(Roles = "SuperAdmin,Admin")]
    [Area("Adminpanel")]
    public class VouchersController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        private IVoucherService _voucherService = new VoucherService();
        private Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> _userManager;

        

        public VouchersController(IVoucherService voucherService)
        {
            _voucherService = voucherService ?? new VoucherService();
        }

        public Microsoft.AspNetCore.Identity.UserManager<ApplicationUser> UserManager
        {
            get
            {
                return _userManager;
            }
            private set
            {
                _userManager = value;
            }
        }

        // GET: Adminpanel/Batches
        public async Task<ActionResult> Index()
        {
            return View(await _voucherService.GetBatchVouchers());
        }

        // GET: Adminpanel/Vouchers
        public async Task<ActionResult> AllVouchers()
        {
            return View(await _voucherService.GetVouchers());
        }

        // GET: Adminpanel/Vouchers
        public async Task<ActionResult> VouchersInBatches(int? id)
        {
            return View(await _voucherService.GetVouchersInBatch(id));
        }


        // GET: Adminpanel/VouchersPreview
        public async Task<ActionResult> VouchersPreview(int? id)
        {
            return View(await _voucherService.GetVouchersInBatch(id));
        }

        // GET: Adminpanel/Vouchers/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            var voucher = await _voucherService.GetVoucher(id);
            if (voucher == null)
            {
                return NotFound();
            }
            return View(voucher);
        }

        // GET: Adminpanel/Vouchers/Create
        public ActionResult GenerateVoucher()
        {
            return View();
        }

        // POST: Adminpanel/Vouchers/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> GenerateVoucher(VoucherViewModel voucher)
        {
            if (ModelState.IsValid)
            {
                try
                {
                    var user = await UserManager.FindByNameAsync(User.Identity.Name);
                    await _voucherService.GenerateVouchers(12, voucher.Quantity, voucher.Units, voucher.BatchNumber, user.Id);
                    return RedirectToAction("Index");
                }
                catch (Exception e)
                {
                    TempData["error"] = "Error! " + e.Message;
                    return View(voucher);
                }
            }

            return View(voucher);
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