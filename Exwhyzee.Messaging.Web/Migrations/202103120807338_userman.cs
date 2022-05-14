namespace Exwhyzee.Messaging.Web.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class userman : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.AspNetUsers", "Code", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.AspNetUsers", "Code");
        }
    }
}
