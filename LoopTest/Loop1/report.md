# Loop 1 - Performans Raporu

## Genel Bilgi
- **Baslangic:** 06.04.2026 18:40 (TR)
- **Bitis:** 06.04.2026 20:45 (TR)
- **Sure:** ~2 saat
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $20.00

## Sonuclar

| Metrik | Deger |
|--------|-------|
| Toplam Islem | 35 |
| Kazanc | 14 |
| Kayip | 21 |
| Basari Orani | %40.0 |
| Toplam PnL | -$8.11 |
| Ortalama PnL/Islem | -$0.23 |
| Son Bakiye | $10.89 |
| Kar/Zarar | -%40.5 |

## Asset Bazli Performans

| Asset | Yon | Islem | Kazanc | PnL |
|-------|-----|-------|--------|-----|
| BNBUSDT | Down | 3 | 1 | -$1.11 |
| BNBUSDT | Up | 4 | 2 | -$0.16 |
| BTCUSDT | Up | 2 | 1 | +$0.45 |
| DOGEUSDT | Down | 1 | 0 | -$1.04 |
| DOGEUSDT | Up | 4 | 2 | -$0.27 |
| ETHUSDT | Down | 1 | 1 | +$0.98 |
| ETHUSDT | Up | 1 | 0 | -$1.04 |
| HYPEUSDT | Down | 3 | 1 | -$1.11 |
| HYPEUSDT | Up | 5 | 2 | -$1.20 |
| SOLUSDT | Down | 1 | 0 | -$1.04 |
| SOLUSDT | Up | 2 | 1 | +$0.05 |
| XRPUSDT | Down | 3 | 1 | -$1.13 |
| XRPUSDT | Up | 5 | 2 | -$1.50 |

## Red Edilen Sinyaller

| Sebep | Adet |
|-------|------|
| DuplicatePosition | 7 |
| PortfolioInsufficient | 1 |

## Tespit Edilen Sorunlar

1. **DuplicatePosition Bug:** Onceki pencerenin trade'i Gamma API resolution beklerken yeni pencereyi blokluyordu. 5dk zamanlayici yerine Polymarket window boundary (epoch % 300) tabanli kontrole gecildi.
2. **Dusuk Basari Orani:** %40 — hedef %75'in cok altinda
3. **Up Sinyalleri Zayif:** Up yonunde 19 islem, 8 kazanc (%42). Down yonunde 12 islem, 4 kazanc (%33).
4. **Her Iki Yon de Zayif:** Ne mean reversion ne momentum stratejisi tutarli sonuc uretemiyor.

## Analiz

- Hurst/Z-Score/TakerRatio tabanli rejim algilama cok fazla sinyal uretiyor (35 islem / 2 saat = ~17.5/saat)
- Sinyal kalitesi dusuk — neredeyse rastgele (%40 vs %50 coin flip)
- FairValue hesaplama ve edge belirleme gercek market fiyatlarindan kopuk
- Polymarket 5dk binary market'lerde basarili olmak icin daha guclu ongorucu sinyaller gerekiyor
