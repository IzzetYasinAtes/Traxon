# Loop 9 - Performans Raporu

## Genel Bilgi
- **Baslangic:** 08.04.2026 03:10 (TR)
- **Bitis:** 08.04.2026 07:10 (TR)
- **Sure:** 4 saat
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00
- **Strateji:** Asimetrik — BTC lead-lag sadece UP, mean reversion DOWN

## Sonuclar — BASARISIZ

| Metrik | Loop8 | **Loop9** |
|--------|-------|-----------|
| Islem | 128 | 87 |
| Win% | %53.1 | **%46.0** |
| PnL | +$2.82 | **-$9.48** |

## Yon Analizi

| Yon | Trade | W/L | Win% | Loop8 Win% |
|-----|-------|-----|------|------------|
| Down | 43 | 21/22 | %48.8 | %39.2 |
| Up | 44 | 19/25 | %43.2 | %62.3 |

DOWN %39→%49 iyilesti ama UP %62→%43 coktu.
Mean reversion sinyali (Signal 6) UP skoruna gurultu ekledi.

## Kok Neden Analizi
1. Signal 6 (mean reversion) hem UP hem DOWN'a etki ediyor — UP'i bozdu
2. Z-Score threshold 2.5→2.0 cok erken override yapiyor
3. BTC lead-lag'in DOWN'dan cikarilmasi DOWN'i sadece +10 puan iyilestirdi ama UP'i -19 puan dusurdu
4. Net etki: -7 puan genel win rate, -$12 PnL farki

## Sonuc
Loop9 degisiklikleri geri alinmali. Loop8 algoritmasi daha iyi.
