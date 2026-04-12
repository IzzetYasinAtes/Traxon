# Loop 30 - Rapor

## Loop30 = Loop21 (TAM DONUS)

### 29 Loop Ogrenmesi
- **Loop21: +$22.49, %56.6 — EN IYI** (microstructure OBI+OFI+VWAP, linear scaling, confidence 8x/0.90)
- Loop22-29 arasi her "iyilestirme" kotu sonuc verdi
- Momentum ekleme, OBI dominant agirlik, pure price-based, EntryPrice filter — hepsi basarisiz
- Loop29: %32 WR, bakiye 4 saatte bitti (FATAL)

### Bu Loop Neden?
29 loop boyunca kanitladik ki: **Loop21 konfigurasyonu bu problem icin en iyi**. Daha fazla "iyilestirme" denemek yerine, Loop21'in 8 saatlik tam potansiyelini gorecegiz.

### Kod = Loop21 Commit (030e3d9)
- OFI Delta (1v3 bar, scale 15x) — agirlik %35
- VWAP Z-Score (60 bar) — agirlik %10
- OBI (12 sample, scale 2.0x) — agirlik %40
- OBI Momentum (scale 8x) — agirlik %15
- Confidence: `0.50 + |delta| * 8, [0.52, 0.90]`
- Modifiers: Funding rate, OI change, volatility gate
- Threshold: 0.05
- Position size: max(bal * 2%, $1)

### Hic Degisiklik Yok
Bu loop'ta HICBIR yeni sey denenmeyecek. Sadece Loop21'in 8 saatlik performansini olcecegiz.

## Loop30 Baslangic
- **Baslangic:** 12.04.2026 09:52 (TR)
- **Bitis (planlanan):** 12.04.2026 17:52 (TR)
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00
- **Loop Suresi:** 8 saat

## Loop30 Sonuclari
_(Loop devam ediyor, sonuclar loop bitiminde eklenecek)_
