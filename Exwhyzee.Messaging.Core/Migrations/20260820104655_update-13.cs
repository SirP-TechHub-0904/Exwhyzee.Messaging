using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exwhyzee.Messaging.Core.Migrations
{
    /// <inheritdoc />
    public partial class update13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "LastLowUnitAlertDate",
                table: "Client",
                type: "datetime2",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "LowUnitReminderThreshold",
                table: "Client",
                type: "decimal(18,2)",
                nullable: false,
                defaultValue: 50.0m);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "LastLowUnitAlertDate",
                table: "Client");

            migrationBuilder.DropColumn(
                name: "LowUnitReminderThreshold",
                table: "Client");
        }
    }
}
