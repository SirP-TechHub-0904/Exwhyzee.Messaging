using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exwhyzee.Messaging.Core.Migrations
{
    public partial class UpgradeIdentityToCore : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // 1. Add missing ASP.NET Core Identity columns to existing MVC 5 tables
            migrationBuilder.AddColumn<string>(name: "NormalizedUserName", table: "AspNetUsers", type: "nvarchar(256)", maxLength: 256, nullable: true);
            migrationBuilder.AddColumn<string>(name: "NormalizedEmail", table: "AspNetUsers", type: "nvarchar(256)", maxLength: 256, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ConcurrencyStamp", table: "AspNetUsers", type: "nvarchar(max)", nullable: true);
            migrationBuilder.AddColumn<DateTimeOffset>(name: "LockoutEnd", table: "AspNetUsers", type: "datetimeoffset", nullable: true);

            migrationBuilder.AddColumn<string>(name: "NormalizedName", table: "AspNetRoles", type: "nvarchar(256)", maxLength: 256, nullable: true);
            migrationBuilder.AddColumn<string>(name: "ConcurrencyStamp", table: "AspNetRoles", type: "nvarchar(max)", nullable: true);

            // 2. Create the two brand new tables that ASP.NET Core Identity needs
            // Note: Changed nvarchar(450) to nvarchar(128) to match old MVC 5 ID lengths!
            migrationBuilder.CreateTable(
                name: "AspNetUserTokens",
                columns: table => new
                {
                    UserId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    LoginProvider = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Name = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetUserTokens", x => new { x.UserId, x.LoginProvider, x.Name });
                    table.ForeignKey(
                        name: "FK_AspNetUserTokens_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AspNetRoleClaims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    RoleId = table.Column<string>(type: "nvarchar(128)", maxLength: 128, nullable: false),
                    ClaimType = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    ClaimValue = table.Column<string>(type: "nvarchar(max)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AspNetRoleClaims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AspNetRoleClaims_AspNetRoles_RoleId",
                        column: x => x.RoleId,
                        principalTable: "AspNetRoles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AspNetRoleClaims_RoleId",
                table: "AspNetRoleClaims",
                column: "RoleId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Empty down method for safety
        }
    }
}