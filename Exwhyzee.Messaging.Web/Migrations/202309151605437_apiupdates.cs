namespace Exwhyzee.Messaging.Web.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class apiupdates : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.XyzSenderID",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SenderId = c.String(),
                        ClientId = c.Int(nullable: false),
                        XYZ_status = c.String(),
                        XYZ_error_code = c.String(),
                        XYZ_msg = c.String(),
                        Verify_msg = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Client", t => t.ClientId, cascadeDelete: true)
                .Index(t => t.ClientId);
            
            AddColumn("dbo.Message", "Response_status", c => c.String());
            AddColumn("dbo.Message", "Response_error_code", c => c.String());
            AddColumn("dbo.Message", "Response_cost", c => c.String());
            AddColumn("dbo.Message", "Response_msg", c => c.String());
            AddColumn("dbo.Message", "Response_length", c => c.Int(nullable: false));
            AddColumn("dbo.Message", "Response_page", c => c.Int(nullable: false));
            AddColumn("dbo.Message", "Response_balance", c => c.String());
            AddColumn("dbo.Message", "Response_BalanceResponse", c => c.String());
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.XyzSenderID", "ClientId", "dbo.Client");
            DropIndex("dbo.XyzSenderID", new[] { "ClientId" });
            DropColumn("dbo.Message", "Response_BalanceResponse");
            DropColumn("dbo.Message", "Response_balance");
            DropColumn("dbo.Message", "Response_page");
            DropColumn("dbo.Message", "Response_length");
            DropColumn("dbo.Message", "Response_msg");
            DropColumn("dbo.Message", "Response_cost");
            DropColumn("dbo.Message", "Response_error_code");
            DropColumn("dbo.Message", "Response_status");
            DropTable("dbo.XyzSenderID");
        }
    }
}
