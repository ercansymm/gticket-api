using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GBILET.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportTicketGuestEmail : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "GuestEmail",
                table: "SupportTickets",
                type: "text",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(5975));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(5980));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(5981));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(5983));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(5984));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(5986));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(5988));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(5990));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(5992));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(5994));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(6377));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(6381));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(6383));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(6384));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(6386));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 6, 14, 39, 24, 480, DateTimeKind.Utc).AddTicks(6387));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "GuestEmail",
                table: "SupportTickets");

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(1976));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(1980));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(1982));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(1983));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(1985));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(1986));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(1987));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(1989));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(1990));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(1992));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(2307));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(2311));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(2313));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(2314));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(2315));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 5, 2, 12, 40, 42, 765, DateTimeKind.Utc).AddTicks(2317));
        }
    }
}
