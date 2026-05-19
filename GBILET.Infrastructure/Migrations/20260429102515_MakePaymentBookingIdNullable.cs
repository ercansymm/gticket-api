using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GBILET.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class MakePaymentBookingIdNullable : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Bookings_BookingId",
                table: "Payments");

            migrationBuilder.AlterColumn<Guid>(
                name: "BookingId",
                table: "Payments",
                type: "uuid",
                nullable: true,
                oldClrType: typeof(Guid),
                oldType: "uuid");

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(1988));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(1992));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(1994));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(1995));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(1996));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(1998));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(1999));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(2000));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(2002));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(2003));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(2226));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(2230));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(2231));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(2232));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(2234));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 25, 14, 501, DateTimeKind.Utc).AddTicks(2235));

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Bookings_BookingId",
                table: "Payments",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Bookings_BookingId",
                table: "Payments");

            migrationBuilder.AlterColumn<Guid>(
                name: "BookingId",
                table: "Payments",
                type: "uuid",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"),
                oldClrType: typeof(Guid),
                oldType: "uuid",
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(679));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(684));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(686));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(687));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(688));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(689));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(691));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(692));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(694));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(695));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(1241));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(1247));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(1248));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(1250));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(1251));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 29, 10, 10, 32, 968, DateTimeKind.Utc).AddTicks(1252));

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Bookings_BookingId",
                table: "Payments",
                column: "BookingId",
                principalTable: "Bookings",
                principalColumn: "Id",
                onDelete: ReferentialAction.Cascade);
        }
    }
}
