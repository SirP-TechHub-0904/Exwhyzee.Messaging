using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Exwhyzee.Messaging.Core.Models;

namespace Exwhyzee.Messaging.Core.Services
{
    public interface ITicketService
    {
        Task<SupportTicket> CreateTicketAsync(SupportTicket ticket);
        Task<TicketResponse> AddResponseAsync(int ticketId, string message, string senderUserId, string senderName, bool isAdminReply, string adminRepliedBy = null);
        Task<bool> UpdateStatusAsync(int ticketId, TicketStatus status);
        Task<SupportTicket> GetTicketByIdAsync(int ticketId);
        Task<SupportTicket> GetTicketByNumberAsync(string ticketNumber);
        Task<List<SupportTicket>> GetTicketsForUserAsync(string userId);
        Task<List<SupportTicket>> GetAllTicketsAsync(TicketStatus? statusFilter = null, TicketPriority? priorityFilter = null, string search = null);
    }

    public class TicketService : ITicketService
    {
        private readonly ApplicationDbContext _db;
        private readonly IConfiguration _configuration;
        private readonly ZeptoMailService _mailService;

        public TicketService()
        {
            _db = new ApplicationDbContext();
            _configuration = BuildConfiguration();
            _mailService = new ZeptoMailService(_configuration);
        }

        public TicketService(ApplicationDbContext db, IConfiguration configuration)
        {
            _db = db ?? new ApplicationDbContext();
            _configuration = configuration ?? BuildConfiguration();
            _mailService = new ZeptoMailService(_configuration);
        }

        private static IConfiguration BuildConfiguration()
        {
            try
            {
                var builder = new ConfigurationBuilder();

                string jsonPath = Path.Combine(AppContext.BaseDirectory, "appsettings.json");
                if (!File.Exists(jsonPath))
                {
                    jsonPath = Path.Combine(Directory.GetCurrentDirectory(), "appsettings.json");
                }
                if (File.Exists(jsonPath))
                {
                    builder.AddJsonFile(jsonPath, optional: true, reloadOnChange: true);
                }

                var possibleEnvPaths = new[]
                {
                    Path.Combine(AppContext.BaseDirectory, ".env"),
                    Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                    Path.Combine(Directory.GetCurrentDirectory(), "..", "Exwhyzee.Messaging.Web", ".env")
                };

                foreach (var envPath in possibleEnvPaths)
                {
                    if (File.Exists(envPath))
                    {
                        foreach (var rawLine in File.ReadAllLines(envPath))
                        {
                            var line = rawLine.Trim();
                            if (!string.IsNullOrEmpty(line) && !line.StartsWith("#") && line.Contains('='))
                            {
                                var parts = line.Split(new[] { '=' }, 2);
                                Environment.SetEnvironmentVariable(parts[0].Trim(), parts[1].Trim());
                            }
                        }
                        break;
                    }
                }

                builder.AddEnvironmentVariables();
                return builder.Build();
            }
            catch { }
            return null;
        }

        private string[] TicketSupportEmails
        {
            get
            {
                var val = _configuration?["AppSettings:TicketSupportEmails"];
                if (string.IsNullOrWhiteSpace(val))
                {
                    val = Environment.GetEnvironmentVariable("AppSettings__TicketSupportEmails");
                }
                return (val ?? string.Empty).Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries);
            }
        }

        public async Task<SupportTicket> CreateTicketAsync(SupportTicket ticket)
        {
            if (ticket == null) throw new ArgumentNullException(nameof(ticket));

            ticket.TicketNumber = GenerateTicketNumber();
            ticket.DateCreated = DateTime.UtcNow.AddHours(1);
            ticket.DateUpdated = DateTime.UtcNow.AddHours(1);
            ticket.Status = TicketStatus.Open;

            _db.SupportTickets.Add(ticket);
            await _db.SaveChangesAsync();

            // 1. Send Email Notification to System Support Emails (appsettings.json)
            await SendNewTicketAlertToSupportAsync(ticket);

            // 2. Send Confirmation Email to User
            await SendTicketConfirmationToUserAsync(ticket);

            return ticket;
        }

        public async Task<TicketResponse> AddResponseAsync(int ticketId, string message, string senderUserId, string senderName, bool isAdminReply, string adminRepliedBy = null)
        {
            var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket == null) throw new Exception("Ticket not found.");

