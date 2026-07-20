using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Models;
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
    public class PriceSettingsController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();
        private IPriceSettingsService _priceService = new PriceSettingService();

        

        public PriceSettingsController(PriceSettingService priceSettingService)
        {
            _priceService = priceSettingService;
        }

        // GET: Adminpanel/PriceSettings
        public async Task<ActionResult> Index()
        {
            return View(await _priceService.GetPriceSettings());
        }

        // GET: Adminpanel/PriceSettings
        public async Task<ActionResult> DialCodes()
        {
            return View(await _priceService.GetDialCodes());
        }

        // GET: Adminpanel/PriceSettings/Details/5
        public async Task<ActionResult> PriceSettingDetails(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            var priceSetting = await _priceService.GetPriceSetting(id);
            if (priceSetting == null)
            {
                return NotFound();
            }
            return View(priceSetting);
        }

        // GET: Adminpanel/PriceSettings/Details/5
        //public async Task<ActionResult> DialCodeDetails(int? id)
        //{
        //    if (id == null)
        //    {
        //        return BadRequest();
        //    }
        //    var priceSetting = await _priceService.GetPriceSetting(id);
        //    if (priceSetting == null)
        //    {
        //        return NotFound();
        //    }
        //    return View(priceSetting);
        //}

        public ActionResult CreatePriceSetting()
        {
            return View();
        }

        // POST: Adminpanel/PriceSettings/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreatePriceSetting(PriceSetting priceSetting)
        {
            if (ModelState.IsValid)
            {
                await _priceService.AddPriceSetting(priceSetting);
                return RedirectToAction("Index");
            }

            return View(priceSetting);
        }

        public async Task<ActionResult> CreateDial(int? id)
        {
            var priceSettings = await _priceService.GetPriceSetting(id);

            if (priceSettings == null)
            {
                return NotFound();
            }
            ViewBag.Id = id;
            return View();
        }

        // POST: Adminpanel/PriceSettings/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> CreateDial(DialCode dialCode, int? id)
        {
            if (ModelState.IsValid)
            {
                dialCode.PriceSettingId = id.Value;
                await _priceService.AddDialCode(dialCode);
                return RedirectToAction("PriceSettingDetails", new { id = id });
            }

            return View(dialCode);
        }

        // GET: Adminpanel/PriceSettings/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            var priceSetting = await _priceService.GetPriceSetting(id);
            if (priceSetting == null)
            {
                return NotFound();
            }
            return View(priceSetting);
        }

        // POST: Adminpanel/PriceSettings/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(PriceSetting priceSetting)
        {
            if (ModelState.IsValid)
            {
                await _priceService.UpdatePriceSetting(priceSetting);
                return RedirectToAction("Index");
            }
            return View(priceSetting);
        }

        // GET: Adminpanel/PriceSettings/Edit/5
        public async Task<ActionResult> EditDialCode(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            var dialCode = await _priceService.GetDialCode(id);
            if (dialCode == null)
            {
                return NotFound();
            }
            return View(dialCode);
        }

        // POST: Adminpanel/PriceSettings/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> EditDialCode(DialCode dialCode)
        {
            if (ModelState.IsValid)
            {
                await _priceService.UpdateDialCode(dialCode);
                return RedirectToAction("PriceSettingDetails", new { id = dialCode.PriceSettingId });
            }
            return View(dialCode);
        }

        // GET: Adminpanel/PriceSettings/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            PriceSetting priceSetting = await _priceService.GetPriceSetting(id);
            if (priceSetting == null)
            {
                return NotFound();
            }
            return View(priceSetting);
        }

        // POST: Adminpanel/PriceSettings/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            PriceSetting priceSetting = await _priceService.GetPriceSetting(id);

            await _priceService.DeletePriceSettings(priceSetting);
            return RedirectToAction("Index");
        }

        // GET: Adminpanel/PriceSettings/Delete/5
        public async Task<ActionResult> DeleteDialCode(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            DialCode dialCode = await _priceService.GetDialCode(id);
            if (dialCode == null)
            {
                return NotFound();
            }
            return View(dialCode);
        }

        // POST: Adminpanel/PriceSettings/Delete/5
        [HttpPost, ActionName("DeleteDialCode")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteDialCodeConfirmed(int id)
        {
            DialCode dialCode = await _priceService.GetDialCode(id);
            int priceSettingId = dialCode.PriceSettingId;
            await _priceService.DeleteDialCode(dialCode);
            return RedirectToAction("PriceSettingDetails", new { id = priceSettingId });
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