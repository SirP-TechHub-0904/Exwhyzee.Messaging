using Exwhyzee.Messaging.Core.Data.Services;
using Exwhyzee.Messaging.Core.Dtos;
using Exwhyzee.Messaging.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace Exwhyzee.Messaging.Api.Controllers
{
    [ApiController]
    [Route("api/sms")]
    public class SmsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;

        public SmsController(ApplicationDbContext db)
        {
            _db = db;
        }

        // To make it fully secure, you can implement a standard JWT authentication later.
        // For now, if you are expecting the API token inside the headers:
        private string GetUserIdFromToken()
        {
            // First check the custom ApiKey header
            var token = Request.Headers["ApiKey"].FirstOrDefault();
            
            // If not found, check the standard Authorization header for a Bearer token
            if (string.IsNullOrEmpty(token))
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer "))
                {
                    token = authHeader.Substring("Bearer ".Length).Trim();
                }
            }

            if (string.IsNullOrEmpty(token)) return null;

            var client = _db.Clients.FirstOrDefault(c => c.ApiKey == token);
            return client?.UserId;
        }

        [HttpPost("send")]
        public async Task<IActionResult> SendBulkMessage([FromBody] ComposeSmsDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Missing or invalid ApiKey.");

            var clientService = new ClientService();

            var response = await clientService.ComposeSms(model, userId);

            if (response.Success)
            {
                return Ok(response);
            }
            
            return BadRequest(response);
        }

        [HttpGet("balance")]
        public async Task<IActionResult> CheckBalance()
        {
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Missing or invalid ApiKey.");

            var client = await _db.Clients.FirstOrDefaultAsync(c => c.UserId == userId);
            if (client == null) return NotFound("Client not found.");

            return Ok(new { Balance = client.Units });
        }

        [HttpGet("history")]
        public async Task<IActionResult> ViewHistory()
        {
            var userId = GetUserIdFromToken();
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Missing or invalid ApiKey.");

            var messages = await _db.Messages
                .Where(m => m.UserId == userId && m.Status != Exwhyzee.Messaging.Core.Models.MessageStatus.Draft)
                .OrderByDescending(m => m.DeliveredDate)
                .Take(50)
                .Select(m => new {
                    m.SenderId,
                    m.Recipients,
                    m.MessageContent,
                    m.UnitsUsed,
                    m.Status,
                    m.DeliveredDate
                })
                .ToListAsync();

            return Ok(messages);
        }
    }
}