            var response = new TicketResponse
            {
                TicketId = ticketId,
                Message = message,
                SenderUserId = senderUserId,
                SenderName = senderName,
                IsAdminReply = isAdminReply,
                AdminRepliedBy = isAdminReply ? (adminRepliedBy ?? senderName) : null,
                DateCreated = DateTime.UtcNow.AddHours(1)
            };

            _db.TicketResponses.Add(response);

            // Transition ticket status
            ticket.DateUpdated = DateTime.UtcNow.AddHours(1);
            if (isAdminReply && ticket.Status == TicketStatus.Open)
            {
                ticket.Status = TicketStatus.InProgress;
            }
            else if (!isAdminReply && (ticket.Status == TicketStatus.Resolved || ticket.Status == TicketStatus.Closed))
            {
                ticket.Status = TicketStatus.InProgress;
            }

            await _db.SaveChangesAsync();

            // Email Alerts
            if (isAdminReply)
            {
                await SendAdminReplyAlertToUserAsync(ticket, response);
            }
            else
            {
                await SendUserReplyAlertToSupportAsync(ticket, response);
            }

            return response;
        }

        public async Task<bool> UpdateStatusAsync(int ticketId, TicketStatus status)
        {
            var ticket = await _db.SupportTickets.FirstOrDefaultAsync(t => t.TicketId == ticketId);
            if (ticket == null) return false;

            ticket.Status = status;
            ticket.DateUpdated = DateTime.UtcNow.AddHours(1);
            await _db.SaveChangesAsync();
            return true;
        }

        public async Task<SupportTicket> GetTicketByIdAsync(int ticketId)
        {
            return await _db.SupportTickets
                .Include(t => t.Responses)
                .FirstOrDefaultAsync(t => t.TicketId == ticketId);
        }

        public async Task<SupportTicket> GetTicketByNumberAsync(string ticketNumber)
        {
            return await _db.SupportTickets
                .Include(t => t.Responses)
                .FirstOrDefaultAsync(t => t.TicketNumber == ticketNumber);
        }

        public async Task<List<SupportTicket>> GetTicketsForUserAsync(string userId)
        {
            if (string.IsNullOrEmpty(userId)) return new List<SupportTicket>();

            return await _db.SupportTickets
                .AsNoTracking()
                .Include(t => t.Responses)
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.DateUpdated)
                .ToListAsync();
        }

        public async Task<List<SupportTicket>> GetAllTicketsAsync(TicketStatus? statusFilter = null, TicketPriority? priorityFilter = null, string search = null)
        {
            var query = _db.SupportTickets.AsNoTracking().Include(t => t.Responses).AsQueryable();

            if (statusFilter.HasValue)
            {
                query = query.Where(t => t.Status == statusFilter.Value);
            }
            if (priorityFilter.HasValue)
            {
                query = query.Where(t => t.Priority == priorityFilter.Value);
            }
            if (!string.IsNullOrWhiteSpace(search))
            {
                string term = search.Trim().ToLower();
                query = query.Where(t => t.TicketNumber.ToLower().Contains(term)
                    || t.SenderName.ToLower().Contains(term)
                    || t.SenderEmail.ToLower().Contains(term)
                    || t.Subject.ToLower().Contains(term));
            }

            return await query.OrderByDescending(t => t.DateUpdated).ToListAsync();
        }

        private string GenerateTicketNumber()
        {
            string datePart = DateTime.UtcNow.ToString("yyyyMMdd");
            using (var rng = RandomNumberGenerator.Create())
            {
                byte[] bytes = new byte[4];
                rng.GetBytes(bytes);
                int randomNum = Math.Abs(BitConverter.ToInt32(bytes, 0)) % 10000;
                return $"TICK-{datePart}-{randomNum:D4}";
            }
        }

        private async Task SendNewTicketAlertToSupportAsync(SupportTicket ticket)
        {
            try
            {
                string subject = $"[Support Alert] New Ticket #{ticket.TicketNumber}: {ticket.Subject}";
                string body = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; background-color: #F8FAFC;'>
                        <div style='max-width: 600px; margin: 0 auto; background: #FFFFFF; border-radius: 12px; padding: 24px; border: 1px solid #E2E8F0;'>
                            <h2 style='color: #0F172A; margin-top: 0;'>New Support Ticket Submitted</h2>
                            <p style='color: #475569;'><strong>Ticket Number:</strong> #{ticket.TicketNumber}</p>
                            <p style='color: #475569;'><strong>Sender:</strong> {ticket.SenderName} ({ticket.SenderEmail})</p>
                            <p style='color: #475569;'><strong>Category:</strong> {ticket.Category}</p>
                            <p style='color: #475569;'><strong>Priority:</strong> {ticket.Priority}</p>
                            <p style='color: #475569;'><strong>Subject:</strong> {ticket.Subject}</p>
                            <div style='background: #F1F5F9; border-left: 4px solid #2563EB; padding: 14px; margin: 16px 0; border-radius: 4px;'>
                                <p style='color: #1E293B; margin: 0; white-space: pre-wrap;'>{ticket.Message}</p>
                            </div>
                        </div>
                    </div>";

                await _mailService.SendEmailWithCustomRecipientAsync(body, TicketSupportEmails, subject);
            }
            catch { }
        }

        private async Task SendTicketConfirmationToUserAsync(SupportTicket ticket)
        {
            try
            {
                string subject = $"[XYZSMS Support] Ticket Received #{ticket.TicketNumber}: {ticket.Subject}";
                string body = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; background-color: #F8FAFC;'>
                        <div style='max-width: 600px; margin: 0 auto; background: #FFFFFF; border-radius: 12px; padding: 24px; border: 1px solid #E2E8F0;'>
                            <h2 style='color: #0F172A; margin-top: 0;'>Support Ticket Received</h2>
                            <p style='color: #475569;'>Hello <strong>{ticket.SenderName}</strong>,</p>
                            <p style='color: #475569;'>We have received your support inquiry and assigned it reference code <strong>#{ticket.TicketNumber}</strong>.</p>
                            <p style='color: #475569;'>Our support team is reviewing your ticket and will respond shortly.</p>
                            <div style='background: #F1F5F9; padding: 14px; margin: 16px 0; border-radius: 8px;'>
                                <strong style='color: #0F172A;'>Subject:</strong> {ticket.Subject}<br/>
                                <strong style='color: #0F172A;'>Status:</strong> {ticket.Status}
                            </div>
                        </div>
                    </div>";

                await _mailService.SendEmailWithCustomRecipientAsync(body, new[] { ticket.SenderEmail }, subject);
            }
            catch { }
        }

        private async Task SendAdminReplyAlertToUserAsync(SupportTicket ticket, TicketResponse response)
        {
            try
            {
                string subject = $"[Response Alert] #{ticket.TicketNumber}: Staff Replied to Your Ticket";
                string body = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; background-color: #F8FAFC;'>
                        <div style='max-width: 600px; margin: 0 auto; background: #FFFFFF; border-radius: 12px; padding: 24px; border: 1px solid #E2E8F0;'>
                            <h2 style='color: #0F172A; margin-top: 0;'>New Response from Support Team</h2>
                            <p style='color: #475569;'>Hello <strong>{ticket.SenderName}</strong>,</p>
                            <p style='color: #475569;'>Support Staff <strong>{response.AdminRepliedBy}</strong> replied to your ticket <strong>#{ticket.TicketNumber}</strong>:</p>
                            <div style='background: #EFF6FF; border-left: 4px solid #2563EB; padding: 16px; margin: 16px 0; border-radius: 4px;'>
                                <p style='color: #1E3A8A; margin: 0; white-space: pre-wrap;'>{response.Message}</p>
                            </div>
                        </div>
                    </div>";

                await _mailService.SendEmailWithCustomRecipientAsync(body, new[] { ticket.SenderEmail }, subject);
            }
            catch { }
        }

        private async Task SendUserReplyAlertToSupportAsync(SupportTicket ticket, TicketResponse response)
        {
            try
            {
                string subject = $"[User Reply Alert] #{ticket.TicketNumber}: User Replied";
                string body = $@"
                    <div style='font-family: Arial, sans-serif; padding: 20px; background-color: #F8FAFC;'>
                        <div style='max-width: 600px; margin: 0 auto; background: #FFFFFF; border-radius: 12px; padding: 24px; border: 1px solid #E2E8F0;'>
                            <h2 style='color: #0F172A; margin-top: 0;'>User Replied to Ticket #{ticket.TicketNumber}</h2>
                            <p style='color: #475569;'><strong>User:</strong> {response.SenderName}</p>
                            <div style='background: #F1F5F9; border-left: 4px solid #10B981; padding: 14px; margin: 16px 0; border-radius: 4px;'>
                                <p style='color: #1E293B; margin: 0; white-space: pre-wrap;'>{response.Message}</p>
                            </div>
                        </div>
                    </div>";

                await _mailService.SendEmailWithCustomRecipientAsync(body, TicketSupportEmails, subject);
            }
            catch { }
        }
    }
}
