using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Exwhyzee.Messaging.Core.Models;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    [Area("Adminpanel")]
    [Authorize(Roles = "SuperAdmin,Admin")]
    public class ModalInfoesController : Controller
    {
        private ApplicationDbContext db => HttpContext.RequestServices.GetService<ApplicationDbContext>();
        private readonly IWebHostEnvironment _env;

        public ModalInfoesController(IWebHostEnvironment env)
        {
            _env = env;
        }

        // GET: Adminpanel/ModalInfoes
        public async Task<ActionResult> Index()
        {
            return View(await db.ModalInfos.OrderByDescending(x => x.Id).ToListAsync());
        }

        // GET: Adminpanel/ModalInfoes/Details/5
        public async Task<ActionResult> Details(int? id)
        {
            if (id == null) return BadRequest();
            ModalInfo modalInfo = await db.ModalInfos.FindAsync(id);
            if (modalInfo == null) return NotFound();
            return View(modalInfo);
        }

        // GET: Adminpanel/ModalInfoes/Create
        public async Task<ActionResult> Create()
        {
            var existing = await db.ModalInfos.FirstOrDefaultAsync();
            if (existing != null)
            {
                return RedirectToAction("Edit", new { id = existing.Id });
            }
            return View(new ModalInfo { ShowFrequencyDays = 7, IsActive = true });
        }

        // POST: Adminpanel/ModalInfoes/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Create(ModalInfo modalInfo, IFormFile imageFile)
        {
            var existing = await db.ModalInfos.FirstOrDefaultAsync();
            if (existing != null)
            {
                // Overwrite existing modal info instead of creating duplicate
                existing.Title = modalInfo.Title;
                existing.ContentHtml = modalInfo.ContentHtml;
                existing.Modal = modalInfo.Modal;
                existing.UseImage = modalInfo.UseImage;
                existing.UseText = modalInfo.UseText;
                existing.IsActive = modalInfo.IsActive;
                existing.ShowFrequencyDays = modalInfo.ShowFrequencyDays > 0 ? modalInfo.ShowFrequencyDays : 7;

                if (imageFile != null && imageFile.Length > 0)
                {
                    string fileName = $"modal_img_{Guid.NewGuid():N}{Path.GetExtension(imageFile.FileName)}";
                    string uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "modals");
                    Directory.CreateDirectory(uploadFolder);
                    string filePath = Path.Combine(uploadFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    existing.ImageUrl = $"/uploads/modals/{fileName}";
                }

                await db.SaveChangesAsync();
                TempData["success"] = "Dashboard Announcement Modal updated successfully!";
                return RedirectToAction("Index");
            }

            if (ModelState.IsValid)
            {
                if (imageFile != null && imageFile.Length > 0)
                {
                    string fileName = $"modal_img_{Guid.NewGuid():N}{Path.GetExtension(imageFile.FileName)}";
                    string uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "modals");
                    Directory.CreateDirectory(uploadFolder);
                    string filePath = Path.Combine(uploadFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    modalInfo.ImageUrl = $"/uploads/modals/{fileName}";
                }

                modalInfo.DateCreated = DateTime.UtcNow;
                db.ModalInfos.Add(modalInfo);
                await db.SaveChangesAsync();
                TempData["success"] = "Dashboard Announcement Modal created successfully!";
                return RedirectToAction("Index");
            }

            return View(modalInfo);
        }

        // GET: Adminpanel/ModalInfoes/Edit/5
        public async Task<ActionResult> Edit(int? id)
        {
            if (id == null) return BadRequest();
            ModalInfo modalInfo = await db.ModalInfos.FindAsync(id);
            if (modalInfo == null) return NotFound();
            return View(modalInfo);
        }

        // POST: Adminpanel/ModalInfoes/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> Edit(ModalInfo modalInfo, IFormFile imageFile)
        {
            if (ModelState.IsValid)
            {
                var existing = await db.ModalInfos.FindAsync(modalInfo.Id);
                if (existing == null) return NotFound();

                existing.Title = modalInfo.Title;
                existing.ContentHtml = modalInfo.ContentHtml;
                existing.Modal = modalInfo.Modal;
                existing.UseImage = modalInfo.UseImage;
                existing.UseText = modalInfo.UseText;
                existing.IsActive = modalInfo.IsActive;
                existing.ShowFrequencyDays = modalInfo.ShowFrequencyDays > 0 ? modalInfo.ShowFrequencyDays : 7;

                if (imageFile != null && imageFile.Length > 0)
                {
                    string fileName = $"modal_img_{Guid.NewGuid():N}{Path.GetExtension(imageFile.FileName)}";
                    string uploadFolder = Path.Combine(_env.WebRootPath, "uploads", "modals");
                    Directory.CreateDirectory(uploadFolder);
                    string filePath = Path.Combine(uploadFolder, fileName);

                    using (var stream = new FileStream(filePath, FileMode.Create))
                    {
                        await imageFile.CopyToAsync(stream);
                    }

                    existing.ImageUrl = $"/uploads/modals/{fileName}";
                }

                await db.SaveChangesAsync();
                TempData["success"] = "Dashboard Announcement Modal updated successfully!";
                return RedirectToAction("Index");
            }
            return View(modalInfo);
        }

        // GET: Adminpanel/ModalInfoes/Delete/5
        public async Task<ActionResult> Delete(int? id)
        {
            if (id == null) return BadRequest();
            ModalInfo modalInfo = await db.ModalInfos.FindAsync(id);
            if (modalInfo == null) return NotFound();
            return View(modalInfo);
        }

        // POST: Adminpanel/ModalInfoes/Delete/5
        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteConfirmed(int id)
        {
            ModalInfo modalInfo = await db.ModalInfos.FindAsync(id);
            if (modalInfo != null)
            {
                db.ModalInfos.Remove(modalInfo);
                await db.SaveChangesAsync();
            }
            return RedirectToAction("Index");
        }

        // GET: Adminpanel/ModalInfoes/GetActiveModalJson (Public / Client AJAX endpoint)
        [HttpGet]
        [AllowAnonymous]
        public async Task<IActionResult> GetActiveModalJson()
        {
            var activeModal = await db.ModalInfos
                .AsNoTracking()
                .Where(m => m.IsActive)
                .OrderByDescending(m => m.Id)
                .FirstOrDefaultAsync();

            if (activeModal == null)
            {
                return Json(new { hasModal = false });
            }

            return Json(new
            {
                hasModal = true,
                id = activeModal.Id,
                title = activeModal.Title ?? "System Announcement",
                contentHtml = activeModal.UseText ? (activeModal.ContentHtml ?? activeModal.Modal) : null,
                imageUrl = activeModal.UseImage ? activeModal.ImageUrl : null,
                useImage = activeModal.UseImage,
                useText = activeModal.UseText,
                frequencyDays = activeModal.ShowFrequencyDays > 0 ? activeModal.ShowFrequencyDays : 7
            });
        }
    }
}
