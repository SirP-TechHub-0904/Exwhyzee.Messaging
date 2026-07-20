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
                // Fallback connection string for legacy new ApplicationDbContext() usages scattered throughout the app
                optionsBuilder.UseSqlServer("Data Source=147.93.128.160,1433;Initial Catalog=DB_9AFABF_xyzsmsdb;User Id=user_db;Password=Exwhyzee@123;TrustServerCertificate=True;");
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

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            // Remove PluralizingTableNameConvention equivalent
            foreach (var entityType in modelBuilder.Model.GetEntityTypes())
            {
                entityType.SetTableName(entityType.DisplayName());
            }

            // Call base AFTER custom conventions so Identity tables (AspNetUsers, etc.) are correctly mapped and not overwritten
            base.OnModelCreating(modelBuilder);
        }
    }
}
