using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ComicSort.DataAccess.Migrations
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComicBookLists",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComicBookLists", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Pages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Image = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageWidth = table.Column<long>(type: "INTEGER", nullable: false),
                    ImageHeight = table.Column<long>(type: "INTEGER", nullable: false),
                    Type = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Pages", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "ComicBooks",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    File = table.Column<string>(type: "TEXT", nullable: true),
                    Series = table.Column<string>(type: "TEXT", nullable: true),
                    IssueNumber = table.Column<string>(type: "TEXT", nullable: true),
                    Volume = table.Column<string>(type: "TEXT", nullable: true),
                    PageCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PagesId = table.Column<int>(type: "INTEGER", nullable: true),
                    DateAdded = table.Column<string>(type: "TEXT", nullable: true),
                    FileSize = table.Column<long>(type: "INTEGER", nullable: false),
                    DateModified = table.Column<string>(type: "TEXT", nullable: true),
                    DateCreated = table.Column<string>(type: "TEXT", nullable: true),
                    ComicBookListId = table.Column<int>(type: "INTEGER", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComicBooks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComicBooks_ComicBookLists_ComicBookListId",
                        column: x => x.ComicBookListId,
                        principalTable: "ComicBookLists",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ComicBooks_Pages_PagesId",
                        column: x => x.PagesId,
                        principalTable: "Pages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComicBooks_ComicBookListId",
                table: "ComicBooks",
                column: "ComicBookListId");

            migrationBuilder.CreateIndex(
                name: "IX_ComicBooks_PagesId",
                table: "ComicBooks",
                column: "PagesId");
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComicBooks");

            migrationBuilder.DropTable(
                name: "ComicBookLists");

            migrationBuilder.DropTable(
                name: "Pages");
        }
    }
}
