## Strateji Analiz Raporu — 2026-03-29

### KRITIK DURUM: SISTEM TRADE ACMIYOR

**Trades tablosu BOS** — 0 trade. Sistem candle topluyor ama hicbir sinyal trade'e donusmuyor.

---

### Temel Sorunlar (Oncelik Sirasina Gore)

#### 1. MINIMUM 50 CANDLE GEREKSINIMI — EN BUYUK ENGEL
- `SignalGenerator.cs:19` — `MinCandlesForSignal = 50`
- DB'de toplam candle sayisi: BNB=32, BTC=10, ETH=3, XRP=1
- **Hicbir symbol 50 candle'a ulasmamis** → Sinyal uretimi IMKANSIZ
- 5dk candle ile 50 candle = 250 dakika = ~4.2 saat gerekli
- Sistem yeni basladiysa, ilk sinyal icin 4+ saat beklemek gerekiyor
- **ONERI: MinCandlesForSignal 50 → 20 dusurulmeli** (RSI 14 periyot + 6 candle yeterli)

#### 2. SIMULATED MARKET PRICE = 0.50 HARDCODED
- `MarketDataWorker.cs:149` — `simulatedMarketPrice = 0.50m`
- Polymarket'te gercek fiyatlar %50-55 arasi
- Eger FV=0.60 ve market=0.50 → edge = 0.10 → MinEdge 0.20 ile ELENIR
- Eger FV=0.65 ve market=0.50 → edge = 0.15 → MinEdge 0.20 ile YINE ELENIR
- **FV genelde 0.50-0.70 arasi → edge neredeyse hic 0.20'yi gecmez!**
- **ONERI: simulatedMarketPrice gercek Polymarket fiyatina baglenmali veya MinEdge dusurulmeli**

#### 3. MIN EDGE COK YUKSEK
- `PositionSizer.cs:8-9` — MinEdge=0.20, MinEdgeLowVol=0.25
- FV tipik olarak 0.50-0.70 arasi → market 0.50 ile edge = 0.00-0.20
- Edge esigi neredeyse HICBIR sinyalin gecmesine izin vermiyor
- **ONERI: MinEdge 0.20 → 0.08, MinEdgeLowVol 0.25 → 0.12**

#### 4. DOWN TRADE'LER DEVRE DISI
- `SignalGenerator.cs:31` — `MinBearishConfirmations = 99` (5 indikatorle imkansiz)
- Dusus piyasasinda KAR EDEMIYORUZ
- **ONERI: MinBearishConfirmations 99 → 4** (4/5 bearish = net dusus sinyali)

#### 5. MIN BULLISH CONFIRMATIONS COK YUKSEK
- `SignalGenerator.cs:30` — `MinBullishConfirmations = 3`
- Kisa vadeli kripto genelde 1-2 bullish gosterir, 3/5 nadir
- **ONERI: MinBullishConfirmations 3 → 2**

#### 6. POSITION SIZING COK KUCUK
- KellyMultiplier=0.05 (sadece %5 Kelly) + MaxPositionFraction=0.005 (%0.5)
- $10k bankroll ile max $50 pozisyon → %1.2 TP ile max $0.60 kar/trade
- **ONERI: KellyMultiplier 0.05 → 0.15, MaxPositionFraction 0.005 → 0.02**

---

### DB Durumu

| Tablo | Kayit | Aciklama |
|-------|-------|----------|
| Trades | 0 | HIC trade yok |
| Candles | 46 | BNB:32, BTC:10, ETH:3, XRP:1 |

Son candle zamani: 2026-03-29 00:34 UTC (BNB 5m)
SOL candle'i HIC YOK — enabledSymbols'de var ama veri gelmiyor.

---

### Web Arastirma Bulgulari

