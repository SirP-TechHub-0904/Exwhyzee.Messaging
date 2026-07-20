using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exwhyzee.Messaging.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddMissingClientColumns : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // migrationBuilder.DropColumn(
            //     name: "Response_data",
            //     table: "Message");

            migrationBuilder.Sql(@"
IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'ApiKey' AND Object_ID = Object_ID(N'Client'))
BEGIN
    ALTER TABLE [Client] ADD [ApiKey] nvarchar(max) NULL;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'Discount' AND Object_ID = Object_ID(N'Client'))
BEGIN
    ALTER TABLE [Client] ADD [Discount] decimal(18,2) NOT NULL DEFAULT 0.0;
END

IF NOT EXISTS (SELECT * FROM sys.columns WHERE Name = N'AllowNotifications' AND Object_ID = Object_ID(N'Client'))
BEGIN
    ALTER TABLE [Client] ADD [AllowNotifications] int NOT NULL DEFAULT 0;
END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Response_data",
                table: "Message",
                type: "nvarchar(max)",
                nullable: true);
        }
    }
}
