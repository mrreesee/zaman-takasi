using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ZamanTakasi.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddOpeningBalanceUniqueIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_LedgerEntries_UserId_OpeningBalance",
                table: "LedgerEntries",
                column: "UserId",
                unique: true,
                filter: "\"EntryType\" = 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_LedgerEntries_UserId_OpeningBalance",
                table: "LedgerEntries");
        }
    }
}
