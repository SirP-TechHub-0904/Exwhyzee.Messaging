using Microsoft.Extensions.DependencyInjection;
using System;
using System.Collections.Generic;
using System.Data;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using System.Net;

using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Exwhyzee.Messaging.Core.Models;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Area("Adminpanel")]
    public class BankDetailsController : Controller
    {
        private ApplicationDbContext db => HttpContext.RequestServices.GetService<ApplicationDbContext>();

        // GET: Adminpanel/BankDetails
        public async Task<ActionResult> Index()
        {
            return View(await db.BankDetails.ToListAsync());
        }

        // GET: Adminpanel/BankDetails/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            BankDetail bankDetail = await db.BankDetails.FindAsync(id);
            if (bankDetail == null)
            {
                return NotFound();
            }
            return View(bankDetail);
        }

        // GET: Adminpanel/BankDetails/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Adminpanel/BankDetails/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(BankDetail bankDetail)
        {
            if (ModelState.IsValid)
            {
                db.BankDetails.Add(bankDetail);
                await db.SaveChangesAsync();
                return RedirectToAction("Index");
            }

            return View(bankDetail);
        }

        // GET: Adminpanel/BankDetails/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            BankDetail bankDetail = await db.BankDetails.FindAsync(id);
            if (bankDetail == null)
            {
                return NotFound();
            }
            return View(bankDetail);
        }

        // POST: Adminpanel/BankDetails/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(BankDetail bankDetail)
        {
            if (ModelState.IsValid)
            {
                db.Entry(bankDetail).State = EntityState.Modified;
                await db.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            return View(bankDetail);
        }

        // GET: Adminpanel/BankDetails/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return BadRequest();
            }
            BankDetail bankDetail = await db.BankDetails.FindAsync(id);
            if (bankDetail == null)
            {
                return NotFound();
            }
            return View(bankDetail);
        }

        // POST: Adminpanel/BankDetails/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            BankDetail bankDetail = await db.BankDetails.FindAsync(id);
            db.BankDetails.Remove(bankDetail);
            await db.SaveChangesAsync();
            return RedirectToAction("Index");
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


