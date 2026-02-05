using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace CreditAI.API.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Customers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Name = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    FinancialScore = table.Column<int>(type: "int", nullable: false),
                    HistoricText = table.Column<string>(type: "nvarchar(max)", nullable: true),
                    BehaviorEmbedding = table.Column<byte[]>(type: "VARBINARY(MAX)", nullable: true),
                    LastAnalysisDate = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Customers", x => x.Id);
                });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Customers");
        }
    }
}
