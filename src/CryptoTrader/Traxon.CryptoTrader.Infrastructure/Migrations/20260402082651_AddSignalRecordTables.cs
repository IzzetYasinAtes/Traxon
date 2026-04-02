using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Traxon.CryptoTrader.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddSignalRecordTables : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SignalRecords",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    Symbol = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    TimeFrame = table.Column<string>(type: "nvarchar(5)", maxLength: 5, nullable: false),
                    Direction = table.Column<string>(type: "nvarchar(10)", maxLength: 10, nullable: false),
                    FairValue = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    MarketPrice = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    Edge = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    KellyFraction = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    MuEstimate = table.Column<decimal>(type: "decimal(18,12)", nullable: false),
                    SigmaEstimate = table.Column<decimal>(type: "decimal(18,12)", nullable: false),
                    Regime = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    SignalScore = table.Column<decimal>(type: "decimal(18,8)", nullable: true),
                    Rsi = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    MacdHistogram = table.Column<decimal>(type: "decimal(18,8)", nullable: false),
                    BullishCount = table.Column<int>(type: "int", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalRecords", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "SignalEngineResults",
                columns: table => new
                {
                    Id = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    SignalRecordId = table.Column<Guid>(type: "uniqueidentifier", nullable: false),
                    EngineName = table.Column<string>(type: "nvarchar(20)", maxLength: 20, nullable: false),
                    Accepted = table.Column<bool>(type: "bit", nullable: false),
                    RejectionReason = table.Column<string>(type: "nvarchar(100)", maxLength: 100, nullable: true),
                    TradeId = table.Column<Guid>(type: "uniqueidentifier", nullable: true),
                    EvaluatedAt = table.Column<DateTime>(type: "datetime2", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SignalEngineResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SignalEngineResults_SignalRecords_SignalRecordId",
                        column: x => x.SignalRecordId,
                        principalTable: "SignalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SignalEngineResults_SignalRecordId",
                table: "SignalEngineResults",
                column: "SignalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_SignalRecords_GeneratedAt",
                table: "SignalRecords",
                column: "GeneratedAt",
                descending: new bool[0]);

            migrationBuilder.CreateIndex(
                name: "IX_SignalRecords_Symbol_GeneratedAt",
                table: "SignalRecords",
                columns: new[] { "Symbol", "GeneratedAt" },
                descending: new[] { false, true });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SignalEngineResults");

            migrationBuilder.DropTable(
                name: "SignalRecords");
        }
    }
}
