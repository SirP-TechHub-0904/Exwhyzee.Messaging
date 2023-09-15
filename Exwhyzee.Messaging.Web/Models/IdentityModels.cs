using Exwhyzee.Messaging.Web.Areas.Content.Models;
using Microsoft.AspNet.Identity;
using Microsoft.AspNet.Identity.EntityFramework;
using System;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Data.Entity.ModelConfiguration.Conventions;
using System.Security.Claims;
using System.Threading.Tasks;

namespace Exwhyzee.Messaging.Web.Models
{
    // You can add profile data for the user by adding more properties to your ApplicationUser class, please visit http://go.microsoft.com/fwlink/?LinkID=317594 to learn more.
    public class ApplicationUser : IdentityUser
    {
        public async Task<ClaimsIdentity> GenerateUserIdentityAsync(UserManager<ApplicationUser> manager)
        {
            // Note the authenticationType must match the one defined in CookieAuthenticationOptions.AuthenticationType
            var userIdentity = await manager.CreateIdentityAsync(this, DefaultAuthenticationTypes.ApplicationCookie);
            // Add custom user claims here
            return userIdentity;
        }

        [DisplayFormat(ApplyFormatInEditMode = true, DataFormatString = "{0:MM/dd/yyyy}")]
        public DateTime DateOfBirth { get; set; }
        public DateTime DateRegitered { get; set; }
        public string Code { get; set; }
    }

    public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
    {
        public ApplicationDbContext()
            : base("ZyxsmsDbConnection", throwIfV1Schema: false)
        {
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
        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            modelBuilder.Conventions.Remove<PluralizingTableNameConvention>();
        }

        public static ApplicationDbContext Create()
        {
            return new ApplicationDbContext();
        }
    }
}