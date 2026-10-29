using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exwhyzee.Messaging.Core.Migrations
{
    /// <inheritdoc />
    public partial class kycupdate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "KycSetting",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    IsKycEnabled = table.Column<bool>(type: "bit", nullable: false),
                    TriggerRule = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    SmsSentThreshold = table.Column<int>(type: "int", nullable: false),
                    UnitsFundedThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    SingleTopUpUnitsThreshold = table.Column<decimal>(type: "decimal(18,2)", nullable: false),
                    EnforceBlockOnCompose = table.Column<bool>(type: "bit", nullable: false),
                    EnforceBlockOnFunding = table.Column<bool>(type: "bit", nullable: false),
                    RequirePhoneVerification = table.Column<bool>(type: "bit", nullable: false),
                    RequireAddress = table.Column<bool>(type: "bit", nullable: false),
                    RequirePassportPhoto = table.Column<bool>(type: "bit", nullable: false),
                    RequireGovernmentId = table.Column<bool>(type: "bit", nullable: false),
                    AcceptedIdTypes = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdatedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdatedDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KycSetting", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "UserKycProfile",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    UserId = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClientId = table.Column<int>(type: "int", nullable: true),
                    ResidentialAddress = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    City = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    State = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    Country = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IsPhoneVerified = table.Column<bool>(type: "bit", nullable: false),
                    PhoneVerifiedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PhoneOtpCode = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    PhoneOtpExpiry = table.Column<DateTime>(type: "datetime2", nullable: true),
                    PassportPhotoPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdNumber = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdDocumentFrontPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    IdDocumentBackPath = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    KycStatus = table.Column<int>(type: "int", nullable: false),
                    TriggerReason = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    AdminReviewNote = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    TriggeredDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    SubmittedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedDate = table.Column<DateTime>(type: "datetime2", nullable: true),
                    ReviewedBy = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    LastUpdated = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UserKycProfile", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KycSetting");

            migrationBuilder.DropTable(
                name: "UserKycProfile");
        }
    }
}
