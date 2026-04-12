# Loop 31 - Rapor

## KOKLU YENI YAKLASIM — BTC Lead-Lag Strategy

### Neden Bu Yaklasim?
30 loop boyunca tum denemeler altcoin'leri kendi mikroyapi verileriyle tahmin etmeye calisti — hicbiri uzun vadede calismadi. Akademik arastirma farkli bir paradigma oneriyor: **BTC leads, altcoins follow.**

### Arastirma Kaynaklari
- Springer 2026 — "Price Transmission from Bitcoin to Altcoins: High-Frequency Evidence"
  - **BTC'nin 1 dakika onceki fiyat degisimi altcoin'lerin 5dk yonunu %5 significance level'de tahmin ediyor**
  - Granger causality: BTC -> ALT (unidirectional)
  - Kucuk cap coin'ler daha gecikmeli tepki veriyor
- ScienceDirect — "Lead-Lag relationship between Bitcoin and Ethereum"
  - BTC lead-lag effect yuksek frekansta daha guclu
- ACM AI Finance 2025 — "Is BTC Enough? New Perspective on Cryptocurrency Price Formation"
  - BTC related factors en guclu explanatory power (ETH, SOL, BNB icin)
- MDPI — "Statistical Arbitrage in Cryptocurrency Markets"
  - Lag trading: BTC returns → altcoin returns (7.1bp statistical significance)

### Tez
"Altcoin'in kendi verisi yerine, BTC'nin son 1dk hareketi daha iyi tahmin sinyali" (akademik kanit).

### Kod Degisikligi
- **BTC close history tracked** (last 10 bars)
- **scoreBTCLead** feature eklendi (BTC 1dk momentum * 300, clamped)
- Agirliklar: OFI %20, VWAP %5, OBI %25, OBIMom %10, **BTCLead %40** (dominant)
- Diger tum sey (modifier'lar, confidence, OBI history) Loop21 ile ayni

### Yenilikler (30 loop sonra ilk defa)
- **Cross-asset signal**: Bir coin'in yonunu baska coin'in verisi tahmin ediyor
- **Lead-lag exploitation**: Akademik literatur destekli
- **BTC dominant weight %40**: En guclu akademik sinyal

## Loop31 Baslangic
- **Baslangic:** 12.04.2026 11:40 (TR)
- **Bitis (planlanan):** 12.04.2026 19:40 (TR)
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00
- **Loop Suresi:** 8 saat

## Loop31 Sonuclari
_(Loop devam ediyor, sonuclar loop bitiminde eklenecek)_
