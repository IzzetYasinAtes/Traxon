using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Traxon.CryptoTrader.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddFuturesSnapshots : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "FuturesSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    FundingRate = table.Column<decimal>(type: "decimal(18,10)", nullable: false),
                    OpenInterest = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OrderBookImbalance = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    ObiPersistence = table.Column<decimal>(type: "decimal(5,2)", nullable: false),
                    BidVolume = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    AskVolume = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FuturesSnapshots", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_FuturesSnapshots_Symbol_Timestamp",
                table: "FuturesSnapshots",
                columns: new[] { "Symbol", "Timestamp" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "FuturesSnapshots");
        }
    }
}
