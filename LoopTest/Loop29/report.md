# Loop 29 - Rapor

## KOKLU DEGISIKLIK — Pure Price-Based, NO Microstructure

### Loop28 Basarisiz — Neden Microstructure Kaldirildi?
Loop28: -$28.65, %49.2 WR. Momentum eklendi ama OBI/OFI hala vardi. Basarisiz.

### 28 Loop'tan Sonra Ogrenilen
- Microstructure (OBI/OFI) 5dk icin calismiyor — bilimsel olarak 5-30sn icin gecerli
- Tum loop'larda ayni pattern: ilk saat iyi, sonra cokus
- Saat bazinda analiz: 19 UTC %60.6, 01 UTC %40.3 — saat hassasiyeti var ama filtre yasak
- SOL Up kronik kotu (%36), BTC Up kronik (%42)

### YENI YAKLASIM: Saf Fiyat-Bazli Multi-Timeframe
- **Mom5**: Son 5 bar fiyat degisimi (5dk trend)
- **Mom15**: Son 15 bar fiyat degisimi (15dk trend)
- **Mom60**: Son 60 bar fiyat degisimi (60dk trend)
- 3 timeframe aynı yonde uyusursa 1.20x boost
- 0-1 timeframe uyusursa 0.60x discount
- Agirliklar: Mom5 %45, Mom15 %30, Mom60 %25

### Tez
"Microstructure ne oluyor'u gosterir, trend ne olacak'i gosterir." 5dk prediction icin trend devamliligi microstructure gurultusunden daha guclu sinyal.

### Kaldirilan
- Order Book Imbalance (OBI)
- Order Flow Imbalance (OFI)
- OBI Momentum
- Eski composite score agirliklari

### Korunan
- Funding Rate modifier
- Open Interest change modifier
- Volatility gate (Parkinson)
- Volume filter
- Min threshold (0.05)
- T+2s entry delay
- Fixed position size (max(bal*2%, $1))

## Loop29 Baslangic
- **Baslangic:** 12.04.2026 05:47 (TR)
- **Bitis (planlanan):** 12.04.2026 13:47 (TR)
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00
- **Loop Suresi:** 8 saat

## Loop29 Sonuclari — FATAL, erken durduruldu (4 saat)

- **Kapali Trade:** 75
- **Win Rate:** %32.0 (coin flip'ten kotu!)
- **Toplam PnL:** -$29.38
- **Bakiye:** $0.62 (pratik olarak 0)
- **Acik Trade:** 0 (sistem kendi kendine durdu)

### SONUC
Pure price-based FELAKET. Microstructure olmadan algoritma calismiyor.

### 29 Loop Ozeti
- L21: +$22.49, %56.6 (EN IYI — microstructure, linear scaling)
- L22-27: Kucuk tweak'ler, hepsi L21'den kotu
- L28: Momentum + microstructure — ilk iyi, sonra cokus
- L29: Pure price-based — felaket (%32 WR)

### KARAR
Loop30: Loop21 koduna TAMAMEN donus. Hicbir sey ekleme/cikarma. En iyi olan konfigurasyon bu.
