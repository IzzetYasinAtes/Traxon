# Loop 25 - Rapor

## Loop24 -> Loop25 Gecis

### Loop24 Sonuclari
- 580 kapali trade / 8 saat, %52.4 WR, +$8.56 PnL (KARLI)
- DOGE + SOL yildiz: +$27.06 toplam
- BTC en kotu: -$9.88
- KRITIK BULGU: "Guclu" sinyaller (FV>=0.65/<=0.35) %51.9 WR, "Zayif" sinyaller %54.3 WR
- Confidence hesabi TERS calisiyor — extreme composite score daha kotu tahmin

### Arastirma Kaynaklari
- MDPI 2025 — "Confidence-Threshold Framework for Cryptocurrency Price Direction Prediction"
  - Calibration overconfidence'i azaltir, borderline prediction'larda precision artar
- arXiv 2507.22712 — "Order Book Filtration and Directional Signal Extraction at High Frequency"
  - Extreme OBI gecici likidite bosluklari/spoofing, moderate OBI gercek arz/talep kaymasi
- TowardsDataScience — "Price Impact of OBI in Cryptocurrency Markets"
  - OBI-fiyat iliskisi concave — extreme degerler azalan getiri
- Macrosynergy — "How to measure the quality of a trading signal"
  - Precision vs recall tradeoff, Fb score ile olcum

### Arastirma Bulgulari

1. **Extreme OBI/OFI = noise, Moderate = signal**: Akademik literatur extreme order book imbalance'in genellikle gecici oldugunu, moderate imbalance'in gercek bilgi tasadigini dogruluyor. Loop24 verisi bunu kanitladi.

2. **Sqrt (concave) scaling**: Extreme degerleri compress eder. OBI=0.8 ile OBI=0.2 arasindaki fark azalir. Moderate degerler goreceli daha fazla agirlik kazanir.

3. **Fixed confidence > dynamic confidence**: Confidence hesabi TERS calistiginda sabit deger kullanmak daha iyi. Her sinyale esit muamele, overconfidence yok.

### Kod Degisiklikleri

**1. MarketDataWorker.cs — Sqrt Scaling**
- OFI: `ofiDelta * 12` -> `sign(raw) * sqrt(|raw|)` (raw = ofiDelta * 12)
- OBI: `obi * 2.0` -> `sign(raw) * sqrt(|raw|)` (raw = obi * 2.0)
- OBI Momentum: `obiMomentum * 5` -> `sign(raw) * sqrt(|raw|)` (raw = obiMomentum * 5)
- Extreme degerleri compress eder, moderate degerlere daha fazla agirlik verir

**2. AdaptiveSignalGenerator.cs — Fixed Confidence**
- `Clamp(0.50 + |delta| * 3, 0.52, 0.75)` -> sabit `0.60`
- FairValue: UP = 0.60, DOWN = 0.40, Edge = ~0.10

## Loop25 Baslangic
- **Baslangic:** 11.04.2026 07:12 (TR)
- **Bitis (planlanan):** 11.04.2026 15:12 (TR)
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00
- **Loop Suresi:** 8 saat

## Loop25 Sonuclari
_(Loop devam ediyor, sonuclar loop bitiminde eklenecek)_
