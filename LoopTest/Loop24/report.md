# Loop 24 - Rapor — KARLI: +$8.56 | %52.4 WR | 580 trade / 8 saat

## Loop23 -> Loop24 Gecis

### Loop21-23 Sonuclari
- Loop21: 290 trade, %56.6 WR, +$22.49 (EN IYI)
- Loop22: 297 trade, %52.5 WR, +$5.48
- Loop23: 291 trade, %51.5 WR, -$0.80 (ILK ZARAR)
- Kullanici Elle Test: 434 trade / 7 saat, %48.8 WR, -$29.34

### Veri Analizi (434 trade, 7 saat)

**Kok Neden Bulundu: FairValue Saturasyonu**
- AdaptiveSignalGenerator.GenerateFromEnsemble confidence hesabi
- `confidence = Clamp(0.50 + |effectiveDelta| * 8, 0.52, 0.90)`
- |effectiveDelta| >= 0.05 olan HER sinyal -> confidence = 0.90
- FairValue HER ZAMAN 0.90 (UP) veya 0.10 (DOWN)
- Algoritma zayif ve guclu sinyal arasinda SIFIR ayrim yapiyor
- Her sinyal "%90 eminim" diyor — bu yanlis

**Yon Analizi (7 saat):**
- UP: %57.1 WR, +$23.44 (iyi)
- DOWN: %41.5 WR, -$52.78 (felaket)
- Loop21'de TAM TERSI idi — algoritma yonu tutarli tahmin edemiyor

**Saat Bazinda:**
- 09 UTC: %75.0, +$9.98 (harika)
- 10 UTC: %39.4, -$18.36 (felaket)
- 11-13 UTC: %56-57, pozitif (iyi)
- 14 UTC: %28.8, -$34.82 (KATASTROFIK — tek saat)
- BTC Down: %24.3, -$20.44 (37 trade, en buyuk kayip)

### Arastirma Kaynaklari
- arXiv 2506.05764 — "Better Inputs Matter More Than Stacking Another Hidden Layer"
- arXiv 2507.22712 — "Order Book Filtration and Directional Signal Extraction at High Frequency"
- arXiv 2408.03594 — "Forecasting high frequency order flow imbalance using Hawkes processes"
- arXiv 2602.00776 — "Explainable Patterns in Cryptocurrency Microstructure"
- PMC — "Deep limit order book forecasting: a microstructural guide"
- hftbacktest — Market Making with Alpha: OBI rolling window optimization
- Cornell — "Microstructure and Market Dynamics in Crypto Markets"
- EFMA 2025 — "Order Flow and Cryptocurrency Returns"

### Arastirma Bulgulari

1. **FairValue saturasyonu en kritik bug**: Confidence 8x multiplier ile |effectiveDelta| >= 0.05'te 0.90'a yapisiyor. Tum sinyaller ayni confidence ile trade ediliyor — zayif sinyal de guclu sinyal de. Cozum: multiplier 3x, cap 0.75.

2. **OBI 12-sample pencere cok kisa (6sn)**: Gecici order book fluctuation'lari yon sinyalini flip ediyor. Akademik literatur 15-30sn pencere oneriyor. 30 sample = 15sn daha stabil.

3. **OFI 1-bar vs 3-bar cok gurultulu**: Tek bar'in TakerBuyBaseVolume'u rastgele degiskenlik gosteriyor. 2-bar vs 6-bar ortalama gürultuyu azaltir.

4. **OBI Momentum 8x cok agresif**: Kucuk OBI degisimleri bile saturasyona goturuyor. 5x daha olculu.

5. **Kalman/Savitzky-Golay filtreleme**: Akademik calismalar LOB verisine on-isleme yapildiginda basit modellerin bile performansinin arttigini gosteriyor.

### Kod Degisiklikleri

**1. AdaptiveSignalGenerator.cs — GenerateFromEnsemble**
- Confidence: `0.50 + |delta| * 8, [0.52, 0.90]` -> `0.50 + |delta| * 3, [0.52, 0.75]`
- FairValue artik 0.52-0.75 (UP) veya 0.25-0.48 (DOWN) arasinda dagilacak
- Guclu sinyaller yuksek confidence, zayif sinyaller dusuk confidence alacak

