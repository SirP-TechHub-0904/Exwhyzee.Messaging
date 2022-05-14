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
    public class ModalInfoesController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Adminpanel/ModalInfoes
        public async Task<ActionResult> Index()
        {
            return View(await db.ModalInfos.ToListAsync());
        }

        // GET: Adminpanel/ModalInfoes/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ModalInfo modalInfo = await db.ModalInfos.FindAsync(id);
            if (modalInfo == null)
            {
                return HttpNotFound();
            }
            return View(modalInfo);
        }

        // GET: Adminpanel/ModalInfoes/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Adminpanel/ModalInfoes/Create
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create([Bind(Include = "Id,Modal")] ModalInfo modalInfo)
        {
            if (ModelState.IsValid)
            {
                db.ModalInfos.Add(modalInfo);
                await db.SaveChangesAsync();
                return RedirectToAction("Index");
            }

            return View(modalInfo);
        }

        // GET: Adminpanel/ModalInfoes/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ModalInfo modalInfo = await db.ModalInfos.FindAsync(id);
            if (modalInfo == null)
            {
                return HttpNotFound();
            }
            return View(modalInfo);
        }

        // POST: Adminpanel/ModalInfoes/Edit/5
        // To protect from overposting attacks, please enable the specific properties you want to bind to, for 
        // more details see https://go.microsoft.com/fwlink/?LinkId=317598.
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit([Bind(Include = "Id,Modal")] ModalInfo modalInfo)
        {
            if (ModelState.IsValid)
            {
                db.Entry(modalInfo).State = EntityState.Modified;
                await db.SaveChangesAsync();
                return RedirectToAction("Index");
            }
            return View(modalInfo);
        }

        // GET: Adminpanel/ModalInfoes/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }
            ModalInfo modalInfo = await db.ModalInfos.FindAsync(id);
            if (modalInfo == null)
            {
                return HttpNotFound();
            }
            return View(modalInfo);
        }

        // POST: Adminpanel/ModalInfoes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            ModalInfo modalInfo = await db.ModalInfos.FindAsync(id);
            db.ModalInfos.Remove(modalInfo);
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
