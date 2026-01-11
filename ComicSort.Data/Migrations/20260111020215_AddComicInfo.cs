using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComicSort.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddComicInfo : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ComicInfo",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ComicBookId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Series = table.Column<string>(type: "TEXT", nullable: true),
                    Number = table.Column<string>(type: "TEXT", nullable: true),
                    AlternateSeries = table.Column<string>(type: "TEXT", nullable: true),
                    Summary = table.Column<string>(type: "TEXT", nullable: true),
                    Writer = table.Column<string>(type: "TEXT", nullable: true),
                    Penciller = table.Column<string>(type: "TEXT", nullable: true),
                    Inker = table.Column<string>(type: "TEXT", nullable: true),
                    Colorist = table.Column<string>(type: "TEXT", nullable: true),
                    Letterer = table.Column<string>(type: "TEXT", nullable: true),
                    CoverArtist = table.Column<string>(type: "TEXT", nullable: true),
                    Editor = table.Column<string>(type: "TEXT", nullable: true),
                    Publisher = table.Column<string>(type: "TEXT", nullable: true),
                    PageCount = table.Column<int>(type: "INTEGER", nullable: true),
                    Characters = table.Column<string>(type: "TEXT", nullable: true),
                    Teams = table.Column<string>(type: "TEXT", nullable: true),
                    Locations = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComicInfo", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComicInfo_ComicBooks_ComicBookId",
                        column: x => x.ComicBookId,
                        principalTable: "ComicBooks",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ComicInfoPages",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "TEXT", nullable: false),
                    ComicInfoId = table.Column<Guid>(type: "TEXT", nullable: false),
                    Image = table.Column<int>(type: "INTEGER", nullable: false),
                    ImageWidth = table.Column<int>(type: "INTEGER", nullable: true),
                    ImageHeight = table.Column<int>(type: "INTEGER", nullable: true),
                    Type = table.Column<string>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ComicInfoPages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ComicInfoPages_ComicInfo_ComicInfoId",
                        column: x => x.ComicInfoId,
                        principalTable: "ComicInfo",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ComicInfo_ComicBookId",
                table: "ComicInfo",
                column: "ComicBookId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ComicInfoPages_ComicInfoId_Image",
                table: "ComicInfoPages",
                columns: new[] { "ComicInfoId", "Image" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ComicInfoPages");

            migrationBuilder.DropTable(
                name: "ComicInfo");
        }
    }
}
