# Loop 28 - Rapor

## KOKLU DEGISIKLIK — Momentum replaces VWAP, Loop21 base restored

### Neden Koklu Degisiklik?
27 loop boyunca kucuk tweak'ler denendi. Son 3 loop (L25-27) hepsi basarisiz:
- Loop25: %51.4 WR, -$10.79 (sqrt scaling)
- Loop26: %39.2 WR, -$21.77 (EntryPrice filter)
- Loop27: %48.7 WR, -$25.29 (OBI dominant)

### Arastirma — Kok Nedenler
1. **OBI prediktif penceresi 5-30 SANIYE**: Order book imbalance saniyeler icin gecerli. Biz 5 dakika tahmin ediyoruz. Fundamental mismatch.
2. **VWAP mean reversion YANLIS**: Crypto'da 5dk icinde trend devam eder, revert etmez. VWAP Z-Score ters sinyal veriyor.
3. **Latency arbitrage oldu**: Polymarket dynamic fee ile latency botlarini oldurdu. Artik tahmin bazli edge gerek.
4. **Akademik konsensus**: Kisa vadeli crypto yon tahmini %50'yi zar zor geciyor. Feature engineering > model complexity.

### Yeni Yaklasim
- **TEZ**: "OBI ile yon TAHMIN etme, MOMENTUM ile yon belirle, OBI ile TEYIT et"
- Momentum = son 5 bar'in fiyat degisim yonu (dakika bazinda trend)
- OBI = anlık order book durumu (teyit)
- OFI = taker flow (teyit)
- VWAP KALDIRILDI — mean reversion 5dk crypto'da calismiyorYeni

### Kod Degisiklikleri
1. **3 dosya Loop21'e geri alindi** — sqrt scaling, fixed confidence, OBI 30s, OFI 2v6 HEPSI geri alindi
2. **VWAP Z-Score silindi** → Price Momentum eklendi: `(Close[-1] - Close[-6]) / Close[-6] * 200`
3. **Agirliklar**: OFI %25, Momentum %20, OBI %35, OBIMom %20
4. **Loop21 base korundu**: OBI 12 sample, OFI 1v3 15x, confidence 8x/0.90

## Loop28 Baslangic
- **Baslangic:** 11.04.2026 21:40 (TR)
- **Bitis (planlanan):** 12.04.2026 05:40 (TR)
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00
- **Loop Suresi:** 8 saat

## Loop28 Sonuclari — BASARISIZ: -$28.65 | %49.2 WR | 543 trade / 8 saat

- **Bitis:** 12.04.2026 05:42 (TR)
- **Sure:** 8 saat
- **Kapali Trade:** 543
- **WR:** %49.2
- **Toplam PnL:** -$28.65
- **Momentum yaklasimi da BASARISIZ** — ayni son 2 saatte cokus pattern'i
- **SOL Up kronik:** %36 WR, -$10.68
- **BTC Up kronik:** %42 WR, -$8.51
- **ETH Down:** %43 WR, -$6.86
- **DOGE + BNB Down karli** ama digerleri capitulated

### Trend Analizi
- +2h: +$6.63 (umut verici)
- +4h: +$4.05 (hafif erozyon)
- +6h: -$1.08 (breakeven)
- +8h: -$28.65 (felaket) — son 2 saatte -$27.57 kayip

### SONUC
Momentum + Loop21 base de BASARISIZ. Ayni "ilk saatler iyi, sonra cokus" pattern'i tekrarlandi.
