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

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    public class BankDetailsController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

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
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            BankDetail bankDetail = await db.BankDetails.FindAsync(id);
            if (bankDetail == null)
            {
                return HttpNotFound();
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
        public async Task<ActionResult> Create([Bind(Include = "BankDetailId,BankName,AccountName,AccountNumber,Active")] BankDetail bankDetail)
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
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            BankDetail bankDetail = await db.BankDetails.FindAsync(id);
            if (bankDetail == null)
            {
                return HttpNotFound();
            }
            return View(bankDetail);
        }

        // POST: Adminpanel/BankDetails/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see http://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "BankDetailId,BankName,AccountName,AccountNumber,Active")] BankDetail bankDetail)
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
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            BankDetail bankDetail = await db.BankDetails.FindAsync(id);
            if (bankDetail == null)
            {
                return HttpNotFound();
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
