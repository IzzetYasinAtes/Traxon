using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Traxon.CryptoTrader.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddUniqueOpenTradeIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Ayni engine+symbol+timeframe icin sadece bir acik pozisyon olabilir.
            // Bu filtered unique index worker restart sonrasi olusabilecek duplicate'lere karsi
            // DB seviyesinde son savunma hattini olusturur.
            migrationBuilder.Sql(@"
                CREATE UNIQUE INDEX [UIX_Trades_Open_Engine_Symbol_TimeFrame]
                ON [Trades] ([Engine], [Symbol], [TimeFrame])
                WHERE [Status] = 'Open'
            ");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex("UIX_Trades_Open_Engine_Symbol_TimeFrame", "Trades");
        }
    }
}
