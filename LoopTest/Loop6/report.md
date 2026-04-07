# Loop 6 - Performans Raporu

## Genel Bilgi
- **Baslangic:** 07.04.2026 18:08 (TR)
- **Bitis:** 07.04.2026 22:08 (TR)
- **Sure:** 4 saat
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $20.00
- **Strateji:** T=0+2sn entry, prev window delta, filtreler kaldirildi

## Degisiklikler (Loop5 → Loop6)
1. Gamma API dedup fix — resolved market'i korur
2. Agresif filtreler KALDIRILDI (trend, acceleration, volume, price cap)
3. Delta threshold 0.08% → 0.02%
4. MinEdge 0.03 → 0.01

## Sonuclar

| Metrik | Loop1 | Loop2 | Loop3 | Loop4 | Loop5 | **Loop6** |
|--------|-------|-------|-------|-------|-------|-----------|
| Islem | 35 | 29 | 20 | 12 | 12 | **135** |
| Kazanc | 14 | 15 | 10 | 3 | 4 | **70** |
| Kayip | 21 | 14 | 10 | 9 | 7 | **65** |
| Win% | %40 | %52 | %50 | %25 | %36 | **%51.9** |
| PnL | -$8.11 | +$0.75 | -$1.80 | -$6.35 | -$2.83 | **-$0.25** |
| Sinyal/saat | 17.5 | 12 | 5 | 3 | 3 | **~60** |
| Bakiye | $10.89 | $17.46 | $18.20 | $13.65 | $17.17 | $11.33 |

## ONEMLI NOT
Loop6 ilk 2 saatte **%55 win rate ve +$6.98 kar** ile calisiyordu.
Sinyal hacmi sorunu cozuldu (3/saat → 60/saat).
Resolution bug cozuldu (stuck trade 0).
Ancak 101 sinyal PortfolioInsufficient ile reddedildi — bu daha fazla kar firsatini engelledi.
Karin erimesinin ana sebebi MaxExposure limitinin sistemi kilitlemesi ve
sadece bazi sinyallerin alinabilmesi.

## Yon Analizi (135 trade — guclu orneklem)

| Yon | Trade | W | L | Win% | PnL |
|-----|-------|---|---|------|-----|
| **Down** | 62 | 39 | 23 | **%62.9** | **+$12.96** |
| **Up** | 73 | 31 | 42 | **%42.5** | **-$13.21** |

Down sinyalleri tek basina +$12.96 kar uretirken, Up sinyalleri -$13.21 zarar uretip
sistemi neredeyse sifira cekti.

## Asset Bazli (en iyiden en kotuye)

| Asset+Yon | Trade | W/L | PnL |
|-----------|-------|-----|-----|
| XRPUSDT Down | 12 | 8/4 | +$3.98 |
| DOGEUSDT Down | 6 | 5/1 | +$3.68 |
| BNBUSDT Down | 9 | 6/3 | +$2.20 |
| ETHUSDT Down | 8 | 5/3 | +$1.73 |
| BTCUSDT Down | 12 | 7/5 | +$1.23 |
| SOLUSDT Up | 9 | 5/4 | +$0.63 |
| SOLUSDT Down | 9 | 5/4 | +$0.51 |
| HYPEUSDT Up | 10 | 5/5 | +$0.09 |
| ETHUSDT Up | 12 | 6/6 | -$0.16 |
| HYPEUSDT Down | 6 | 3/3 | -$0.37 |
| XRPUSDT Up | 8 | 3/5 | -$1.68 |
| BTCUSDT Up | 10 | 4/6 | -$2.45 |
| BNBUSDT Up | 12 | 4/8 | -$4.19 |
| DOGEUSDT Up | 12 | 4/8 | -$5.45 |

## Red Edilen Sinyaller
- **PortfolioInsufficient: 101** — Ana sorun. MaxExposure = Balance * %90 limiti

## Cozulen Sorunlar
1. Sinyal hacmi: 3/saat → 60/saat (COZULDU)
2. Stuck trade: 0 (dedup fix CALISIYOR)
3. T=0+2sn zamanlama (CALISIYOR)

## Cozulmesi Gereken Sorunlar (Loop7)
1. **MaxExposure limiti** — %90 limit sistemi kilitliyor, 101 sinyal reddedildi
2. **Up sinyalleri zayif** — %42.5 win rate, kar yiyor
