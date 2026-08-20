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

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> SaveGeminiKey(string geminiApiKey)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

            if (client != null)
            {
                client.GeminiApiKey = geminiApiKey?.Trim();
                db.Entry(client).State = EntityState.Modified;
                await db.SaveChangesAsync();
                TempData["Message"] = "Google Gemini API Key saved successfully!";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<ActionResult> DeleteGeminiKey()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var client = await db.Clients.FirstOrDefaultAsync(c => c.UserId == userId);

            if (client != null)
            {
                client.GeminiApiKey = null;
                db.Entry(client).State = EntityState.Modified;
                await db.SaveChangesAsync();
                TempData["Message"] = "Google Gemini API Key removed successfully!";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> TestGeminiKeyAjax(string geminiApiKey)
        {
            try
            {
                if (string.IsNullOrWhiteSpace(geminiApiKey))
                {
                    return Json(new { success = false, message = "Please enter a Gemini API Key to test." });
                }

                string key = geminiApiKey.Trim();

                using var http = new System.Net.Http.HttpClient();
                http.Timeout = TimeSpan.FromSeconds(15);

                // Check 1: List models via GET /v1beta/models?key=...
                var checkUrl = $"https://generativelanguage.googleapis.com/v1beta/models?key={key}";
                var response = await http.GetAsync(checkUrl);

                if (response.IsSuccessStatusCode)
                {
                    return Json(new { success = true, message = "Google Gemini API Key is valid and verified!" });
                }

                // If GET returned error, parse Google's exact error message
                string errorContent = await response.Content.ReadAsStringAsync();
                string detailedError = $"Gemini returned ({response.StatusCode})";

                try
                {
                    using var doc = System.Text.Json.JsonDocument.Parse(errorContent);
                    if (doc.RootElement.TryGetProperty("error", out var err) && err.TryGetProperty("message", out var msg))
                    {
                        detailedError = msg.GetString();
                    }
                }
                catch { }

                return Json(new { success = false, message = detailedError });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Connection error: " + ex.Message });
            }
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



