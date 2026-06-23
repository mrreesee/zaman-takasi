using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZamanTakasi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddListingLanguage : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Language",
                table: "Listings",
                type: "character varying(8)",
                maxLength: 8,
                nullable: false,
                defaultValue: "tr");

            migrationBuilder.CreateIndex(
                name: "IX_Listings_Language",
                table: "Listings",
                column: "Language");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Listings_Language",
                table: "Listings");

            migrationBuilder.DropColumn(
                name: "Language",
                table: "Listings");
        }
    }
}
