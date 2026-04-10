# Loop 23 - Rapor

## Loop22 -> Loop23 Gecis

### Loop22 Sonuclari
- 297 kapali trade, %52.5 win rate, +$5.48 PnL
- Loop21'den dusuk (%56.6 vs %52.5) — feature agreement son 3 saatte etkisini kaybetti
- UP/DOWN asimetri TERS DONDU: Loop21'de DOWN dominant, Loop22'de UP daha iyi
- XRP yildiz (+$16.15), BTC Up hala sorun (%41, -$5.47)
- Feature agreement ilk saatte %60.9 verdi, sonra geriledi

### Arastirma Kaynaklari
- arXiv 2507.22712 — "Order Book Filtration and Directional Signal Extraction at High Frequency"
  - LOB verisi gurultulu, filtreleme kritik
  - Top 1-2 level gurultulu, deeper levels daha stabil
- arXiv 2506.05764 — LOB feature muhendisligi model karmasikligindan onemli (tekrar dogrulandi)
- ScienceDirect 2024 — "Short-term predictability of returns in order book markets"
  - OBI signal half-life: 5-30 saniye, 1 dakikaya kadar
- Kaiko Research — "A Cheatsheet for Bid Ask Spreads"
  - Dar spread = likit, guvenilir sinyal
  - Genis spread = belirsizlik, gurultu
- Binance Academy — Spread and slippage: spread piyasa stresinde genisler
- Medium — "Order Book Imbalances Predict Price Moves": persistence key metric
- TradingKey — Polymarket Q1 2026 $3.2B volume, 5x YoY artis

### Arastirma Bulgulari

1. **Spread as confidence signal**: Dar spread = piyasa yapicilari guvenli, sinyal guvenilir. Genis spread = belirsizlik, sinyal gurultu olabilir. Henuz kullanilmiyordu.

2. **Feature agreement too aggressive**: 0.70x disagreement penalty cok sert — bazi iyi sinyalleri de oldurdu. Yumusatilmali.

3. **OBI scaling 1.5x too low**: Orijinal 2.0x'den 1.5x'e dusunce OBI'nin etkisi azaldi. 1.8x ara deger daha uygun.

4. **UP/DOWN asimetri piyasa kosuluna bagli**: Loop21'de DOWN dominant, Loop22'de UP — algoritmik degil, piyasa kaynaklı. Yon bagimsiz kalma karari dogru.

### Kod Degisiklikleri

**1. IFuturesDataProvider.cs**
- EKLENDI: `GetNormalizedSpread(symbol)` — current spread / rolling average spread

**2. BinanceFuturesDataProvider.cs**
- Yeni field'lar: `_normalizedSpread`, `_spreadHistory` (60-sample rolling, 30sn)
- Order book callback'inde spread hesabi: (bestAsk - bestBid) / midPrice
- `GetNormalizedSpread`: current/average ratio dondurur (< 1.0 = dar, > 1.0 = genis)

**3. MarketDataWorker.cs — TryGenerateAndDispatchSignalAsync**
- Spread confidence modifier: spreadRatio < 0.8 = 1.10x boost, > 2.0 = 0.80x discount
- Feature agreement penalty: 0.70x -> 0.80x (yumusatildi)
- OBI scaling: 1.5x -> 1.8x (restore)
- Log formati L23, Spr:{Sp:F2} eklendi

## Loop23 Baslangic
- **Baslangic:** 10.04.2026 08:25 (TR)
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00

## Loop23 Sonuclari — -$0.80 PnL, %51.5 Win Rate

- **Bitis:** 10.04.2026 12:26 (TR)
- **Sure:** 4 saat
- **Kapali Trade:** 291
- **Acik Trade:** 17
- **Kazanc / Kayip:** 150W / 141L
- **Win Rate:** %51.5
- **Toplam PnL:** -$0.80
- **Ort. PnL/trade:** $0.00
- **En Iyi Trade:** +$2.23
- **En Kotu Trade:** -$1.05
- **Sinyal/saat:** ~73

### Coin Bazinda Sonuclar

| Coin | Down Cnt | Down W/L | Down PnL | Up Cnt | Up W/L | Up PnL | Toplam PnL |
|------|----------|----------|----------|--------|--------|--------|------------|
| ETH | 20 | 12/8 (%60) | +$4.64 | 23 | 13/10 (%57) | +$1.07 | +$5.71 |
| BNB | 19 | 12/7 (%63) | +$4.07 | 21 | 12/9 (%57) | +$2.20 | +$6.27 |
| HYPE | 21 | 11/10 (%52) | +$1.50 | 17 | 9/8 (%53) | +$0.33 | +$1.83 |
| DOGE | 18 | 8/10 (%44) | -$2.97 | 24 | 13/11 (%54) | +$1.64 | -$1.33 |
| XRP | 20 | 11/9 (%55) | +$0.93 | 22 | 11/11 (%50) | -$1.38 | -$0.45 |
| BTC | 22 | 12/10 (%55) | +$1.71 | 21 | 8/13 (%38) | -$7.13 | -$5.42 |
| SOL | 20 | 9/11 (%45) | -$1.43 | 23 | 9/14 (%39) | -$5.99 | -$7.42 |

### Yon Analizi

| Yon | Trade | Win | Loss | WR% | PnL |
|-----|-------|-----|------|-----|-----|
| DOWN | 140 | 75 | 65 | %53.6 | +$8.45 |
| UP | 151 | 75 | 76 | %49.7 | -$8.26 |

### Onemli Gozlemler

1. **Ilk ZARARI loop (Loop20 sonrasi)**: -$0.80 PnL, breakeven'in altina dustuk
2. **BTC Up kronik**: %38 WR, -$7.13 — 3 loop'tur en buyuk kayip kaynagi
3. **SOL ciddi sorun**: Her iki yonde zarar, -$7.42 toplam
4. **ETH + BNB tutarli**: Ikisi de her iki yonde karli
5. **Son saat cokus**: +3h'de +$5.54 iken bitiste -$0.80 — son 1 saatte -$6.34 kayip
6. **Spread modifier yetersiz**: Loop22'den cok farkli sonuc vermedi
7. **UP sinyalleri hala sorun**: %49.7 WR — breakeven altinda
