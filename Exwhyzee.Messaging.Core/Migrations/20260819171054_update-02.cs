using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exwhyzee.Messaging.Core.Migrations
{
    /// <inheritdoc />
    public partial class update02 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"
                IF OBJECT_ID(N'[Contact]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Contact]') AND name = 'IsActive')
                BEGIN
                    ALTER TABLE [Contact] ADD [IsActive] bit NOT NULL DEFAULT 1;
                END

                IF OBJECT_ID(N'[Client]', N'U') IS NOT NULL AND NOT EXISTS (SELECT 1 FROM sys.columns WHERE object_id = OBJECT_ID(N'[Client]') AND name = 'GeminiApiKey')
                BEGIN
                    ALTER TABLE [Client] ADD [GeminiApiKey] nvarchar(500) NULL;
                END

                IF OBJECT_ID(N'[AppNotification]', N'U') IS NULL
                BEGIN
                    CREATE TABLE [AppNotification] (
                        [Id] int NOT NULL IDENTITY(1,1),
                        [UserId] nvarchar(450) NOT NULL,
                        [Title] nvarchar(200) NOT NULL,
                        [Message] nvarchar(max) NOT NULL,
                        [NotificationType] nvarchar(50) NULL,
                        [ActionUrl] nvarchar(500) NULL,
                        [IsRead] bit NOT NULL DEFAULT 0,
                        [DateCreated] datetime2 NOT NULL DEFAULT GETUTCDATE(),
                        [DateRead] datetime2 NULL,
                        CONSTRAINT [PK_AppNotification] PRIMARY KEY ([Id])
                    );
                END
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