**2. BinanceFuturesDataProvider.cs**
- OBI history size: 12 -> 30 sample (6sn -> 15sn)
- Daha stabil order book imbalance sinyali

**3. MarketDataWorker.cs — TryGenerateAndDispatchSignalAsync**
- OFI lookback: 1-bar vs 3-bar -> 2-bar vs 6-bar
- OFI scale: 15x -> 12x (genis pencere icin)
- OBI Momentum scale: 8x -> 5x
- Comment ve log L24 olarak guncellendi

## Loop24 Baslangic
- **Baslangic:** 10.04.2026 23:05 (TR)
- **Bitis (planlanan):** 11.04.2026 07:05 (TR)
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00
- **Loop Suresi:** 8 saat

## Loop24 Sonuclari — $8.56 PnL, %52.4 Win Rate

- **Bitis:** 11.04.2026 07:07 (TR)
- **Sure:** 8 saat
- **Kapali Trade:** 580
- **Acik Trade:** 11
- **Kazanc / Kayip:** 304W / 276L
- **Win Rate:** %52.4
- **Toplam PnL:** +$8.56
- **Ort. PnL/trade:** +$0.01
- **En Iyi Trade:** +$2.28
- **En Kotu Trade:** -$1.05
- **Sinyal/saat:** ~73

### Coin Bazinda Sonuclar

| Coin | Down Cnt | Down W/L (WR%) | Down PnL | Up Cnt | Up W/L (WR%) | Up PnL | Toplam PnL |
|------|----------|----------------|----------|--------|--------------|--------|------------|
| DOGE | 46 | 28/18 (%61) | +$8.96 | 37 | 23/14 (%62) | +$6.96 | +$15.92 |
| SOL | 44 | 27/17 (%61) | +$8.22 | 45 | 26/19 (%58) | +$2.92 | +$11.14 |
| HYPE | 41 | 21/20 (%51) | +$0.64 | 39 | 21/18 (%54) | +$3.78 | +$4.42 |
| BNB | 44 | 24/20 (%55) | +$2.01 | 34 | 14/20 (%41) | -$5.69 | -$3.68 |
| ETH | 43 | 21/22 (%49) | -$2.28 | 44 | 22/22 (%50) | -$2.19 | -$4.47 |
| XRP | 40 | 21/19 (%53) | +$2.40 | 45 | 20/25 (%44) | -$7.27 | -$4.87 |
| BTC | 35 | 17/18 (%49) | -$2.40 | 43 | 19/24 (%44) | -$7.48 | -$9.88 |

### Yon Analizi

| Yon | Trade | Win | Loss | WR% | PnL |
|-----|-------|-----|------|-----|-----|
| DOWN | 293 | 159 | 134 | %54.3 | +$17.55 |
| UP | 287 | 145 | 142 | %50.5 | -$8.97 |

### FairValue Confidence Analizi

| Tip | Trade | WR% | PnL |
|-----|-------|-----|-----|
| Guclu (FV>=0.65 / <=0.35) | 451 | %51.9 | +$0.64 |
| Zayif (FV 0.35-0.65) | 129 | %54.3 | +$7.92 |

**BULGU:** Confidence hesabi TERS calisiyor. "Zayif" sinyaller (%54.3 WR, +$7.92) "guclu" sinyallerden (%51.9 WR, +$0.64) daha iyi. Algoritmanin conviction olcusu yanlis.

### Onemli Gozlemler

1. **KARLI**: +$8.56, %52.4 WR — breakeven uzerinde
2. **8 saat boyunca stabil**: Buyuk zarar yok, son 2 saatte kara gecti
3. **DOGE + SOL yildiz**: +$27.06 toplam, her iki yonde guclu
4. **BTC en kotu**: -$9.88, %44 Up WR
5. **Confidence ters**: Zayif sinyaller guclulerden daha iyi — bu Loop25'te duzeltilmeli
6. **DOWN (%54.3) > UP (%50.5)**: DOWN karli, UP breakeven
