using System;
using System.Linq;
using System.Threading.Tasks;
using Exwhyzee.Messaging.Core.Data.IServices;
using Exwhyzee.Messaging.Core.Dtos;
using Exwhyzee.Messaging.Core.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace Exwhyzee.Messaging.Api.Controllers
{
    [ApiController]
    [Route("api/sms")]
    [Produces("application/json")]
    public class SmsController : ControllerBase
    {
        private readonly ApplicationDbContext _db;
        private readonly IClientService _clientService;

        public SmsController(ApplicationDbContext db, IClientService clientService)
        {
            _db = db;
            _clientService = clientService;
        }

        /// <summary>
        /// Resolves the authenticated client from the API Key headers.
        /// </summary>
        private (string UserId, Client Client) AuthenticateClient()
        {
            // 1. Check custom X-Api-Key or ApiKey header
            string token = Request.Headers["X-Api-Key"].FirstOrDefault()
                ?? Request.Headers["ApiKey"].FirstOrDefault();

            // 2. Check standard Authorization: Bearer <token>
            if (string.IsNullOrEmpty(token))
            {
                var authHeader = Request.Headers["Authorization"].FirstOrDefault();
                if (!string.IsNullOrEmpty(authHeader) && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    token = authHeader.Substring("Bearer ".Length).Trim();
                }
            }

            // 3. Fallback: Query parameter ?apikey=...
            if (string.IsNullOrEmpty(token) && Request.Query.ContainsKey("apikey"))
            {
                token = Request.Query["apikey"].FirstOrDefault();
            }

            if (string.IsNullOrWhiteSpace(token)) return (null, null);

            var client = _db.Clients.Include(c => c.User).FirstOrDefault(c => c.ApiKey == token.Trim());
            return (client?.UserId, client);
        }

        /// <summary>
        /// Dispatch single or bulk SMS messages to recipients.
        /// </summary>
        /// <param name="model">SMS payload containing sender ID, recipients, and message body.</param>
        [HttpPost("send")]
        [ProducesResponseType(typeof(SendMessageResponseDto), 200)]
        [ProducesResponseType(400)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> SendBulkMessage([FromBody] ComposeSmsDto model)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid SMS payload.", errors = ModelState });
            }

            var (userId, client) = AuthenticateClient();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, message = "Unauthorized: Missing, inactive, or invalid API Key. Include 'X-Api-Key: YOUR_KEY' in the header." });
            }

            var response = await _clientService.ComposeSms(model, userId);

            if (response.Success)
            {
                return Ok(response);
            }

            return BadRequest(response);
        }

        /// <summary>
        /// Query the live SMS messaging unit balance for the authenticated account.
        /// </summary>
        [HttpGet("balance")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> CheckBalance()
        {
            var (userId, client) = AuthenticateClient();
            if (string.IsNullOrEmpty(userId) || client == null)
            {
                return Unauthorized(new { success = false, message = "Unauthorized: Invalid API Key." });
            }

            return Ok(new
            {
                success = true,
                username = client.User?.UserName,
                email = client.User?.Email,
                unitsBalance = client.Units,
                currency = "NGN",
                serverTime = DateTime.UtcNow
            });
        }

        /// <summary>
        /// Retrieve recent outbound message delivery logs for the authenticated account.
        /// </summary>
        /// <param name="limit">Max number of records to retrieve (default: 50, max: 200).</param>
        [HttpGet("history")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> ViewHistory([FromQuery] int limit = 50)
        {
            var (userId, client) = AuthenticateClient();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, message = "Unauthorized: Invalid API Key." });
            }

            if (limit <= 0) limit = 50;
            if (limit > 200) limit = 200;

            var messages = await _db.Messages
                .Where(m => m.UserId == userId && m.Status != MessageStatus.Draft)
                .OrderByDescending(m => m.DeliveredDate)
                .Take(limit)
                .Select(m => new
                {
                    messageId = m.MessageId,
                    senderId = m.SenderId,
                    recipients = m.Recipients,
                    messageContent = m.MessageContent,
                    unitsUsed = m.UnitsUsed,
                    status = m.Status.ToString(),
                    dateSent = m.DeliveredDate
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                count = messages.Count,
                records = messages
            });
        }

        /// <summary>
        /// Retrieve all registered sender IDs for the authenticated account with their approval status.
        /// </summary>
        [HttpGet("senderids")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetSenderIds()
        {
            var (userId, client) = AuthenticateClient();
            if (string.IsNullOrEmpty(userId) || client == null)
            {
                return Unauthorized(new { success = false, message = "Unauthorized: Invalid API Key." });
            }

            var senderIds = await _db.XyzSenderIDs
                .Where(s => s.ClientId == client.ClientId)
                .Select(s => new
                {
                    id = s.Id,
                    senderId = s.SenderId,
                    status = s.XYZ_status ?? "Pending",
                    isApproved = (s.XYZ_status == "Approved" || s.XYZ_status == "Active"),
                    message = s.Verify_msg ?? s.XYZ_msg
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                count = senderIds.Count,
                senderIds = senderIds
            });
        }

        /// <summary>
        /// Retrieve base unit price, flat SMS rates, and real-time network pricing with dial codes.
        /// </summary>
        [HttpGet("pricing")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> GetPricing()
        {
            var (userId, client) = AuthenticateClient();
            if (string.IsNullOrEmpty(userId) || client == null)
            {
                return Unauthorized(new { success = false, message = "Unauthorized: Invalid API Key." });
            }

            var adminSetting = await _db.AdminSettings.FirstOrDefaultAsync();
            var basePricePerUnit = adminSetting?.PricePerUnit ?? 2.0m;
            var flatUnitsPerSms = adminSetting?.FlatUnitsPerSms ?? 1.0m;

            var prices = await _db.PriceSettings
                .Select(p => new
                {
                    country = p.Country,
                    networkProvider = p.NetworkProvider,
                    unitsPerSms = p.UnitsPerSms,
                    estimatedCostPerSms = p.UnitsPerSms * basePricePerUnit,
                    internationalDialCode = p.InternationalDialCode
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                currency = "NGN",
                basePricePerUnit = basePricePerUnit,
                flatUnitsPerSms = flatUnitsPerSms,
                count = prices.Count,
                rates = prices
            });
        }
    }
}
