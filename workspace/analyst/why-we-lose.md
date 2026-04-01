# NEDEN KAYBEDIYORUZ? — Kok Neden Analizi

**Tarih:** 2026-03-29
**Analyst Agent Raporu**
**Durum:** KRITIK — WinRate %46-49, random'dan kotu

---

## EXECUTIVE SUMMARY

Sistemimiz 5 dakikalik kripto fiyat tahminlerinde **rastgele secimden daha kotu** performans gosteriyor. 7 farkli kok neden tespit ettim. Bunlarin 3'u **KRITIK**, 2'si **YUKSEK**, 2'si **ORTA** oncelikli.

**Ana Bulgu:** Sorun tek bir parametrede degil, **yaklasimin kendisinde**. Lagging indikatorlerle 5dk kripto tahmini yapmak, akademik arastirmalara gore **en iyi ihtimalle %55-65 accuracy** verir. Bizim sistemimiz bunu bile basaramiyor cunku ek yapisal sorunlar var.

---

## KOD ANALIZI BULGULARI

### 1. GenerateV2 AKTIF — Eski Generate Kullanilmiyor ✅
- `MarketDataWorker.cs:179` — `GenerateV2()` cagriliyor
- V2 weighted scoring aktif, BullishCount bypass ediliyor
- **Bu bir sorun degil.**

### 2. V2 Skorlama Sistemi — Agirliklar
`TechnicalIndicators.cs:84-136` — Weighted score:

| Indikator | Agirlik | Tip | Sorun |
|-----------|---------|-----|-------|
| RSI7 | %25 | **LAGGING** | Gecmis 7 mumu gosteriyor |
| MACD | %20 | **LAGGING** | 12-26-9 EMA farki, en yavas indikator |
| Bollinger | %20 | **LAGGING** | 20 periyot SMA bazli |
| EMA9 | %15 | **LAGGING** | 9 periyot ortalama |
| Volume | %10 | **NÖTR** | Dogrulama ici, yön gostermiyor |
| RSI14 | %10 | **LAGGING** | Gecmis 14 mumu gosteriyor |

**SONUC: Tum indikatorlerin %90'i LAGGING. Gecmise bakarak gelecegi tahmin etmeye calisiyoruz.**

### 3. simulatedMarketPrice = 0.50 — KRITIK SORUN ❌
- `MarketDataWorker.cs:173` — Hardcoded `0.50m`
- Fair value hesabi bu degere gore yapiliyor
- `SignalGenerator.cs:23-24` — FV siniri: min 0.30, max 0.80
- **Fair value her zaman 0.50 civarinda basliyorsa, edge hesabi YANLIS.**
- Gercek piyasa fiyati bu hesaba girmiyor — simulatedMarketPrice gercek degil

### 4. Black-Scholes 5dk Crypto Icin UYGUN DEGIL ❌
- `FairValueCalculator.cs:8-10` — Black-Scholes d2 formulu kullaniliyor
- `d2 = (mu - sigma^2/2) / sigma`, `P(Up) = Phi(d2)`
- **Black-Scholes varsayimlari:**
  - Log-normal dagılım (crypto: HAYIR — fat tails, skewed)
  - Sabit volatilite (crypto: HAYIR — volatilite anlik degisiyor)
  - Surekli islem (crypto: 7/24 ama likidite degisiyor)
  - Surtusmesiz piyasa (crypto: slippage, spread var)
- Akademik arastirma: BS modeli kripto opsiyonlarinda **en yuksek fiyatlama hatasi** veriyor
- Alternatifler: Kou modeli (BTC icin en iyi), Bates modeli (ETH icin en iyi), ML modelleri

### 5. DOWN Trading Devre Disi ❌
- `SignalGenerator.cs:30` — `MinBearishConfirmations = 99` (pratik olarak devre disi)
- Sadece UP sinyaller uretiliyor
- Piyasa dususte bile UP sinyal veriyoruz — **bu buyuk kayip sebebi**
- Onceki -$847 kayip nedeniyle kapatilmis ama cozum DOWN'i kapatmak degil, daha iyi sinyal uretmek

