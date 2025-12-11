using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ComicSort.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddedIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_ComicBooks_FilePath",
                table: "ComicBooks",
                column: "FilePath",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_ComicBooks_FilePath",
                table: "ComicBooks");
        }
    }
}
