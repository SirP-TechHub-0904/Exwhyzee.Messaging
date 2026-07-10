namespace Exwhyzee.Messaging.Web.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class intupdatenew : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.ApiSetting", "Token", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.ApiSetting", "Token");
        }
    }
}
