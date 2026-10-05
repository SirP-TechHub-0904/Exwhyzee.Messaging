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
        /// Resolves the authenticated client asynchronously from API Key headers.
        /// Supports X-Api-Key, ApiKey, Authorization: Bearer, and ?apikey= query parameters.
        /// </summary>
        private async Task<(string UserId, Client Client)> AuthenticateClientAsync()
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

            var cleanToken = token.Trim().Trim('"', '\'');
            var client = await _db.Clients
                .AsNoTracking()
                .Include(c => c.User)
                .FirstOrDefaultAsync(c => c.ApiKey == cleanToken);

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
            if (model == null)
            {
                return BadRequest(new { success = false, message = "Request body cannot be empty." });
            }

            if (string.IsNullOrWhiteSpace(model.SenderId))
            {
                return BadRequest(new { success = false, message = "SenderId is required (maximum 11 alphanumeric characters)." });
            }

            if (string.IsNullOrWhiteSpace(model.Recipients))
            {
                return BadRequest(new { success = false, message = "Recipients list is required (comma, space, or newline separated numbers)." });
            }

            if (string.IsNullOrWhiteSpace(model.Content))
            {
                return BadRequest(new { success = false, message = "Message content is required." });
            }

            if (!ModelState.IsValid)
            {
                return BadRequest(new { success = false, message = "Invalid SMS payload.", errors = ModelState });
            }

            var (userId, client) = await AuthenticateClientAsync();
            if (string.IsNullOrEmpty(userId) || client == null)
            {
                return Unauthorized(new { success = false, message = "Unauthorized: Missing, inactive, or invalid API Key. Include 'X-Api-Key: YOUR_KEY' in your headers." });
            }

            // Check if client has sufficient units balance
            if (client.Units <= 0)
            {
                return BadRequest(new { success = false, message = "Insufficient SMS unit balance. Your current balance is " + client.Units.ToString("N2") + " units. Please top up your wallet." });
            }

            var response = await _clientService.ComposeSms(model, userId);

            if (response != null && response.Success)
            {
                return Ok(response);
            }

            return BadRequest(response ?? new SendMessageResponseDto { Success = false, Message = "Unable to dispatch message." });
        }

        /// <summary>
        /// Query the live SMS messaging unit balance for the authenticated account.
        /// </summary>
        [HttpGet("balance")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> CheckBalance()
        {
            var (userId, client) = await AuthenticateClientAsync();
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
        /// Check delivery status and response for a specific message by its Message ID.
        /// </summary>
        [HttpGet("status/{messageId:int}")]
        [ProducesResponseType(200)]
        [ProducesResponseType(404)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> CheckMessageStatus(int messageId)
        {
            var (userId, client) = await AuthenticateClientAsync();
            if (string.IsNullOrEmpty(userId) || client == null)
            {
                return Unauthorized(new { success = false, message = "Unauthorized: Invalid API Key." });
            }

            var message = await _db.Messages
                .AsNoTracking()
                .FirstOrDefaultAsync(m => m.MessageId == messageId && m.UserId == userId);

            if (message == null)
            {
                return NotFound(new { success = false, message = $"Message with ID {messageId} not found." });
            }

            return Ok(new
            {
                success = true,
                messageId = message.MessageId,
                senderId = message.SenderId,
                recipients = message.Recipients,
                unitsUsed = message.UnitsUsed,
                status = message.Status.ToString(),
                response = message.Response,
                dateSent = message.DeliveredDate ?? message.Scheduleddate
            });
        }

        /// <summary>
        /// Retrieve outbound message delivery history with full pagination and optional filtering.
        /// </summary>
        /// <param name="page">Page number (1-based index, default: 1).</param>
        /// <param name="pageSize">Number of records per page (default: 50, max: 200).</param>
        /// <param name="senderId">Optional filter by sender ID.</param>
        /// <param name="recipient">Optional filter by recipient phone number.</param>
        /// <param name="status">Optional filter by delivery status (e.g. Sent, Delivered, Failed).</param>
        [HttpGet("history")]
        [ProducesResponseType(200)]
        [ProducesResponseType(401)]
        public async Task<IActionResult> ViewHistory(
            [FromQuery] int page = 1,
            [FromQuery] int pageSize = 50,
            [FromQuery] string senderId = null,
            [FromQuery] string recipient = null,
            [FromQuery] string status = null)
        {
            var (userId, client) = await AuthenticateClientAsync();
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized(new { success = false, message = "Unauthorized: Invalid API Key." });
            }

            if (page < 1) page = 1;
            if (pageSize <= 0) pageSize = 50;
            if (pageSize > 200) pageSize = 200;

            var query = _db.Messages
                .AsNoTracking()
                .Where(m => m.UserId == userId && m.Status != MessageStatus.Draft);

            if (!string.IsNullOrWhiteSpace(senderId))
            {
                query = query.Where(m => m.SenderId == senderId.Trim());
            }

            if (!string.IsNullOrWhiteSpace(recipient))
            {
                query = query.Where(m => m.Recipients.Contains(recipient.Trim()));
            }

            if (!string.IsNullOrWhiteSpace(status) && Enum.TryParse<MessageStatus>(status, true, out var parsedStatus))
            {
                query = query.Where(m => m.Status == parsedStatus);
            }

            int totalRecords = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalRecords / (double)pageSize);

            var messages = await query
                .OrderByDescending(m => m.DeliveredDate ?? m.Scheduleddate)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(m => new
                {
                    messageId = m.MessageId,
                    senderId = m.SenderId,
                    recipients = m.Recipients,
                    messageContent = m.MessageContent,
                    unitsUsed = m.UnitsUsed,
                    status = m.Status.ToString(),
                    dateSent = m.DeliveredDate ?? m.Scheduleddate
                })
                .ToListAsync();

            return Ok(new
            {
                success = true,
                page = page,
                pageSize = pageSize,
                totalRecords = totalRecords,
                totalPages = totalPages,
                hasNextPage = page < totalPages,
                hasPreviousPage = page > 1,
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
            var (userId, client) = await AuthenticateClientAsync();
            if (string.IsNullOrEmpty(userId) || client == null)
            {
                return Unauthorized(new { success = false, message = "Unauthorized: Invalid API Key." });
            }

            var senderIds = await _db.XyzSenderIDs
                .AsNoTracking()
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
            var (userId, client) = await AuthenticateClientAsync();
            if (string.IsNullOrEmpty(userId) || client == null)
            {
                return Unauthorized(new { success = false, message = "Unauthorized: Invalid API Key." });
            }

            var adminSetting = await _db.AdminSettings.AsNoTracking().FirstOrDefaultAsync();
            var basePricePerUnit = adminSetting?.PricePerUnit ?? 2.0m;
            var flatUnitsPerSms = adminSetting?.FlatUnitsPerSms ?? 1.0m;

            var prices = await _db.PriceSettings
                .AsNoTracking()
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
