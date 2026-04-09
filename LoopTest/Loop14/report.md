# Loop 14 - Rapor

## Loop13 -> Loop14 Gecis Arastirmasi

### Arastirma Kaynaklari
- Tartakovsky et al. (2020) - "Autocorrelation of returns in major cryptocurrency markets" (arxiv.org/abs/2003.13517)
- Dean Markwick - "Order Flow Imbalance as a High Frequency Trading Signal" (dm13450.github.io)
- RSI Trading Strategy backtest (quantifiedstrategies.com) — RSI(7) 1dk crypto icin optimal
- Bollinger Band Squeeze Breakout Strategy (mindmathmoney.com)
- Multi-Indicator Convergence and Reversal Strategy (medium.com/@FMZQuant)
- MACD and Bollinger Bands Strategy — %78 win rate claim (quantifiedstrategies.com)
- Bitcoin Lead-Lag tick-by-tick measurement (businessperspectives.org)
- Asset volatility forecasting: optimal EWMA decay parameter (arxiv.org/abs/2105.14382)
- Luxalgo — Mean Reversion Trading with precision, Volume Analysis Techniques

### Arastirma Bulgulari
1. **Convergence scoring**: 3+ bagimsiz mean-reversion gostergesi ayni anda sinyal verdiginde win rate %67'den %78'e cikiyor. En yuksek etkili iyilestirme.
2. **RSI(7) surekli carpan**: RSI asiri bolgede (oversold <25, overbought >75) iken sinyal dogrulama +%2-4 iyilestirme. RSI(14) yerine RSI(7) daha hizli.
3. **Adaptive threshold**: Farkli coinlerin farkli volatilitesi var. ATR bazli esik: volatil coinler (SOL) daha yuksek threshold, sakin coinler (BTC) daha dusuk. +%2-3.
4. **Volume confirmation**: Yuksek hacim + mean reversion = exhaustion (teyit). Dusuk hacim = zayif setup. +%2-3.
5. **EWMA autocorrelation**: Lambda=0.90 ile son pencerelere daha fazla agirlik. 100 yerine 50 pencere. Regime degisikliklerine ~30dk'da adapte olur.
6. **Per-asset BTC lag**: ETH icin BTC lead sinyali gereksiz (esanli hareket). DOGE icin 2dk lag, digerleri 1dk.
7. **Doji filtresi**: Son mumun body/range < %20 ise kararsizlik, sinyal 0.5x indirim.
8. **Strong candle confirmation**: Body/range > %60 ve yon teyit ediyorsa 1.15x boost, celisiyorsa 0.75x.

### Kod Degisiklikleri

**Dosya: `src/CryptoTrader/Traxon.CryptoTrader.Application/Workers/MarketDataWorker.cs`**
`TryGenerateAndDispatchSignalAsync` metodu tamamen yeniden yazildi:

1. **EWMA Autocorrelation** (YENI):
   - Lambda=0.90 ile agirlikli korelasyon hesabi
   - 50 pencere (onceki 100'du)
   - SOL/ETH icin minimum |autocorr| > 0.08 (digerleri 0.03)

2. **Volume Confirmation** (YENI):
   - volRatio = son 5 bar ort hacim / son 20 bar ort hacim
   - Mean reversion rejiminde: volRatio > 2.0 ise 1.3x boost, < 0.8 ise 0.6x discount

3. **Time-Weighted OFI** (GUNCELLEME):
   - Son bar %67, onceki bar %33 agirlik (onceki esit agirlikti)
   - Baseline: 5 bar, exponential decay (0.8^i)

4. **BTC Lead Per-Asset Lag** (GUNCELLEME):
   - ETH icin BTC lead devre disi (scoreBtcLead = 0)
   - DOGE: 2dk lag, digerleri: 1dk lag

5. **RSI Surekli Carpan** (YENI):
   - UP sinyal + RSI < 25 (oversold) → 1.25x
   - DOWN sinyal + RSI > 75 (overbought) → 1.25x
   - Notr RSI (35-65) → 0.85x

6. **Doji Filtresi** (YENI):
   - body/range < 0.20 → compositeScore *= 0.5
   - body/range > 0.60 ve yon teyit → 1.15x, celiski → 0.75x

7. **Convergence Scoring** (YENI):
   - 4 bagimsiz sinyal sayilir: autocorr mean-revert, RSI extreme, BB extreme, VWAP extreme
   - 0 teyit → 0.60x, 1 → 0.85x, 2 → 1.0x, 3 → 1.30x, 4 → 1.50x

8. **Adaptive Threshold** (YENI):
   - threshold = clamp(0.08 + ATR% * 30, 0.08, 0.25)
   - Ornek: BTC ATR%=0.001 → 0.11, SOL ATR%=0.003 → 0.17

## Loop14 Sonuclari

| Metrik | Deger |
|--------|-------|
| Kapali Islem | 91 |
| Win/Loss | 43W / 48L |
| Win Rate | **%47.3** |
| Toplam PnL | **-$7.79** |
| Baslangic Bakiye | $30.00 |
| Son Bakiye | $18.21 |

### Coin Bazinda
| Coin | Trade | Win% | PnL |
|------|-------|------|-----|
| BNB | 6 | %66.7 | +$1.52 |
| SOL | 10 | %60.0 | +$1.30 |
| DOGE | 5 | %60.0 | +$0.28 |
| HYPE | 10 | %50.0 | +$0.72 |
| XRP | 10 | %50.0 | -$0.21 |
| ETH | 25 | %44.0 | -$3.54 |
| BTC | 25 | **%36.0** | **-$7.86** |

### Analiz
- BTC %36 ile EN KOTU performans — Loop13'te %52 idi. Convergence + RSI multiplier BTC'de ters etkili
- SOL %60'a cikti (Loop13'te %35 idi) — adaptive threshold calisti
- Sinyal hacmi 91 trade / ~8 saat — saatte ~11 sinyal, Loop13'ten (40/saat) dusuk
- Filtreler kaliteyi artirmadi, sadece sinyal sayisini azaltti
- Convergence scoring cok agresif: 0 teyit = 0.6x carpani cok sinirleri kirityor