### 6. Pozisyon Parametreleri Cok Dar
- `PaperBinanceEngine.cs:36-37` — SL: %0.4, TP: %1.2
- MaxHold: 10 mum (50dk)
- 5dk'lik kripto volatilitesinde %0.4 SL **cok dar** — noise ile tetikleniyor
- Reward/Risk 3:1 iyi ama SL noise'a kurban gidiyor

### 7. Hourly Trend Confirmation Zayif
- `SignalGenerator.cs:251-265` — 1h SMA(5) vs SMA(10) karsilastirma
- **Sinyal trend confirmation OLMADAN da uretilebiliyor**
- Sadece "noted" ediliyor, filtreleme yapmıyor

---

## WEB ARASTIRMA BULGULARI

### Bulgu 1: 5dk Crypto Tahmini Icin Akademik Limit
- ML modelleri bile 5dk'da **en iyi %55-65** accuracy
- En yuksek %10 confidence sinyallerinde bile **%57-60** accuracy
- Bizim %46-49 WinRate, iyi bir sistemin bile alt sinirinin **altinda**
- **Kaynak:** [ArXiv — Bitcoin Price Prediction with ML](https://arxiv.org/html/2410.06935v1)

### Bulgu 2: RSI + MACD Tek Basina Yetersiz
- RSI ve MACD **lagging** indikatorler — trendleri **sonradan** teyit eder
- Sideways piyasada **false signal** uretir
- Guclu trendde yaniltici olur (RSI overbought'ta kalir, fiyat yuksemeye devam eder)
- **Birlikte kullanildiginda** Gate.io backtestinde %77 winrate — AMA bu daily timeframe
- **Kaynak:** [Gate.io — MACD RSI Analysis 2026](https://www.gate.com/crypto-wiki/article/how-do-macd-and-rsi-indicators-predict-crypto-price-movements-in-2026-20260102)

### Bulgu 3: Leading Indikatorler Gerekli
- **Non-lagging indikatorler:** True Strength Index, Fisher Transform, Stochastic RSI, Pivot Points
- Lagging indikatorler teyit icin, leading indikatorler giris icin kullanilmali
- **Kaynak:** [Gate Wiki — Top 5 Non-Lagging Indicators](https://www.gate.com/crypto-wiki/article/top-5-non-lagging-indicators-for-crypto-trading-20260115)

### Bulgu 4: Order Flow / CVD Kisa Vadede Etkili
- Cumulative Volume Delta (CVD) — **4 saat altinda en etkili**
- Alis/satis basinc dengesizligi geleceyi lagging indikatorlerden daha iyi gosteriyor
- Order book imbalance 5-60 saniye ileriye tahmin edebiliyor
- **5dk icin yeterli degil ama 1dk entry timing icin kullanilabilir**
- **Kaynak:** [Bookmap — CVD Trading](https://bookmap.com/blog/how-cumulative-volume-delta-transform-your-trading-strategy)

### Bulgu 5: VWAP Scalping'de Guclu
- VWAP + EMA + Fibonacci confluence: backtest'te **%77.78 winrate** (72 trade, 56 dogru)
- VWAP hem destek/direnc hem de yön gosteriyor
- 1-5dk timeframe icin ideal
- **Kaynak:** [Cryptowisser — VWAP EMA Scalping 2026](https://www.cryptowisser.com/guides/fibonacci-vwap-ema-crypto-scalping/)

### Bulgu 6: Black-Scholes Crypto Icin En Kotu Model
- Kripto opsiyonlarinda **en yuksek fiyatlama hatasi**
- Kou modeli BTC icin, Bates modeli ETH icin daha iyi
- ML modelleri tum klasik modelleri geciyor
- **Kaynak:** [CryptoDataDownload — BS Assumptions](https://www.cryptodatadownload.com/blog/posts/black-scholes-options-assumptions-crypto-markets/)

### Bulgu 7: Orderbook Imbalance Guclu Sinyal
- LOB (Limit Order Book) imbalance kisa vadeli fiyat hareketini tahmin ediyor
- Linear modeller 500ms gelecek return'un **%10-37** varyansini acikliyor
- Ama bizim sistemde orderbook verisi YOK — sadece OHLCV kullaniyoruz
- **Kaynak:** [ArXiv — Microstructure Dynamics](https://arxiv.org/html/2506.05764v2)

---

## KOK NEDENLER (ONCELIK SIRASINA GORE)

### ❌ KRITIK 1: Tum Indikatorler LAGGING
**Sorun:** RSI, MACD, BB, EMA — hepsi gecmis verilerin ortalaması. 5dk'da fiyat degisimi bu ortalamalara yansimadan ONCE gerceklesiyor.
**Etki:** Sinyal urettigimizde fiyat hareketi coktan olmus oluyor. "UP" dedigimizde hareket bitmis, ters donuyor.
**Cozum:** Leading indikatorlere gecis — VWAP, Stochastic RSI, CVD, Fisher Transform

### ❌ KRITIK 2: simulatedMarketPrice = 0.50 Hardcoded
**Sorun:** Fair value hesabi sabit 0.50 market price'a gore yapiliyor. Gercek piyasa fiyati kullanilmiyor.
**Etki:** Edge hesabi (FV - MarketPrice) her zaman ayni baseline'dan basliyor. MinEdge filtresi anlamsizlasiyor.
**Cozum:** Gercek Polymarket market price API'den cekilmeli veya Black-Scholes tamamen kaldirilmali.

### ❌ KRITIK 3: Black-Scholes 5dk Crypto Icin Yanlis Model
**Sorun:** BS log-normal dagilim, sabit volatilite, surekli islem varsayiyor. Crypto bunlarin HICBIRINI saglamiyor.
**Etki:** Fair value hesabi sistematik olarak yanlis. Bu yanlis FV uzerine kurulan tum karar mekanizmasi coker.
**Cozum:** BS yerine basit momentum-bazli probability veya ML-based scorer kullan.

### ⚠️ YUKSEK 4: DOWN Trading Kapatilmis
**Sorun:** MinBearishConfirmations = 99, sadece UP sinyal uretiyor.
**Etki:** Dusus piyasasinda para kaybetmeye mahkumuz — sadece yukselise oynuyoruz.
**Cozum:** DOWN'i ac ama daha iyi sinyallerle. Leading indikatorler DOWN icin de calisir.

### ⚠️ YUKSEK 5: SL %0.4 Noise'a Cok Yakin
**Sorun:** 5dk'lik crypto volatilitesinde %0.4 stop loss cok dar.
**Etki:** Dogru yonde olan trade'ler bile kucuk dalgalanmalarla SL'ye takilip kaybediyor.
**Cozum:** ATR-bazli dinamik SL veya %0.8-1.0 sabit SL. Ya da timeframe'i 15dk'ya cikar.

### 🔶 ORTA 6: Trend Confirmation Filtrelemiyor
**Sorun:** 1h trend confirmation "noted" ediliyor ama sinyal uretimini ENGELLEMIYOR.
**Etki:** Counter-trend sinyaller uretilebiliyor — dusus trendinde UP sinyal.
**Cozum:** Trend confirmation'i zorunlu yap — 1h trend DOWN ise UP sinyal URETME.

### 🔶 ORTA 7: Volume Sadece %10 Agirlik
**Sorun:** Volume ratio sadece dogrulama icin %10 agirlikla kullaniliyor.
**Etki:** Dusuk volume'da uretilen sinyaller guclu sinyallerle ayni muameleyi goruyor.
**Cozum:** Volume'u entry gate yap — min volume threshold'un altinda sinyal URETME.

---

## ONERILEN COZUM PLANI

### Faz 1 — Acil (Bugun)
1. **simulatedMarketPrice sorununu coz** — gercek market price kullan veya BS'yi devre disi birak
2. **Trend confirmation'i zorunlu yap** — counter-trend sinyalleri engelle
3. **SL'yi %0.8'e cikar** — noise filtresi

### Faz 2 — Kisa Vade (1-3 Gun)
4. **VWAP indikatoru ekle** — leading indicator olarak ana sinyal kaynagi yap
5. **Stochastic RSI ekle** — RSI14 yerine, daha hizli reversal tespiti
6. **Volume gate ekle** — dusuk volume'da trade YAPMA
7. **DOWN trading'i VWAP bazli ac** — fiyat VWAP altindaysa ve dususse DOWN sinyal

### Faz 3 — Orta Vade (1 Hafta)
8. **Black-Scholes'u kaldir** — yerine basit momentum probability:
   - `P(Up) = weighted_avg(VWAP_position, StochRSI, volume_trend, 1h_trend)`
   - Agirliklar: VWAP %30, StochRSI %25, Volume %20, Trend %25
9. **Timeframe deneyimi** — 15dk ile paralel test yap, karsilastir
10. **CVD/Volume Delta** ekle (Binance API'den alinabilir)

### Faz 4 — Uzun Vade (2+ Hafta)
11. **Orderbook imbalance** verisi ekle (Binance WebSocket)
12. **ML-based scorer** — son 1000 trade'den ogrenme
13. **Adaptive SL** — ATR-bazli dinamik stop loss

---

## BEKLENEN ETKI

| Degisiklik | Beklenen WinRate Etkisi | Guvence |
|------------|------------------------|---------|
| simulatedMarketPrice fix | +%3-5 | YUKSEK |
| Trend confirmation zorunlu | +%5-8 | YUKSEK |
| SL %0.8 | +%3-5 (daha az noise SL) | ORTA |
| VWAP ekleme | +%5-10 | YUKSEK (backtest destekli) |
| StochRSI ekleme | +%2-4 | ORTA |
| Volume gate | +%2-3 | ORTA |
| BS kaldirma + momentum prob | +%3-5 | ORTA |
| **TOPLAM BEKLENEN** | **%69-89 (ust sinir iyimser)** | — |
| **GERCEKCI HEDEF** | **%58-65** | — |

> Not: Bu etkiler birbiriyle correlated, toplanamazlar. Gercekci hedef %58-65 WinRate.

---

## SONUC

**Neden kaybediyoruz?** Cunku:
1. Gecmise bakan indikatorlerle gelecegi tahmin etmeye calisiyoruz
2. Fair value hesabimiz hardcoded deger ve crypto'ya uygun olmayan model kullaniyor
3. Sadece UP yonunde oynuyoruz
4. Stop loss noise'a cok yakin

**Cozum var mi?** EVET — ama yaklasim degisikligi gerekiyor. Parametre tweaking degil, **mimari degisiklik**. VWAP, StochRSI, CVD gibi leading indikatorler, trend filtreleme ve gercek market price kullanimi ile %58-65 WinRate hedeflenebilir.

**Parametre degistirmek neden ise yaramiyor?** Cunku sorun parametrelerde degil, kullanilan indikatorlerin LAGGING olmasi ve fair value modelinin yanlis olmasi. Yanlis araclarin parametrelerini optimize etmek, yanlis araci daha iyi kullanmak demek — dogru araci kullanmak degil.

---

## KAYNAKLAR

- [ArXiv — Bitcoin Price Prediction with Enhanced Technical Indicators](https://arxiv.org/html/2410.06935v1)
- [Gate.io — MACD RSI Indicators 2026](https://www.gate.com/crypto-wiki/article/how-do-macd-and-rsi-indicators-predict-crypto-price-movements-in-2026-20260102)
- [Gate Wiki — Top 5 Non-Lagging Indicators](https://www.gate.com/crypto-wiki/article/top-5-non-lagging-indicators-for-crypto-trading-20260115)
- [Bookmap — CVD Trading Strategy](https://bookmap.com/blog/how-cumulative-volume-delta-transform-your-trading-strategy)
- [Cryptowisser — VWAP EMA Fibonacci Scalping 2026](https://www.cryptowisser.com/guides/fibonacci-vwap-ema-crypto-scalping/)
- [CryptoDataDownload — Black-Scholes Assumptions](https://www.cryptodatadownload.com/blog/posts/black-scholes-options-assumptions-crypto-markets/)
- [ArXiv — Microstructure Dynamics in Crypto LOB](https://arxiv.org/html/2506.05764v2)
- [CoinMarketCap — Volume Delta Order Flow](https://coinmarketcap.com/academy/article/what-is-volume-delta-the-ultimate-order-flow-indicator)
- [ArXiv — Order Flow Image Representation](https://arxiv.org/html/2304.02472v2)
- [Phemex — Top Non-Lagging Indicators 2025](https://phemex.com/academy/top-non-lagging-indicators)
