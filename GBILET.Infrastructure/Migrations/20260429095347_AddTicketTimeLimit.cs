using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GBILET.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddTicketTimeLimit : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "TicketTimeLimit",
                table: "Bookings",
                type: "timestamp without time zone",
                nullable: true);

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 224, DateTimeKind.Utc).AddTicks(9305));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 224, DateTimeKind.Utc).AddTicks(9312));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 224, DateTimeKind.Utc).AddTicks(9314));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 224, DateTimeKind.Utc).AddTicks(9316));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 224, DateTimeKind.Utc).AddTicks(9317));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 224, DateTimeKind.Utc).AddTicks(9319));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 224, DateTimeKind.Utc).AddTicks(9321));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 224, DateTimeKind.Utc).AddTicks(9323));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 224, DateTimeKind.Utc).AddTicks(9324));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 224, DateTimeKind.Utc).AddTicks(9326));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 225, DateTimeKind.Utc).AddTicks(556));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 225, DateTimeKind.Utc).AddTicks(561));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 225, DateTimeKind.Utc).AddTicks(563));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 225, DateTimeKind.Utc).AddTicks(565));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 225, DateTimeKind.Utc).AddTicks(567));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 9, 53, 46, 225, DateTimeKind.Utc).AddTicks(568));
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "TicketTimeLimit",
                table: "Bookings");

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(426));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(432));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(434));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(435));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(436));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(438));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(439));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(441));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(442));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(443));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(758));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(761));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(762));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(764));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(765));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 23, 11, 37, 9, 895, DateTimeKind.Utc).AddTicks(766));
        }
    }
}
