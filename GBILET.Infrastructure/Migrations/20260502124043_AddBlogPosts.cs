using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GBILET.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddBlogPosts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlogPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "text", nullable: false),
                    TitleTr = table.Column<string>(type: "text", nullable: false),
                    TitleEn = table.Column<string>(type: "text", nullable: false),
                    SummaryTr = table.Column<string>(type: "text", nullable: false),
                    SummaryEn = table.Column<string>(type: "text", nullable: false),
                    ContentTr = table.Column<string>(type: "text", nullable: false),
                    ContentEn = table.Column<string>(type: "text", nullable: false),
                    ThumbUrl = table.Column<string>(type: "text", nullable: true),
                    TagTr = table.Column<string>(type: "text", nullable: false),
                    TagEn = table.Column<string>(type: "text", nullable: false),
                    Author = table.Column<string>(type: "text", nullable: false),
                    ReadTime = table.Column<int>(type: "integer", nullable: false),
                    MetaTitleTr = table.Column<string>(type: "text", nullable: false),
                    MetaTitleEn = table.Column<string>(type: "text", nullable: false),
                    MetaDescriptionTr = table.Column<string>(type: "text", nullable: false),
                    MetaDescriptionEn = table.Column<string>(type: "text", nullable: false),
                    KeywordsTr = table.Column<string>(type: "text", nullable: false),
                    KeywordsEn = table.Column<string>(type: "text", nullable: false),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp without time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogPosts", x => x.Id);
                });

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

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BlogPosts");

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
        }
    }
}
