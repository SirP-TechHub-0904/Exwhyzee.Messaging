namespace Exwhyzee.Messaging.Web.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class initialdata : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.AdminSetting",
                c => new
                    {
                        AdminSettingId = c.Int(nullable: false, identity: true),
                        UnitPerNewMember = c.Decimal(nullable: false, precision: 18, scale: 2),
                        DefaultUserUnitReorderLevel = c.Decimal(nullable: false, precision: 18, scale: 2),
                        PricePerUnit = c.Decimal(nullable: false, precision: 18, scale: 2),
                        FlatUnitsPerSms = c.Decimal(nullable: false, precision: 18, scale: 2),
                        BlackListedWords = c.String(),
                        SendOrderApprovedNotification = c.Boolean(nullable: false),
                        SendLowOnUnitsNotification = c.Boolean(nullable: false),
                        SendRecievedRequestNotification = c.Boolean(nullable: false),
                        SendReminderToDebtor = c.Boolean(nullable: false),
                        SendAccountCreditedNotification = c.Boolean(nullable: false),
                        SendUserBirthdayWishes = c.Boolean(nullable: false),
                        PreventApiModification = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.AdminSettingId);
            
            CreateTable(
                "dbo.ApiSetting",
                c => new
                    {
                        ApiSettingId = c.Int(nullable: false, identity: true),
                        Name = c.String(),
                        Sending = c.String(),
                        CheckBalance = c.String(),
                        IsDefault = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.ApiSettingId);
            
            CreateTable(
                "dbo.BankDetail",
                c => new
                    {
                        BankDetailId = c.Int(nullable: false, identity: true),
                        BankName = c.String(),
                        AccountName = c.String(),
                        AccountNumber = c.String(),
                        Active = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.BankDetailId);
            
            CreateTable(
                "dbo.BatchVoucher",
                c => new
                    {
                        BatchVoucherId = c.Int(nullable: false, identity: true),
                        UserId = c.String(maxLength: 128),
                        DateGenerated = c.DateTime(nullable: false),
                        BatchNumber = c.String(),
                        Quantity = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.BatchVoucherId)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUsers",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        DateOfBirth = c.DateTime(nullable: false),
                        DateRegitered = c.DateTime(nullable: false),
                        Email = c.String(maxLength: 256),
                        EmailConfirmed = c.Boolean(nullable: false),
                        PasswordHash = c.String(),
                        SecurityStamp = c.String(),
                        PhoneNumber = c.String(),
                        PhoneNumberConfirmed = c.Boolean(nullable: false),
                        TwoFactorEnabled = c.Boolean(nullable: false),
                        LockoutEndDateUtc = c.DateTime(),
                        LockoutEnabled = c.Boolean(nullable: false),
                        AccessFailedCount = c.Int(nullable: false),
                        UserName = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.UserName, unique: true, name: "UserNameIndex");
            
            CreateTable(
                "dbo.AspNetUserClaims",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.String(nullable: false, maxLength: 128),
                        ClaimType = c.String(),
                        ClaimValue = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUserLogins",
                c => new
                    {
                        LoginProvider = c.String(nullable: false, maxLength: 128),
                        ProviderKey = c.String(nullable: false, maxLength: 128),
                        UserId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.LoginProvider, t.ProviderKey, t.UserId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.AspNetUserRoles",
                c => new
                    {
                        UserId = c.String(nullable: false, maxLength: 128),
                        RoleId = c.String(nullable: false, maxLength: 128),
                    })
                .PrimaryKey(t => new { t.UserId, t.RoleId })
                .ForeignKey("dbo.AspNetUsers", t => t.UserId, cascadeDelete: true)
                .ForeignKey("dbo.AspNetRoles", t => t.RoleId, cascadeDelete: true)
                .Index(t => t.UserId)
                .Index(t => t.RoleId);
            
            CreateTable(
                "dbo.Voucher",
                c => new
                    {
                        VoucherId = c.Int(nullable: false, identity: true),
                        UserId = c.String(maxLength: 128),
                        BatchVoucherId = c.Int(nullable: false),
                        Code = c.String(),
                        DateUsed = c.DateTime(),
                        Units = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Status = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.VoucherId)
                .ForeignKey("dbo.BatchVoucher", t => t.BatchVoucherId, cascadeDelete: true)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId)
                .Index(t => t.UserId)
                .Index(t => t.BatchVoucherId);
            
            CreateTable(
                "dbo.Client",
                c => new
                    {
                        ClientId = c.Int(nullable: false, identity: true),
                        UserId = c.String(maxLength: 128),
                        Units = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Surname = c.String(),
                        FirstName = c.String(),
                        OtherNames = c.String(),
                        Discount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        AllowNotifications = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.ClientId)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.Contact",
                c => new
                    {
                        ContactId = c.Int(nullable: false, identity: true),
                        PhoneNumber = c.String(),
                        GroupId = c.Int(),
                        GpId = c.Int(),
                        Surname = c.String(),
                        Othernames = c.String(),
                        DateAddded = c.DateTime(nullable: false),
                        DateOfBirth = c.DateTime(),
                        Note = c.String(),
                    })
                .PrimaryKey(t => t.ContactId)
                .ForeignKey("dbo.Group", t => t.GroupId)
                .Index(t => t.GroupId);
            
            CreateTable(
                "dbo.Group",
                c => new
                    {
                        GroupId = c.Int(nullable: false, identity: true),
                        UserId = c.String(),
                        Name = c.String(),
                        Description = c.String(),
                        DateCreated = c.DateTime(nullable: false),
                        SendBirthDayMessages = c.Boolean(),
                        GpId = c.Int(),
                        SenderId = c.String(maxLength: 11),
                        Message = c.String(),
                    })
                .PrimaryKey(t => t.GroupId);
            
            CreateTable(
                "dbo.DialCode",
                c => new
                    {
                        DialCodeId = c.Int(nullable: false, identity: true),
                        NumberPrefix = c.String(),
                        PriceSettingId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.DialCodeId)
                .ForeignKey("dbo.PriceSetting", t => t.PriceSettingId, cascadeDelete: true)
                .Index(t => t.PriceSettingId);
            
            CreateTable(
                "dbo.PriceSetting",
                c => new
                    {
                        PriceSettingId = c.Int(nullable: false, identity: true),
                        Country = c.String(),
                        NetworkProvider = c.String(),
                        DigitCount = c.Int(nullable: false),
                        UnitsPerSms = c.Decimal(nullable: false, precision: 18, scale: 2),
                        InternationalDialCode = c.String(),
                    })
                .PrimaryKey(t => t.PriceSettingId);
            
            CreateTable(
                "dbo.MessageChunk",
                c => new
                    {
                        MessageChunkId = c.Int(nullable: false, identity: true),
                        MessageId = c.Int(nullable: false),
                        Numbers = c.String(),
                        Response = c.String(),
                    })
                .PrimaryKey(t => t.MessageChunkId);
            
            CreateTable(
                "dbo.Message",
                c => new
                    {
                        MessageId = c.Int(nullable: false, identity: true),
                        SenderId = c.String(),
                        Recipients = c.String(),
                        MessageContent = c.String(),
                        Response = c.String(),
                        SummaryReport = c.String(),
                        UnitsUsed = c.Decimal(nullable: false, precision: 18, scale: 2),
                        Scheduleddate = c.DateTime(),
                        DeliveredDate = c.DateTime(),
                        Status = c.Int(nullable: false),
                        UserId = c.String(maxLength: 128),
                        Resent = c.String(),
                    })
                .PrimaryKey(t => t.MessageId)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.ModalInfo",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Modal = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.AspNetRoles",
                c => new
                    {
                        Id = c.String(nullable: false, maxLength: 128),
                        Name = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => t.Name, unique: true, name: "RoleNameIndex");
            
            CreateTable(
                "dbo.Slider",
                c => new
                    {
                        SliderId = c.Int(nullable: false, identity: true),
                        ImageName = c.String(),
                        ImageUrl = c.String(),
                        Status = c.Int(nullable: false),
                        Caption = c.String(),
                    })
                .PrimaryKey(t => t.SliderId);
            
            CreateTable(
                "dbo.Transaction",
                c => new
                    {
                        TransactionId = c.Int(nullable: false, identity: true),
                        UserId = c.String(maxLength: 128),
                        ClientId = c.Int(nullable: false),
                        Amount = c.Decimal(nullable: false, precision: 18, scale: 2),
                        AmountPaid = c.Decimal(precision: 18, scale: 2),
                        Units = c.Decimal(nullable: false, precision: 18, scale: 2),
                        TransactionType = c.Int(nullable: false),
                        GatewayResponse = c.String(),
                        DateCreated = c.DateTime(nullable: false),
                        DateApproved = c.DateTime(),
                        Status = c.Int(nullable: false),
                        Note = c.String(),
                        TransactionReference = c.String(),
                        ApprovedBy = c.String(),
                    })
                .PrimaryKey(t => t.TransactionId)
                .ForeignKey("dbo.AspNetUsers", t => t.UserId)
                .Index(t => t.UserId);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Transaction", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserRoles", "RoleId", "dbo.AspNetRoles");
            DropForeignKey("dbo.Message", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.DialCode", "PriceSettingId", "dbo.PriceSetting");
            DropForeignKey("dbo.Contact", "GroupId", "dbo.Group");
            DropForeignKey("dbo.Client", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.Voucher", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.Voucher", "BatchVoucherId", "dbo.BatchVoucher");
            DropForeignKey("dbo.BatchVoucher", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserRoles", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserLogins", "UserId", "dbo.AspNetUsers");
            DropForeignKey("dbo.AspNetUserClaims", "UserId", "dbo.AspNetUsers");
            DropIndex("dbo.Transaction", new[] { "UserId" });
            DropIndex("dbo.AspNetRoles", "RoleNameIndex");
            DropIndex("dbo.Message", new[] { "UserId" });
            DropIndex("dbo.DialCode", new[] { "PriceSettingId" });
            DropIndex("dbo.Contact", new[] { "GroupId" });
            DropIndex("dbo.Client", new[] { "UserId" });
            DropIndex("dbo.Voucher", new[] { "BatchVoucherId" });
            DropIndex("dbo.Voucher", new[] { "UserId" });
            DropIndex("dbo.AspNetUserRoles", new[] { "RoleId" });
            DropIndex("dbo.AspNetUserRoles", new[] { "UserId" });
            DropIndex("dbo.AspNetUserLogins", new[] { "UserId" });
            DropIndex("dbo.AspNetUserClaims", new[] { "UserId" });
            DropIndex("dbo.AspNetUsers", "UserNameIndex");
            DropIndex("dbo.BatchVoucher", new[] { "UserId" });
            DropTable("dbo.Transaction");
            DropTable("dbo.Slider");
            DropTable("dbo.AspNetRoles");
            DropTable("dbo.ModalInfo");
            DropTable("dbo.Message");
            DropTable("dbo.MessageChunk");
            DropTable("dbo.PriceSetting");
            DropTable("dbo.DialCode");
            DropTable("dbo.Group");
            DropTable("dbo.Contact");
            DropTable("dbo.Client");
            DropTable("dbo.Voucher");
            DropTable("dbo.AspNetUserRoles");
            DropTable("dbo.AspNetUserLogins");
            DropTable("dbo.AspNetUserClaims");
            DropTable("dbo.AspNetUsers");
            DropTable("dbo.BatchVoucher");
            DropTable("dbo.BankDetail");
            DropTable("dbo.ApiSetting");
            DropTable("dbo.AdminSetting");
        }
    }
}
