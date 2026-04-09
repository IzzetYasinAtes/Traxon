# Loop 17 - Rapor

## Loop16 -> Loop17 Gecis

### Loop16 Sonuclari
- 91 kapali trade, %52.7 win rate, DB PnL +$1.07
- Dashboard -$6.43 gosteriyordu — Portfolio TotalPnL drift bug'i

### Bug Analizi (DETAYLI)

**Problem:** Portfolio'nun in-memory TotalPnL'i DB'deki gercek PnL'den sapiyor.
- DB Trades SUM(PnL) WHERE Closed = +$1.07 (DOGRU)
- Portfolio snapshot TotalPnL = -$6.43 (YANLIS)
- $7.50 fark — drift zamanla birikti

**Kok Neden:** 
- Portfolio.ClosePosition() PnL'i in-memory ekliyor
- Her 30sn snapshot'a kaydediliyor
- Restart'ta snapshot.TotalPnL'den restore ediliyor
- Eger herhangi bir close event missed olursa (exception, race condition) PnL kayiyor
- Sonraki restart yanlis degeri tekrar yukluyor — hata kalici

**Cozum:**
- Restart'ta snapshot.TotalPnL'e GUVENMEK yerine DB'den gercek PnL hesapla
- `SELECT SUM(PnL) FROM Trades WHERE Engine='PaperPoly' AND Status='Closed'`

### Kod Degisiklikleri

**1. `src/CryptoTrader/Traxon.CryptoTrader.Application/Abstractions/ITradeLogger.cs`**
- EKLENDI: `Task<decimal> GetRealizedPnLAsync(string engineName, CancellationToken ct)`
- DB'den gercek kapali trade PnL toplamini sorgular

**2. `src/CryptoTrader/Traxon.CryptoTrader.Infrastructure/Persistence/SqlTradeLogger.cs`**
- EKLENDI: `GetRealizedPnLAsync` implementasyonu
- `db.Trades.Where(Closed).SumAsync(t => t.PnL)` sorgusu

**3. `src/CryptoTrader/Traxon.CryptoTrader.Infrastructure/Engines/PaperPolymarketEngine.cs`**
- `EnsureInitializedAsync()` guncellendi
- ESKI: `var unencumberedBalance = InitialBalance + snapshot.TotalPnL;`
- YENI: `var realPnL = await _tradeLogger.GetRealizedPnLAsync(EngineName, ct);`
- `var unencumberedBalance = InitialBalance + realPnL;`
- Her iki degeri logluyor: DB PnL vs snapshot PnL (debug icin)

**4. `src/CryptoTrader/Traxon.CryptoTrader.Polymarket/Engines/PolymarketEngine.cs`**
- Ayni fix live engine'e de uygulandi

**5. `src/CryptoTrader/Traxon.CryptoTrader.Dashboard/Services/NullTradeLogger.cs`**
- Interface uyumlulugu icin no-op implementasyon (return 0m)

**Sinyal uretme mantigi DEGISTIRILMEDI** — Loop15 algoritmasi devam ediyor.

## Loop17 Sonuclari
_(Loop devam ediyor, sonuclar loop bitiminde eklenecek)_