#### Polymarket 5dk Market Stratejileri
1. **Endcycle Sniper**: Market'in son 30-60 saniyesinde, fiyat neredeyse kesinlesmisken gir. >5-10% edge varsa islem yap.
2. **5dk BTC binary'ler random walk'a yakin**: Naive win rate %25-27. Breakeven %53. Momentum filtresi ile kayip azaltilabiliyor.
3. **10dk trend filtresi**: v3 engine'de kayip %93'ten %13'e dusurulmus.
4. **Kaynak**: [AI-Augmented Arbitrage in Short-Duration Prediction Markets](https://medium.com/@gwrx2005/ai-augmented-arbitrage-in-short-duration-prediction-markets-live-trading-analysis-of-polymarkets-8ce1b8c5f362)

#### Mean Reversion vs Momentum
1. Mean reversion: Sharpe ~2.3 (BTC-neutral), win rate %60-70
2. Momentum: Pre-2021 iyi, sonra etkisi azalmis
3. **Hibrit 50/50 portfolio**: Sharpe 1.71, yillik %56 getiri
4. Bollinger Band + RSI en etkili mean reversion indikatoru
5. **Kaynak**: [Systematic Crypto Trading Strategies](https://medium.com/@briplotnik/systematic-crypto-trading-strategies-momentum-mean-reversion-volatility-filtering-8d7da06d60ed)

#### RSI Scalping (5dk)
1. RSI(7) period, 30/70 seviyeleri 5dk icin optimal
2. RSI < 30 + yesil mum kapanisi = oversold bounce entry
3. 20 EMA + 50 EMA ustunde olmali (trend filtre)
4. Agresif: RSI(2-3) period, 20/80 seviyeleri
5. **Kaynak**: [Best RSI Settings for 5-Minute Charts](https://eplanetbrokers.com/en-US/training/best-rsi-settings-for-5-minute-charts)

#### Kelly Criterion (Binary Options)
1. Kucuk edge'de %25-50 fractional Kelly onerilir (0.25-0.50 multiplier)
2. Mevcut 0.05 multiplier COK DUSUK — edge dogru hesaplanirsa 0.15-0.25 uygun
3. Edge GERCEK olmazsa Kelly ise yaramaz — once edge'i dogru hesapla
4. **Kaynak**: [The Math of Prediction Markets: Binary Options, Kelly Criterion](https://navnoorbawa.substack.com/p/the-math-of-prediction-markets-binary)

#### Polymarket Endcycle Sniper
1. Son 30-60 saniyede gir, fiyat kesinlesmis durumda
2. 0.95+ fiyattan UP/DOWN al, resolution'da 1.00 olur → %5 kar
3. Dusuk likidite ($5K-$50K/window) — buyuk pozisyon zor
4. **Kaynak**: [Unlocking Edges in Polymarket's 5-Minute Crypto Markets](https://medium.com/@benjamin.bigdev/unlocking-edges-in-polymarkets-5-minute-crypto-markets-last-second-dynamics-bot-strategies-and-db8efcb5c196)

---

### SOMUT PARAMETRE DEGISIKLIKLERI (Oncelik Sirasi)

#### [P0 - KRITIK] 1. MinCandlesForSignal Dusur
- **Dosya:** `SignalGenerator.cs:19`
- **Eski:** `MinCandlesForSignal = 50`
- **Yeni:** `MinCandlesForSignal = 20`
- **Neden:** Hicbir symbol 50 candle'a ulasmamis. 20 candle RSI(14) icin yeterli.
- **Risk:** DUSUK — 20 candle indikator hesaplamasi icin yeterli

#### [P0 - KRITIK] 2. MinEdge Dusur
- **Dosya:** `PositionSizer.cs:8-9`
- **Eski:** `MinEdge=0.20, MinEdgeLowVol=0.25`
- **Yeni:** `MinEdge=0.08, MinEdgeLowVol=0.12`
- **Neden:** market=0.50 hardcoded ile FV genelde 0.55-0.70 → edge max 0.20. Mevcut esik tum sinyalleri eliyor.
- **Risk:** ORTA — daha fazla trade = daha fazla kayip potansiyeli. Ama 0 trade = 0 veri = optimizasyon imkansiz.

#### [P1 - YUKSEK] 3. MinBullishConfirmations Dusur
- **Dosya:** `SignalGenerator.cs:30`
- **Eski:** `MinBullishConfirmations = 3`
- **Yeni:** `MinBullishConfirmations = 2`
- **Neden:** 5dk kriptoda 3/5 bullish nadir. 2/5 daha gercekci.
- **Risk:** DUSUK-ORTA

#### [P1 - YUKSEK] 4. DOWN Trade'leri Dikkatli Etkinlestir
- **Dosya:** `SignalGenerator.cs:31`
- **Eski:** `MinBearishConfirmations = 99`
- **Yeni:** `MinBearishConfirmations = 4`
- **Neden:** 4/5 bearish = guclu dusus sinyali. Onceki sorun 2/5 ile acilmasiydi.
- **Risk:** ORTA — eski veride DOWN felaket ama 2/5 esikle. 4/5 cok daha secici.

#### [P2 - ORTA] 5. Position Sizing Artir
- **Dosya:** `PositionSizer.cs:10-11`
- **Eski:** `KellyMultiplier=0.05, MaxPositionFraction=0.005`
- **Yeni:** `KellyMultiplier=0.15, MaxPositionFraction=0.02`
- **Neden:** $50 max pozisyon ile anlamli kar imkansiz. $200 max hala muhafazakar.
- **Risk:** ORTA

#### [P2 - ORTA] 6. FV-Direction Filter Gevselt
- **Dosya:** `SignalGenerator.cs:92`
- **Eski:** `fairValue < 0.48m` (UP icin minimum FV)
- **Yeni:** `fairValue < 0.45m`
- **Neden:** 0.48 cok siki. 0.45 hala mantikli alt sinir.
- **Risk:** DUSUK

#### [P2 - ORTA] 7. Binance SL/TP Iyilestir
- **Dosya:** `PaperBinanceEngine.cs`
- **Eski:** `SL=0.5%, TP=1.0%` (1:2 ratio)
- **Yeni:** `SL=0.4%, TP=1.2%` (1:3 ratio)
- **Neden:** Dusuk WR ile 1:3 ratio daha uygun. %25 WR ile bile karli.
- **Risk:** DUSUK

#### [P3 - GELECEK] 8. simulatedMarketPrice Dinamik Yap
- **Dosya:** `MarketDataWorker.cs:149`
- **Eski:** `simulatedMarketPrice = 0.50m`
- **Yeni:** Gercek Polymarket API'den fiyat cek
- **Neden:** Sabit 0.50 edge hesaplamasini yaniltir.
- **Risk:** Mimarsal degisiklik — Developer ile planlanmali

---

### En Iyi Config Durumu

- **Mevcut en iyi:** analyst-v2-20260329
- **Performans:** -$588.55 PnL, %0 WinRate, 5 trade (onceki veriden, simdi DB bos)
- **Mevcut durum:** DB'de 0 trade, 46 candle — sistem TRADE ACAMIYOR
- **Degisiklik onerilir mi:** **EVET — ACIL. 7 parametre degisikligi gerekli.**

---

### Sonuc

Sistem **teknik olarak trade acamiyacak durumda**:
1. **50 candle gerekli, max 32 var** → Hicbir sinyal uretilemiyor
2. **Edge esigi 0.20** ile market=0.50 hardcoded → Edge neredeyse hic 0.20'yi gecmiyor
3. **DOWN tamamen kapali** → Dusus = kayip firsati
4. **Pozisyon boyutu $50 max** → Anlamli kar icin cok kucuk

**ONCELIK: Once trade acabilmek. Sonra veri topla. Sonra optimize et.**
Veri olmadan optimizasyon IMKANSIZ.
