using System.Security.Claims;
using Exwhyzee.Messaging.Core.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Exwhyzee.Messaging.Web.Controllers
{
    [Authorize]
    public class PresenceController : Controller
    {
        [HttpPost]
        public IActionResult Heartbeat(string cid)
        {
            string userKey = GetPresenceKey(cid);
            int onlineCount = LiveUserTracker.RecordHeartbeat(userKey);
            return Json(new
            {
                success = true,
                onlineCount = onlineCount
            });
        }

        [HttpPost]
        public IActionResult Leave(string cid)
        {
            string userKey = GetPresenceKey(cid);
            LiveUserTracker.RemoveUser(userKey);
            return Json(new
            {
                success = true,
                onlineCount = LiveUserTracker.GetOnlineUsersCount()
            });
        }

        [HttpGet]
        public IActionResult Count()
        {
            return Json(new
            {
                success = true,
                onlineCount = LiveUserTracker.GetOnlineUsersCount()
            });
        }

        private string GetPresenceKey(string cid)
        {
            string userId = User.Identity?.Name ?? User.FindFirstValue(ClaimTypes.NameIdentifier) ?? "user";
            if (!string.IsNullOrWhiteSpace(cid))
            {
                return $"{userId}:{cid.Trim()}";
            }
            return userId;
        }
    }
}

