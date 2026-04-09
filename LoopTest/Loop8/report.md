# Loop 8 - Performans Raporu

## Genel Bilgi
- **Baslangic:** 07.04.2026 23:00 (TR)
- **Bitis:** 08.04.2026 03:00 (TR)
- **Sure:** 4 saat
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00
- **Strateji:** Multi-Signal Weighted Score (BTC lead-lag, volume surge, micro-momentum, Z-score reversion)

## Sonuclar — Tum Loop'lar

| Loop | Islem | Win% | PnL | Sinyal/saat | Algoritma |
|------|-------|------|-----|-------------|-----------|
| 1 | 35 | %40 | -$8.11 | 17.5 | Hurst+ZScore (T=300) |
| 2 | 29 | %52 | +$0.75 | 12 | Window Delta (T=240) |
| 3 | 20 | %50 | -$1.80 | 5 | Delta+Trend+Accel (T=240) |
| 4 | 12 | %25 | -$6.35 | 3 | Loop3+PriceFilter (T=240) |
| 5 | 12 | %36 | -$2.83 | 3 | T=0 entry, delta filtreler |
| 6 | 135 | %52 | -$0.25 | 60 | T=0, filtreler kaldirildi |
| 7 | 13 | %15 | -$9.49 | 30 | Loop6+MaxExposure fix |
| **8** | **128** | **%53.1** | **+$2.82** | **34** | **Multi-Signal Score** |

## Loop8 Detay

| Metrik | Deger |
|--------|-------|
| Toplam islem | 128 |
| Kazanc | 68 |
| Kayip | 60 |
| **Win Rate** | **%53.1** |
| **PnL** | **+$2.82** |
| Bakiye | $21.99 (+$7 acik pozisyon) |
| Rejection | 0 |
| Sinyal | 135 (~34/saat) |

## Yon Analizi — EN ONEMLI BULGU

| Yon | Trade | W/L | Win% | PnL |
|-----|-------|-----|------|-----|
| **Up** | 77 | **48/29** | **%62.3** | **+$18.18** |
| **Down** | 51 | 20/31 | %39.2 | -$15.35 |

**Up sinyalleri mukemmel calisiyor (%62.3, +$18.18).**
**Down sinyalleri sistemi batiriyor (%39.2, -$15.35).**

BTC lead-lag sinyali Up tahminlerini dramatik iyilestirdi.
Ancak Down yonunde BTC lead-lag ters calisiyor.

## Asset Bazli (en iyiden en kotuye)

| Asset+Yon | Trade | W/L | Win% | PnL |
|-----------|-------|-----|------|-----|
| BTCUSDT Up | 11 | 9/2 | **%81.8** | **+$6.71** |
| ETHUSDT Up | 9 | 6/3 | %66.7 | +$3.67 |
| SOLUSDT Up | 13 | 8/5 | %61.5 | +$2.94 |
| DOGEUSDT Up | 10 | 6/4 | %60 | +$1.69 |
| BNBUSDT Up | 12 | 7/5 | %58.3 | +$1.25 |
| HYPEUSDT Up | 8 | 4/4 | %50 | +$0.96 |
| XRPUSDT Up | 14 | 8/6 | %57.1 | +$0.95 |
| DOGEUSDT Down | 12 | 7/5 | %58.3 | +$0.29 |
| XRPUSDT Down | 8 | 4/4 | %50 | -$0.37 |
| SOLUSDT Down | 1 | 0/1 | %0 | -$1.04 |
| HYPEUSDT Down | 5 | 2/3 | %40 | -$1.71 |
| BTCUSDT Down | 8 | 3/5 | %37.5 | -$2.63 |
| ETHUSDT Down | 8 | 2/6 | %25 | -$4.39 |
| BNBUSDT Down | 9 | 2/7 | %22.2 | -$5.51 |

## Saatlik Trend
- Ilk 1 saat: %61 win rate (iyi)
- Son 2 saat: %45-50 (Down sinyalleri bozdu)

## Onemli Not
Up sinyalleri tek basina 77 trade ile %62.3 win rate ve +$18.18 kar uretirken,
Down sinyalleri -$15.35 zarar uretip sistemi neredeyse sifira cekti.
BTC Up %81.8 win rate ile yildiz performans gosterdi.
