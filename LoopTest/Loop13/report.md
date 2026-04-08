# Loop 13 - Performans Raporu

## Genel Bilgi
- **Baslangic:** 08.04.2026 14:20 (TR)
- **Bitis:** 08.04.2026 ~18:20 (TR)
- **Sure:** 4 saat
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00
- **Son Bakiye:** $19.16
- **Strateji:** Autocorrelation + OFI Delta + Multi-TF Alignment Filter

## Sonuclar

| Metrik | Deger |
|--------|-------|
| Kapali Islem | 161 |
| Win/Loss | 80W / 81L |
| Win Rate | **%49.7** |
| Toplam PnL | **-$5.10** |
| Ort PnL | -$0.03 |

## Coin Bazinda
| Coin | Trade | Win% | PnL |
|------|-------|------|-----|
| XRP | 24 | %58.3 | +$2.65 |
| DOGE | 23 | %56.5 | +$2.46 |
| BTC | 42 | %52.4 | +$1.88 |
| HYPE | 16 | %50.0 | -$0.24 |
| BNB | 17 | %47.1 | -$0.69 |
| ETH | 19 | %42.1 | -$3.35 |
| SOL | 20 | %35.0 | -$7.80 |

## Kok Neden Analizi
1. %49.7 — Loop12 (%37.9) dan ciddi iyilesme, ama hala breakeven altinda
2. XRP, DOGE, BTC karli — autocorrelation bu coinlerde calisiyor
3. SOL %35 ile en kotu — autocorrelation yanlis yon tahmini yapiyor
4. ETH %42 — momentum/reversion regime yanlis hesaplaniyor
5. Multi-TF filtre islem sayisini azaltmadi (161 trade) — threshold yeterince siki degil
6. Otokorelasyon yaklasimiyla %50 civari — daha iyi feature'lar veya daha siki filtreleme lazim
