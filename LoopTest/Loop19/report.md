# Loop 19 - Rapor

## Loop18 -> Loop19 Gecis

### Loop18 Sonuclari
- 130 kapali trade, %45.4 win rate, -$15.83 PnL
- PnL sync calisiyor (DB ve snapshot uyusuyor)
- AMA hayalet pozisyon bug'i devam etti — DB'de 0 acik, Portfolio'da 14 acik
- Bakiye $14.17 olmasi gerekirken $0.17 gosterdi, trade yapamaz hale geldi

### Arastirma Kaynaklari
- arXiv 2602.00776 - "Explainable Patterns in Cryptocurrency Microstructure" (Binance Futures CatBoost)
- Wen et al. 2022 - "Intraday Return Predictability in Cryptocurrency Markets"
- Silantyev 2019 - "Order Flow Analysis of Cryptocurrency Markets" (Springer)
- Polymarket Fee Documentation (docs.polymarket.com)
- QuantJourney - "Understanding the Polymarket Fee Curve"

### Arastirma Bulgulari

1. **OFI lookback kisa olmali**: Mikroyapi arastirmasi 1-saniye frekansi oneriyor.
   1dk bar kisitimizla en kisa = 1 bar recent vs 3 bar baseline (onceki 2v5'ten daha taze sinyal).

2. **Volatilite gate**: Parkinson volatilite (son 3 bar / son 10 bar) orani:
   - >1.5 = conviction move, sinyal guclendir (1.2x)
   - <0.7 = flat market, noise, sinyal zayiflat (0.5x)
   - Mikroyapi paperinda #4 en onemli feature

3. **UP bias kaldirildi**: Her coinde esit calisan algoritma icin sabit bias yanlis.
   Bazi coinler bazi zaman dilimlerinde down agirlikli.

4. **Fee analizi**: Taker $0.50'de breakeven %51.8.
   Maker $0.47'de breakeven %47. Gelecekte maker order'a gecis buyuk avantaj.

5. **CVD eklenMEDI**: OFI delta ile matematiksel olarak korelasyonlu, yeni bilgi eklemiyor.

### Kod Degisiklikleri

**1. Portfolio DB-Driven Sync (Hayalet Pozisyon Fix)**

`Portfolio.cs`:
- `SyncPnL` -> `SyncFromDb(realizedPnL, winCount, lossCount, openExposure)` olarak guncellendi
- Balance = InitialBalance + realizedPnL - openExposure (DB'den gelen exposure, RAM'den degil)
- Hayalet pozisyon sorunu ortadan kalkti

`PaperPolymarketEngine.cs`:
- `SyncPortfolioFromDbAsync` guncellendi: artik DB'deki acik trade'lerin exposure'ini da sorguluyor
- `OpenPositionAsync` her cagrildiginda once DB'den sync yapiyor
- Ayni fix `PolymarketEngine.cs`'e de uygulandi

**2. Sinyal Algoritmasi Guncellemesi**

`MarketDataWorker.cs` — `TryGenerateAndDispatchSignalAsync`:

DEGISEN:
- OFI Delta: 2-bar vs 5-bar -> **1-bar vs 3-bar** (daha taze sinyal, scale 12->15)
- UP Bias (+0.05) **KALDIRILDI** (yon bagimsizlik icin)
- Agirliklar: OFI %60->%65, VWAP %30->%35

EKLENEN:
- **Volatilite gate**: Parkinson vol son 3 bar / son 10 bar
  - volExpansion > 1.5 -> compositeScore *= 1.2 (conviction)
  - volExpansion < 0.7 -> compositeScore *= 0.5 (flat, skip)

- Threshold 0.06 -> 0.05 (biraz daha fazla sinyal)

KALDIRILMAYAN (ayni kalan):
- VWAP Z-Score (60-bar)
- Volume filter (dead market skip)
- T=0+2s entry delay

## Loop19 Sonuclari
_(Loop devam ediyor, sonuclar loop bitiminde eklenecek)_
