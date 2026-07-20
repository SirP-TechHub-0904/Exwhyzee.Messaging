using Microsoft.Extensions.DependencyInjection;
using System.Security.Claims;
using Exwhyzee.Messaging.Core.Models;
using Microsoft.AspNetCore.Identity;
using System;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;

namespace Exwhyzee.Messaging.Web.Areas.ClientPanel.Controllers
{
    [Authorize]
    [Area("ClientPanel")]
    public class ApiSettingsController : Controller
    {
        private ApplicationDbContext db => HttpContext.RequestServices.GetService<ApplicationDbContext>();
        private readonly Microsoft.Extensions.Configuration.IConfiguration _configuration;

        public ApiSettingsController(Microsoft.Extensions.Configuration.IConfiguration configuration)
        {
            _configuration = configuration;
        }

        public async Task<ActionResult> Index()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

            if (client == null)
            {
                return NotFound();
            }

            return View(client);
        }

        public ActionResult Documentation()
        {
            ViewBag.ApiBaseUrl = _configuration["AppSettings:ApiBaseUrl"];
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> RegenerateKey()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

            if (client != null)
            {
                client.ApiKey = "xyz_key_" + Guid.NewGuid().ToString("N");
                db.Entry(client).State = EntityState.Modified;
                await db.SaveChangesAsync();
                TempData["Message"] = "API Key Regenerated Successfully!";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteKey()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

            if (client != null)
            {
                client.ApiKey = null;
                db.Entry(client).State = EntityState.Modified;
                await db.SaveChangesAsync();
                TempData["Message"] = "API Key Deleted Successfully!";
            }

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



