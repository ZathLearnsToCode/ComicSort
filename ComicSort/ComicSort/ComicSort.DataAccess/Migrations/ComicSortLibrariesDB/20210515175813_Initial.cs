using System;
using Microsoft.EntityFrameworkCore.Migrations;

namespace ComicSort.DataAccess.Migrations.ComicSortLibrariesDB
{
    public partial class Initial : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComicSortLibraries",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    LibraryPath = table.Column<string>(type: "TEXT", nullable: true),
                    LibraryFile = table.Column<string>(type: "TEXT", nullable: true),
                    LibraryName = table.Column<string>(type: "TEXT", nullable: true),
                    Created = table.Column<string>(type: "TEXT", nullable: true),
                    LastAccessed = table.Column<string>(type: "TEXT", nullable: true),
                    LibraryType = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComicSortLibraries", x => x.Id);
                });
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComicSortLibraries");
        }
    }
}
