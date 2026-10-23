using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Exwhyzee.Messaging.Core.Models;
using Exwhyzee.Messaging.Core.Services;

namespace Exwhyzee.Messaging.Web.Areas.Adminpanel.Controllers
{
    /// <summary>
    /// Admin reference page: platform error codes (1009xx), their meaning,
    /// what clients see, and what admins should do.
    /// </summary>
    [Authorize(Roles = "Admin,SuperAdmin")]
    [Area("Adminpanel")]
    public class ErrorCodesController : Controller
    {
        private ApplicationDbContext db => HttpContext.RequestServices.GetService<ApplicationDbContext>();

        // GET: Adminpanel/ErrorCodes
        public async Task<IActionResult> Index()
        {
            // Occurrence counts per gateway error code (from saved message responses)
            var counts = new Dictionary<string, int>();
            var lastSeen = new Dictionary<string, DateTime?>();
            try
            {
                var since = DateTime.UtcNow.AddDays(-30);
                var stats = await db.Messages.AsNoTracking()
                    .Where(m => m.Response_error_code != null && m.Response_error_code != "" && m.Response_error_code != "000")
                    .GroupBy(m => m.Response_error_code)
                    .Select(g => new
                    {
                        Code = g.Key,
                        Total = g.Count(),
                        Last30 = g.Count(x => x.DeliveredDate >= since),
                        LastSeen = g.Max(x => x.DeliveredDate)
                    })
                    .ToListAsync();

                foreach (var s in stats)
                {
                    var platform = GatewayErrorMapper.GetPlatformCode(s.Code);
                    counts[platform] = (counts.TryGetValue(platform, out var c) ? c : 0) + s.Total;
                    if (!lastSeen.TryGetValue(platform, out var ls) || (s.LastSeen.HasValue && (!ls.HasValue || s.LastSeen > ls)))
                        lastSeen[platform] = s.LastSeen;
                }
                ViewBag.Last30Total = stats.Sum(s => s.Last30);
            }
            catch
            {
                ViewBag.Last30Total = 0;
            }

            ViewBag.Counts = counts;
            ViewBag.LastSeen = lastSeen;
            ViewBag.UnknownCode = GatewayErrorMapper.UnknownCode;
            return View(GatewayErrorMapper.All);
        }
    }
}
