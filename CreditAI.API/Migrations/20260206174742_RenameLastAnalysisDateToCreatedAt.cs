using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditAI.API.Migrations
{
    /// <inheritdoc />
    public partial class RenameLastAnalysisDateToCreatedAt : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.RenameColumn(
                name: "LastAnalysisDate",
                table: "Customers",
                newName: "CreatedAt");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {

        }
    }
}
