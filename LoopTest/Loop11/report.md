# Loop 11 - Performans Raporu (BASARISIZ)

## Genel Bilgi
- **Baslangic:** 08.04.2026 09:30 (TR)
- **Bitis:** 08.04.2026 ~11:15 (TR) — erken sonlandirildi (~2 saat)
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00
- **Son Bakiye:** $4.49
- **Strateji:** Regime-based (ExtremeMeanRevert, Exhaustion, MeanReversion, Momentum) + True Taker Imbalance + BTC Cross-Asset Lead-Lag

## Genel Sonuclar

| Metrik | Deger |
|--------|-------|
| Kapali Islem | 61 |
| Win/Loss | 27W / 34L |
| Win Rate | **%44.3** |
| Toplam PnL | **-$11.05** |
| Ort PnL | -$0.18 |
| En Iyi | +$1.59 |
| En Kotu | -$1.05 |

## Yon Analizi

| Yon | Trade | W/L | Win% | PnL |
|-----|-------|-----|------|-----|
| Down | 37 | 16/21 | %43.2 | -$7.25 |
| Up | 24 | 11/13 | %45.8 | -$3.80 |

Iki yon de benzer performans — yon bagimsizlik saglandi ama iki yon de kotu.

## Coin Bazinda

| Coin | Trade | Win% | PnL | Not |
|------|-------|------|-----|-----|
| BTC | 13 | %61.5 | +$1.18 | En iyi |
| HYPE | 10 | %50.0 | -$1.06 | |
| ETH | 6 | %50.0 | -$0.10 | |
| SOL | 7 | %42.9 | -$1.38 | |
| BNB | 9 | %33.3 | -$3.53 | |
| XRP | 8 | %25.0 | -$3.81 | |
| DOGE | 5 | %0.0 | -$5.19 | En kotu |

## Regime Analizi
**TUM 61 trade "LowVolatility" regime'ine dustu.** ExtremeMeanRevert, Exhaustion, Momentum rejimleri hic tetiklenmedi. Regime detection sistemi tamamen calismiyor.

## Saat Bazinda
| Saat | Trade | Win% |
|------|-------|------|
| 06:xx | 18 | %61.1 |
| 07:xx | 37 | %32.4 |
| 08:xx | 3 | %33.3 |

## Kok Neden Analizi
1. **Regime detection kirik** — Hurst exponent 120-bar penceresi cok uzun, hep LowVolatility veriyor
2. **Z-Score 120-bar penceresi** — 2 saatlik veri 5dk tahmin icin irrelevant, threshold'lara ulasamiyor
3. **TakerRatioCalculator gercek TakerBuyBaseVolume kullanmiyor** — candle direction proxy kullaniyor
4. **Sequential filter (regime -> direction -> confirmation) sinyal olduruyor** — ensemble scoring olmali
5. **Ilk saat (%61) vs sonrasi (%32)** — muhtemelen baslangicta birkac iyi sinyal, sonra hep noise

## Sonuc
Loop11 stratejisi tamamen basarisiz. Regime-based yaklasim lookback pencereleri cok uzun oldugu icin calismiyor. Loop12'de radikal degisiklik gerekli: ensemble weighted scoring, gercek TakerBuyBaseVolume kullanimi, kisa lookback pencereleri.
