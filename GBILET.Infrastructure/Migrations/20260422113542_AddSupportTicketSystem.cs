using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace GBILET.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSupportTicketSystem : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SupportTickets",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketNumber = table.Column<string>(type: "character varying(32)", maxLength: 32, nullable: false),
                    UserId = table.Column<Guid>(type: "uuid", nullable: false),
                    BookingId = table.Column<Guid>(type: "uuid", nullable: true),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Subject = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    ClosedByAdminId = table.Column<Guid>(type: "uuid", nullable: true),
                    LastActivityAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTickets", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTickets_AdminUsers_ClosedByAdminId",
                        column: x => x.ClosedByAdminId,
                        principalTable: "AdminUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Bookings_BookingId",
                        column: x => x.BookingId,
                        principalTable: "Bookings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_SupportTickets_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SupportTicketMessages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uuid", nullable: false),
                    TicketId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderType = table.Column<int>(type: "integer", nullable: false),
                    SenderId = table.Column<Guid>(type: "uuid", nullable: false),
                    SenderDisplayName = table.Column<string>(type: "character varying(128)", maxLength: 128, nullable: false),
                    Body = table.Column<string>(type: "text", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SupportTicketMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SupportTicketMessages_SupportTickets_TicketId",
                        column: x => x.TicketId,
                        principalTable: "SupportTickets",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1359));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1363));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1365));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1366));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1368));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1369));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1371));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1373));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1375));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1376));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1814));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1818));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1820));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1821));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1823));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 22, 11, 35, 41, 855, DateTimeKind.Utc).AddTicks(1824));

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketMessages_CreatedAt",
                table: "SupportTicketMessages",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTicketMessages_TicketId",
                table: "SupportTicketMessages",
                column: "TicketId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_BookingId",
                table: "SupportTickets",
                column: "BookingId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_ClosedByAdminId",
                table: "SupportTickets",
                column: "ClosedByAdminId");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_CreatedAt",
                table: "SupportTickets",
                column: "CreatedAt");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_LastActivityAt",
                table: "SupportTickets",
                column: "LastActivityAt");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Status",
                table: "SupportTickets",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_TicketNumber",
                table: "SupportTickets",
                column: "TicketNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_Type",
                table: "SupportTickets",
                column: "Type");

            migrationBuilder.CreateIndex(
                name: "IX_SupportTickets_UserId",
                table: "SupportTickets",
                column: "UserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SupportTicketMessages");

            migrationBuilder.DropTable(
                name: "SupportTickets");

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9589));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9593));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9594));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9595));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9597));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9598));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 7,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9600));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 8,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9601));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 9,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9602));

            migrationBuilder.UpdateData(
                table: "Airlines",
                keyColumn: "Id",
                keyValue: 10,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9604));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 1,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9838));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 2,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9843));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 3,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9844));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 4,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9846));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 5,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9847));

            migrationBuilder.UpdateData(
                table: "PopularRoutes",
                keyColumn: "Id",
                keyValue: 6,
                column: "CreatedAt",
                value: new DateTime(2026, 4, 21, 12, 2, 11, 945, DateTimeKind.Utc).AddTicks(9848));
        }
    }
}
