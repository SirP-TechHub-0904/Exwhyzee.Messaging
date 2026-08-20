using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using System;
using System.ComponentModel.DataAnnotations;

namespace Exwhyzee.Messaging.Core.Models
{
    public class ApplicationUser : IdentityUser
    {
        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:MM/dd/yyyy}")]
        public DateTime DateOfBirth { get; set; }
        public DateTime DateRegitered { get; set; }
        public string Code { get; set; }

        // Multi-Option 2FA Properties
        public TwoFactorMethod PreferredTwoFactorMethod { get; set; }
        public string TwoFactorSecretKey { get; set; }
        public string LastOtpCode { get; set; }
        public DateTime? LastOtpExpiry { get; set; }
    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
            : base(options)
        {
        }
        
        // Parameterless constructor for design-time / legacy compat
        public ApplicationDbContext() { }

        protected override void OnConfiguring(DbContextOptionsBuilder optionsBuilder)
        {
            if (!optionsBuilder.IsConfigured)
            {
                string connStr = Environment.GetEnvironmentVariable("ConnectionStrings__ZyxsmsDbConnection");
                if (string.IsNullOrWhiteSpace(connStr))
                {
                    try
                    {
                        var possibleEnvPaths = new[]
                        {
                            Path.Combine(AppContext.BaseDirectory, ".env"),
                            Path.Combine(Directory.GetCurrentDirectory(), ".env"),
                            Path.Combine(Directory.GetCurrentDirectory(), "..", "Exwhyzee.Messaging.Web", ".env"),
                            Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "..", "..", "..", "..", "Exwhyzee.Messaging.Web", ".env")
                        };

                        foreach (var envPath in possibleEnvPaths)
                        {
                            if (File.Exists(envPath))
                            {
                                foreach (var rawLine in File.ReadAllLines(envPath))
                                {
                                    var line = rawLine.Trim();
                                    if (line.StartsWith("ConnectionStrings__ZyxsmsDbConnection="))
                                    {
                                        connStr = line.Substring("ConnectionStrings__ZyxsmsDbConnection=".Length).Trim();
                                        break;
                                    }
                                }
                                if (!string.IsNullOrEmpty(connStr)) break;
                            }
                        }
                    }
                    catch { }
                }

                if (!string.IsNullOrWhiteSpace(connStr))
                {
                    optionsBuilder.UseSqlServer(connStr);
                }
            }
        }

        public DbSet<Contact> Contacts { get; set; }
        public DbSet<ApiSetting> ApiSettings { get; set; }
        public DbSet<BankDetail> BankDetails { get; set; }
        public DbSet<BatchVoucher> BatchVouchers { get; set; }
        public DbSet<Client> Clients { get; set; }
        public DbSet<DialCode> DialCodes { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<PriceSetting> PriceSettings { get; set; }
        public DbSet<Transaction> Transactions { get; set; }
        public DbSet<Voucher> Vouchers { get; set; }
        public DbSet<AdminSetting> AdminSettings { get; set; }
        public DbSet<Slider> Sliders { get; set; }
        public DbSet<MessageChunk> MessageChunks { get; set; }
        public DbSet<ModalInfo> ModalInfos { get; set; }
        public DbSet<XyzSenderID> XyzSenderIDs { get; set; }
        public DbSet<AppNotification> AppNotifications { get; set; }
        public DbSet<SupportTicket> SupportTickets { get; set; }
        public DbSet<TicketResponse> TicketResponses { get; set; }
        public DbSet<EmailLog> EmailLogs { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Remove PluralizingTableNameConvention equivalent
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                entityType.SetTableName(entityType.DisplayName());
            }

            foreach (var property in modelBuilder.Model.GetEntityTypes()
                .SelectMany(t => t.GetProperties())
                .Where(p => p.ClrType == typeof(decimal) || p.ClrType == typeof(decimal?)))
            {
                property.SetColumnType("decimal(18,2)");
            }

            // Call base AFTER custom conventions so Identity tables (AspNetUsers, etc.) are correctly mapped and not overwritten
            base.OnModelCreating(modelBuilder);
        }
    }
}
