# Loop 18 - Rapor

## Loop17 -> Loop18 Gecis

### Loop17 Sonuclari
- 24 kapali trade, %42 win rate (10W/14L)
- DB PnL: -$4.68, Portfolio snapshot PnL: -$2.78 — $1.90 DRIFT devam etti
- Onceki fix (restart'ta DB'den PnL hesaplama) yetersiz — runtime drift devam ediyor

### Bug Analizi (DETAYLI)

**Problem:** Portfolio.TotalPnL runtime'da DB'deki gercek PnL'den sapiyor.
Restart fix (Loop17) sadece baslangicta duzeltiyor, calisirken yine drift oluyor.

**Kok Neden:**
1. Portfolio.ClosePosition() ve LogTradeClosedAsync() arasinda atomik islem yok
2. DB yazimi basarisiz olursa Portfolio zaten guncellenmis oluyor
3. Iki farkli kapatma yolu (ClosePositionAsync vs CheckPositionsAsync) farkli PnL hesabi yapabilir
4. Her missed sync hatasi kalici ve birikmeli

**Cozum: Periyodik DB Sync**
Her candle close'da (her dakika) Portfolio'yu DB'den senkronize et:
- DB'den gercek SUM(PnL), win count, loss count sorgula
- Portfolio.SyncPnL() ile uzerine yaz
- Balance = InitialBalance + dbTotalPnL - TotalExposure (acik pozisyonlar)
- Bu sayede drift max 1 dakika icinde duzeltilir

### Kod Degisiklikleri

**1. `src/CryptoTrader/Traxon.CryptoTrader.Domain/Trading/Portfolio.cs`**
- EKLENDI: `SyncPnL(decimal dbTotalPnL, int dbWinCount, int dbLossCount)` metodu
- TotalPnL, WinCount, LossCount DB'den gelen degerlerle uzerine yazilir
- Balance = InitialBalance + dbTotalPnL - TotalExposure olarak yeniden hesaplanir

**2. `src/CryptoTrader/Traxon.CryptoTrader.Application/Abstractions/ITradeLogger.cs`**
- EKLENDI: `GetClosedTradeCountsAsync(string engineName, CancellationToken ct)`
- Win ve loss sayilarini DB'den sorgular

**3. `src/CryptoTrader/Traxon.CryptoTrader.Infrastructure/Persistence/SqlTradeLogger.cs`**
- EKLENDI: `GetClosedTradeCountsAsync` implementasyonu
- `CountAsync(t => t.Outcome == "Win")` ve `CountAsync(t => t.Outcome == "Loss")`

**4. `src/CryptoTrader/Traxon.CryptoTrader.Infrastructure/Engines/PaperPolymarketEngine.cs`**
- EKLENDI: `SyncPortfolioFromDbAsync()` private metodu
- `CheckPositionsAsync` sonunda her cagirildiginda calisir (her candle close = her dakika)
- DB'den realPnL + win/loss count ceker, Portfolio.SyncPnL() cagirir

**5. `src/CryptoTrader/Traxon.CryptoTrader.Polymarket/Engines/PolymarketEngine.cs`**
- Ayni sync mekanizmasi live engine'e de eklendi

**6. `src/CryptoTrader/Traxon.CryptoTrader.Dashboard/Services/NullTradeLogger.cs`**
- Interface uyumlulugu icin no-op (return (0,0))

**Sinyal uretme mantigi DEGISTIRILMEDI** — Loop15 algoritmasi devam ediyor.

## Loop18 Sonuclari
_(Loop devam ediyor, sonuclar loop bitiminde eklenecek)_
