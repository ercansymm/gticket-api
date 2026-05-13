using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GBILET.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOtpCodeAndPhoneVerification : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "IsPhoneVerified",
                table: "Users",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            // Mevcut kullanıcılar grandfather: zaten giriş yapmış sayılır, telefon doğrulanmış kabul edilir
            migrationBuilder.Sql(@"UPDATE ""Users"" SET ""IsPhoneVerified"" = TRUE WHERE ""IsPhoneVerified"" = FALSE");

            migrationBuilder.CreateTable(
                name: "OtpCodes",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    Phone = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    Purpose = table.Column<int>(type: "integer", nullable: false),
                    AttemptCount = table.Column<int>(type: "integer", nullable: false),
                    IsUsed = table.Column<bool>(type: "boolean", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_OtpCodes", x => x.Id);
                });

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1395));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1398));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1400));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1401));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1403));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1404));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1405));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1407));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1408));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1410));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1677));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1681));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1720));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1721));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1723));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 11, 15, 43, 650, DateTimeKind.Utc).AddTicks(1724));

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_ExpiresAt",
                table: "OtpCodes",
                column: "ExpiresAt");

            migrationBuilder.CreateIndex(
                name: "IX_OtpCodes_Phone_Purpose_IsUsed",
                table: "OtpCodes",
                columns: new[] { "Phone", "Purpose", "IsUsed" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OtpCodes");

            migrationBuilder.DropColumn(
                name: "IsPhoneVerified",
                table: "Users");

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4579));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4584));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4585));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4586));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4588));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4589));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4590));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4592));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4593));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4595));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4965));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4969));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4970));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4971));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4973));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 13, 9, 42, 19, 67, DateTimeKind.Utc).AddTicks(4974));
        }
    }
}
