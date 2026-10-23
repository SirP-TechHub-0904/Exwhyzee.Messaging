using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exwhyzee.Messaging.Core.Migrations
{
    /// <inheritdoc />
    public partial class AddPaystackTransferOutflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PaystackTransferOutflow",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    TransferCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TransferReference = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Amount = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    RecipientCode = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    AccountName = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    AccountNumber = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    BankName = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    Status = table.Column<string>(type: "nvarchar(50)", maxLength: 50, nullable: true),
                    InitiatedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    DateInitiated = table.Column<DateTime>(type: "datetime2", nullable: false),
                    DateVerified = table.Column<DateTime>(type: "datetime2", nullable: true),
                    VerifiedBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    PaystackMessage = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsRolledBack = table.Column<bool>(type: "bit", nullable: false),
                    DateRolledBack = table.Column<DateTime>(type: "datetime2", nullable: true),
                    RolledBackBy = table.Column<string>(type: "nvarchar(200)", maxLength: 200, nullable: true),
                    TransactionIdsJson = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TransactionsCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PaystackTransferOutflow", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PaystackTransferOutflow");
        }
    }
}
