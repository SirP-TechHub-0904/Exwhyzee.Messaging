using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Exwhyzee.Messaging.Core.Migrations
{
    /// <inheritdoc />
    public partial class update14 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ContentHtml",
                table: "ModalInfo",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DateCreated",
                table: "ModalInfo",
                type: "datetime2",
                nullable: false,
                defaultValue: new DateTime(1, 1, 1, 0, 0, 0, 0, DateTimeKind.Unspecified));

            migrationBuilder.AddColumn<string>(
                name: "ImageUrl",
                table: "ModalInfo",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsActive",
                table: "ModalInfo",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<int>(
                name: "ShowFrequencyDays",
                table: "ModalInfo",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "Title",
                table: "ModalInfo",
                type: "nvarchar(max)",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "UseImage",
                table: "ModalInfo",
                type: "bit",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "UseText",
                table: "ModalInfo",
                type: "bit",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ContentHtml",
                table: "ModalInfo");

            migrationBuilder.DropColumn(
                name: "DateCreated",
                table: "ModalInfo");

            migrationBuilder.DropColumn(
                name: "ImageUrl",
                table: "ModalInfo");

            migrationBuilder.DropColumn(
                name: "IsActive",
                table: "ModalInfo");

            migrationBuilder.DropColumn(
                name: "ShowFrequencyDays",
                table: "ModalInfo");

            migrationBuilder.DropColumn(
                name: "Title",
                table: "ModalInfo");

            migrationBuilder.DropColumn(
                name: "UseImage",
                table: "ModalInfo");

            migrationBuilder.DropColumn(
                name: "UseText",
                table: "ModalInfo");
        }
    }
}
