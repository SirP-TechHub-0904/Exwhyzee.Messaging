using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exwhyzee.Messaging.Core.Migrations
{
    public partial class AddTransactionPayoutTracking : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Transaction]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Transaction]') AND name = 'PaymentSource')
                BEGIN
                    ALTER TABLE [Transaction] ADD [PaymentSource] nvarchar(100) NULL DEFAULT 'Paystack';
                END

                IF OBJECT_ID(N'[Transaction]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Transaction]') AND name = 'IsAdminTransferred')
                BEGIN
                    ALTER TABLE [Transaction] ADD [IsAdminTransferred] bit NOT NULL DEFAULT 0;
                END

                IF OBJECT_ID(N'[Transaction]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Transaction]') AND name = 'DateTransferred')
                BEGIN
                    ALTER TABLE [Transaction] ADD [DateTransferred] datetime2 NULL;
                END

                IF OBJECT_ID(N'[Transaction]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Transaction]') AND name = 'TransferredBy')
                BEGIN
                    ALTER TABLE [Transaction] ADD [TransferredBy] nvarchar(200) NULL;
                END
            ");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
