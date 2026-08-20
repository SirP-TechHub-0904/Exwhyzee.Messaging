using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exwhyzee.Messaging.Core.Migrations
{
    /// <inheritdoc />
    public partial class update12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPaystackOutflow",
                table: "Transaction",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "IsPaystackOutflow",
                table: "Transaction");
        }
    }
}
