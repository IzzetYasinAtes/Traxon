# Loop 27 - Rapor

## Loop26 -> Loop27 Gecis

### Loop26 Sonuclari (erken durduruldu)
- 74 kapali trade, %39.2 WR, -$21.77 PnL — BASARISIZ
- EntryPrice >= 0.47 filtresi ise yaramadi, geri alindi

### Denenen ve Basarisiz Olan Yaklasimlar
- Loop22: Feature agreement modifier — ilk saat iyi, sonra etkisiz
- Loop23: Spread confidence — fark yaratmadi
- Loop24: Confidence 3x/0.75 — ters calisti (guclu sinyal daha kotu)
- Loop26: EntryPrice >= 0.47 filtresi — felaket (%39 WR)

### Basarili Olan
- Loop21: OBI Momentum + OI Change — %56.6 WR, +$22.49
- Loop25 ilk 2h: Sqrt scaling + fixed confidence — %58.2 WR, +$13.20

### Arastirma Bulgulari
- Medium 2026 — "Unlocking Edges in Polymarket's 5-Minute Crypto Markets"
  - Basarili botlar TAHMIN yapmiyor, Binance fiyat hareketine REAKSIYON gosteriyor
  - Latency arbitrage: Polymarket fiyatlari Binance'den 2-3sn geride
  - %85+ WR elde eden botlar hiz avantaji kullaniyor
- MDPI 2025 — "Confidence-Threshold Framework for Cryptocurrency Price Direction"
  - %82.68 accuracy, ama sadece %12 market coverage (cok secici)
  - Tahmin ve execution'i ayirma kritik
- arXiv 2506.05764 — Feature engineering > model complexity (tekrar dogrulandi)

### Neden OBI Dominant Agirlik?
En taze veri kaynagi en cok agirlik almali:
- OBI: 500ms guncelleme = EN TAZE → %55 agirlik
- OBI Momentum: OBI trendi → %20 agirlik
- OFI: 1dk candle = GECIKMELI → %20 agirlik
- VWAP: 60dk = COK GECIKMELI → %5 agirlik

### Kod Degisiklikleri
- Agirliklar: OFI %35->%20, VWAP %10->%5, OBI %40->%55, OBIMom %15->%20
- Loop25 sqrt scaling + fixed confidence korundu
- EntryPrice filtresi geri alindi

### GERI ALMA KURALI
Bu degisiklik Loop27'de ise yaramaz ise (WR < %53 veya PnL negatif), Loop28'de geri alinacak.

## Loop27 Baslangic
- **Baslangic:** 11.04.2026 13:33 (TR)
- **Bitis (planlanan):** 11.04.2026 21:33 (TR)
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00
- **Loop Suresi:** 8 saat

## Loop27 Sonuclari — BASARISIZ: -$25.29 | %48.7 WR | 372 trade / 8 saat

- **Bitis:** 11.04.2026 21:34 (TR)
- **Sure:** 8 saat
- **Kapali Trade:** 372
- **Kazanc / Kayip:** 181W / 191L
- **Win Rate:** %48.7
- **Toplam PnL:** -$25.29
- **HYPE + BNB:** -$29.36 (tum zararin kaynagi)
- **SOL + XRP:** +$18.41 (karli coinler)
- **OBI dominant agirlik BASARISIZ** — geri alinacak, koklu degisiklik gerek
