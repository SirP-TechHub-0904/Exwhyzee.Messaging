namespace Exwhyzee.Messaging.Web.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class update : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.Emailing", "EmailingCategoryId", "dbo.EmailingCategory");
            DropIndex("dbo.Emailing", new[] { "EmailingCategoryId" });
            DropTable("dbo.EmailingCategory");
            DropTable("dbo.Emailing");
            DropTable("dbo.MailHost");
            DropTable("dbo.NewsletterEmailTemplate");
            DropTable("dbo.ProductEmailTemplate");
            DropTable("dbo.WelcomeEmailTemplate");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.WelcomeEmailTemplate",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Logo = c.String(),
                        HeaderImage = c.String(),
                        Title = c.String(),
                        LetterTitle = c.String(),
                        FirstMessage = c.String(),
                        SecondMessage = c.String(),
                        Conclusion = c.String(),
                        Signature = c.String(),
                        Name = c.String(),
                        Rank = c.String(),
                        ButtonTitle = c.String(),
                        ButtonLink = c.String(),
                        InfoColor = c.String(),
                        InfoTitle = c.String(),
                        InfoMessage = c.String(),
                        InfoButtonLink = c.String(),
                        InfoButtonTitle = c.String(),
                        SocialFace = c.String(),
                        SocialInsta = c.String(),
                        SocialTwit = c.String(),
                        SocialLinkedIn = c.String(),
                        FooterMessage = c.String(),
                        FooterTel = c.String(),
                        FooterMail = c.String(),
                        FooterAddress = c.String(),
                        FooterCopyRight = c.String(),
                        FooterSiteLink = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.ProductEmailTemplate",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Logo = c.String(),
                        HeaderImage = c.String(),
                        Title = c.String(),
                        FirstMessage = c.String(),
                        SecondMessage = c.String(),
                        ButtonTitle = c.String(),
                        ButtonLink = c.String(),
                        ProductOneImage = c.String(),
                        ProductOneTitle = c.String(),
                        ProductOneDescription = c.String(),
                        ProductOneBtnColor = c.String(),
                        ProductOneBtnLink = c.String(),
                        ProductOneBtnTitle = c.String(),
                        ProductTwoImage = c.String(),
                        ProductTwoTitle = c.String(),
                        ProductTwoDescription = c.String(),
                        ProductTwoBtnColor = c.String(),
                        ProductTwoBtnLink = c.String(),
                        ProductTwoBtnTitle = c.String(),
                        InfoColor = c.String(),
                        InfoTitle = c.String(),
                        InfoMessage = c.String(),
                        InfoButtonLink = c.String(),
                        InfoButtonTitle = c.String(),
                        SocialFace = c.String(),
                        SocialInsta = c.String(),
                        SocialTwit = c.String(),
                        SocialLinkedIn = c.String(),
                        FooterMessage = c.String(),
                        FooterTel = c.String(),
                        FooterMail = c.String(),
                        FooterAddress = c.String(),
                        FooterCopyRight = c.String(),
                        FooterSiteLink = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.NewsletterEmailTemplate",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Logo = c.String(),
                        HeaderImage = c.String(),
                        Title = c.String(),
                        FirstMessage = c.String(),
                        SecondMessage = c.String(),
                        ButtonTitle = c.String(),
                        ButtonLink = c.String(),
                        ButtonBgColor = c.String(),
                        ButtonTxtColor = c.String(),
                        InfoColor = c.String(),
                        InfoTitle = c.String(),
                        InfoMessage = c.String(),
                        InfoButtonLink = c.String(),
                        InfoButtonTitle = c.String(),
                        SocialFace = c.String(),
                        SocialInsta = c.String(),
                        SocialTwit = c.String(),
                        SocialLinkedIn = c.String(),
                        FooterMessage = c.String(),
                        FooterTel = c.String(),
                        FooterMail = c.String(),
                        FooterAddress = c.String(),
                        FooterCopyRight = c.String(),
                        FooterSiteLink = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.MailHost",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        HostMail = c.String(),
                        Email = c.String(),
                        KeyP = c.String(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Emailing",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.String(),
                        Email = c.String(),
                        FullName = c.String(),
                        Date = c.DateTime(nullable: false),
                        Active = c.Boolean(nullable: false),
                        EmailingCategoryId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.EmailingCategory",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.String(),
                        Name = c.String(),
                        Date = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateIndex("dbo.Emailing", "EmailingCategoryId");
            AddForeignKey("dbo.Emailing", "EmailingCategoryId", "dbo.EmailingCategory", "Id", cascadeDelete: true);
        }
    }
}
