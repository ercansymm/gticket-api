using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace GBILET.Infrastructure.Migrations
{
    public partial class AddBlogPosts : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BlogPosts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    TitleTr = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    TitleEn = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    SummaryTr = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    SummaryEn = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    ContentTr = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    ContentEn = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    ThumbUrl = table.Column<string>(type: "text", nullable: true),
                    TagTr = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    TagEn = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    Author = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    ReadTime = table.Column<int>(type: "integer", nullable: false, defaultValue: 0),
                    MetaTitleTr = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    MetaTitleEn = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    MetaDescriptionTr = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    MetaDescriptionEn = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    KeywordsTr = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    KeywordsEn = table.Column<string>(type: "text", nullable: false, defaultValue: ""),
                    IsPublished = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BlogPosts", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(name: "BlogPosts");
        }
    }
}
