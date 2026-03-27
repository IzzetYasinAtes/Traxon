using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Traxon.CryptoTrader.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCreate : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Candles",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Interval = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    OpenTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    CloseTime = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Open = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    High = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    Low = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    Close = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    Volume = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    QuoteVolume = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    TradeCount = table.Column<int>(type: "int", nullable: false),
                    IsClosed = table.Column<bool>(type: "bit", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Candles", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PortfolioSnapshots",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("SqlServer:Identity", "1, 1"),
                    Engine = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Timestamp = table.Column<DateTime>(type: "datetime2", nullable: false),
                    Balance = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    OpenPositionCount = table.Column<int>(type: "int", nullable: false),
                    TotalExposure = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    TotalPnL = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    WinRate = table.Column<decimal>(type: "decimal(5,2)", nullable: true),
                    TradeCount = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortfolioSnapshots", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Trades",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Engine = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TimeFrame = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    EntryPrice = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    ExitPrice = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    FairValue = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    Edge = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    PositionSize = table.Column<decimal>(type: "decimal(18,4)", nullable: false),
                    KellyFraction = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    MuEstimate = table.Column<decimal>(type: "decimal(18,12)", nullable: false),
                    SigmaEstimate = table.Column<decimal>(type: "decimal(18,12)", nullable: false),
                    Regime = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    IndicatorSnapshot = table.Column<string>(type: "nvarchar(max)", nullable: false),
                    EntryReason = table.Column<string>(type: "nvarchar(500)", maxLength: 500, nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime2", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "datetime2", nullable: true),
                    Status = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    Outcome = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: true),
                    PnL = table.Column<decimal>(type: "decimal(18,4)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Trades", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Candles_OpenTime",
                table: "Candles",
                column: "OpenTime");

            migrationBuilder.CreateIndex(
                name: "IX_PortfolioSnapshots_Engine_Timestamp",
                table: "PortfolioSnapshots",
                columns: new[] { "Engine", "Timestamp" });

            migrationBuilder.CreateIndex(
                name: "IX_Trades_Engine_OpenedAt",
                table: "Trades",
                columns: new[] { "Engine", "OpenedAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Candles");

            migrationBuilder.DropTable(
                name: "PortfolioSnapshots");

            migrationBuilder.DropTable(
                name: "Trades");
        }
    }
}
