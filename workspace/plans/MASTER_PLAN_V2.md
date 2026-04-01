# CryptoTrader Master Plan V2 — Traxon

> Arastirma-tabanli yeniden yapilandirma plani. 30+ WebSearch bulgusu ile desteklenmistir.
> Tarih: 2026-03-29
> Onceki: MASTER_PLAN.md (v1, 2026-03-27)

---

## OZET: NEDEN V2?

V1 ile 4 iterasyon (v1→v4) yapildi, hala **-$527 PnL**. Ancak UP-only **+$320 ile KARLI**.
Sorun: yetersiz veri, tek indikator, tek timeframe, Binance scalping yok.

V2 tamamen arastirma verisine dayanir ve 3 fazda uygulanir.

---

## FAZ 1: VERI ALTYAPISI DEVRIMI (EN ACIL)

### Hedef
Mevcut 20 mumlu yetersiz veri → 500+ mumlu multi-timeframe veri akisi

### 1.1 Multi-Stream WebSocket Mimarisi

```
STREAM 1: 5dk Mumlar (ANA SINYAL)
  - 500 adet rolling buffer
  - Baslangic: REST /api/v3/klines?interval=5m&limit=500
  - Canli: WebSocket kline stream (x:true = kapandi)
  - Kullanim: RSI, MACD, BB, EMA hesaplama + Pattern tespiti

STREAM 2: 1dk Mumlar (ENTRY TIMING)
  - 120 adet rolling buffer (son 2 saat)
  - Baslangic: REST /api/v3/klines?interval=1m&limit=120
  - Canli: WebSocket kline stream
  - Kullanim: Entry anini optimize etmek (5dk sinyal, 1dk giris)

STREAM 3: 1sa Mumlar (TREND KONTEKST)
  - 48 adet rolling buffer (son 2 gun)
  - Baslangic: REST /api/v3/klines?interval=1h&limit=48
  - Canli: WebSocket kline stream
  - Kullanim: Buyuk resim trend yonu, rejim tespiti
```

### 1.2 Degisecek Dosyalar

| Dosya | Degisiklik |
|-------|-----------|
| `Domain/ValueObjects/TimeFrame.cs` | OneMinute, OneHour ekle |
| `Binance/Services/BinanceWebSocketService.cs` | Multi-stream destegi (1m + 5m + 1h) |
| `Binance/Services/BinanceRestService.cs` | Startup backfill (3 istek) |
| `Infrastructure/Services/CandleBufferService.cs` | **YENi** — Rolling buffer yonetimi (500+120+48) |
| `Application/Interfaces/ICandleBuffer.cs` | **YENI** — Buffer interface |
| `Worker/BackgroundServices/DataIngestionService.cs` | Startup backfill + WebSocket baslat |

### 1.3 Buffer Implementasyonu

```
CandleBuffer<T> {
  MaxSize: int
  Add(candle): void — FIFO, eski mumlar otomatik duser
  GetLast(n): Candle[] — Son n mumu getir
  GetAll(): Candle[] — Tum buffer
  Count: int
  IsWarmedUp: bool — Count >= MinRequired
}

SymbolCandleStore {
  Dictionary<(Symbol, TimeFrame), CandleBuffer>
  GetBuffer(symbol, tf): CandleBuffer
  IsReady(symbol): bool — Tum TF'ler warmup tamam mi?
}
```

### 1.4 Kabul Kriteri
- [ ] Her symbol icin 3 stream aktif (1dk, 5dk, 1sa)
- [ ] 5dk buffer 500 muma ulasinca IsWarmedUp = true
- [ ] Startup'ta REST backfill <5sn icinde tamamlaniyor
- [ ] WebSocket disconnect'te otomatik reconnect + gap fill

---

## FAZ 2: SINYAL MOTORU YENIDEN TASARIMI

### Hedef
Tekli RSI/MACD → Multi-indikator skorlama sistemi

### 2.1 Yeni Indikator Seti

| Indikator | Parametre | Agirlik | Rol |
|-----------|-----------|---------|-----|
| RSI(7) | Period=7 | %25 | Ana oversold/overbought |
| MACD(12,26,9) | Standart | %20 | Momentum yonu |
| Bollinger Bands(20,2) | Standart | %20 | Mean reversion seviye |
| EMA(9) | Period=9 | %15 | Kisa vadeli trend |
| Volume(20) | SMA 20 | %10 | Dogrulama/filtre |
| RSI(14) | Period=14 | %10 | Ek dogrulama |

### 2.2 Skorlama Sistemi

```
Her indikator 0.0 - 1.0 arasi skor uretir:

RSI(7) Skor:
  < 20 → 0.9 (cok oversold, STRONG BUY)
  < 30 → 0.7 (oversold, BUY)
  30-50 → 0.5 (neutral)
  > 70 → 0.3 (overbought, SELL)
  > 80 → 0.1 (cok overbought, STRONG SELL)

MACD Skor:
  Histogram yukseliyor + pozitif → 0.8
  Histogram yukseliyor + negatif → 0.6 (donme baslangici)
  Histogram dusuyor + negatif → 0.2
  Crossover (signal line) → +0.1 bonus

BB Skor:
  Fiyat < Alt Band → 0.85 (mean reversion BUY)
  Fiyat Alt Band yakininda → 0.7
  Fiyat Orta Band → 0.5
  Fiyat > Ust Band → 0.15 (mean reversion SELL)
  Squeeze (bandWidth < threshold) → Breakout bekle

EMA(9) Skor:
  Fiyat > EMA(9) ve EMA yukseliyor → 0.7
  Fiyat < EMA(9) ve EMA dusuyor → 0.3

Volume Skor:
  Volume > 1.5x avg → 1.0 (guclu dogrulama)
  Volume > 1.2x avg → 0.8
  Volume < 0.8x avg → 0.4 (zayif hareket, dikkat)

FINAL SKOR = Σ(indikator_skor × agirlik)
```

### 2.3 Sinyal Karari

```
UP Sinyal:
  final_skor > 0.60 VE
  1sa trend yukari (EMA(26) 1h yukseliyor) VE
  volume dogrulama (>1.0x avg)

DOWN Sinyal:
  final_skor < 0.40 VE
  1sa trend asagi VE
  volume dogrulama VE
  EKSTRA: MinBearish confirmations >= 4 (DOWN daha riskli)

Edge Hesabi:
  edge = |final_skor - 0.50| (skor ne kadar 0.50'den uzaksa edge o kadar buyuk)
  MinEdge: 0.08 (minimum %8 edge)
```

### 2.4 Degisecek Dosyalar

| Dosya | Degisiklik |
|-------|-----------|
| `Domain/ValueObjects/TechnicalIndicators.cs` | RSI(7), Volume ekle |
| `Application/Services/SignalGenerator.cs` | Multi-indikator skorlama |
| `Application/Models/SignalScore.cs` | **YENI** — Skor detay modeli |
| `Infrastructure/Services/IndicatorCalculator.cs` | RSI(7), BB, Volume hesaplama |
| `Application/Interfaces/IIndicatorCalculator.cs` | Yeni indikatorler icin interface |

### 2.5 Kabul Kriteri
- [ ] 6 indikator birlikte hesaplaniyor
- [ ] Her indikator 0-1 arasi skor donuyor
- [ ] Final skor agirlikli ortalama ile hesaplaniyor
- [ ] Multi-timeframe dogrulama aktif (1sa trend)
- [ ] Backtest: Win Rate > %55

---

## FAZ 3: TRADE MOTORU OPTIMIZASYONU

### 3.1 Polymarket Iyilestirmeleri

#### 3.1.1 Entry Fiyat Optimizasyonu
```
MEVCUT: entry = market_price (taker, %3.15 fee)
YENI:   entry = maker limit order (market_price - 0.01) → %0 fee + rebate

Maker order avantaji:
- Fee: $0 (vs $1.50-3.00 taker)
- Rebate: taker fee havuzunun %20'si
- Trade basina ~$1.50-3.00 tasarruf
- 100 trade/gun = $150-300/gun EK KAR
```

#### 3.1.2 Dinamik Entry Fiyat
```
MEVCUT: maxEntryPrice = 0.55 (sabit)
YENI: Edge-based entry
  - Edge > 0.15 → maxEntry 0.55
  - Edge > 0.10 → maxEntry 0.50
  - Edge > 0.08 → maxEntry 0.45
  - Edge < 0.08 → GIRIS YAPMA
```

#### 3.1.3 Position Sizing → Quarter Kelly
```
MEVCUT: flat fraction (maxPositionFraction = 0.02)
YENI: Quarter Kelly
  f = (edge / odds) × 0.25

  Ornek: edge=0.10, odds=1.0
  f = 0.10 × 0.25 = 0.025 (%2.5 of bankroll)

  Ornek: edge=0.05, odds=1.0
  f = 0.05 × 0.25 = 0.0125 (%1.25 of bankroll)

  Max cap: %3.0 of bankroll (asla asma)
  Min floor: %0.5 of bankroll (cok kucuk trade'ler islemsiz)
```

### 3.2 Binance Spot Scalping (YENI MOTOR)

#### 3.2.1 Strateji: Mean Reversion Momentum Scalper

```
LONG Entry Kosullari (TUMU saglanmali):
  1. RSI(7) < 30
  2. Fiyat <= BB alt band
  3. MACD histogram yukseliyor (son 2 bar)
  4. Volume > 1.2x avg
  5. 1sa trend: EMA(26) yukari veya yatay (ASAGI degilse)

SHORT Entry Kosullari (TUMU saglanmali):
  1. RSI(7) > 70
  2. Fiyat >= BB ust band
  3. MACD histogram dusuyor
  4. Volume > 1.2x avg
  5. 1sa trend: EMA(26) asagi veya yatay

Exit:
  TP1: Orta BB seviyesi (pozisyonun %60'i)
  TP2: Karsi BB (kalan %40)
  SL: Entry'den %0.3 (scalp) veya %0.5 (swing)
  Time Stop: 15dk (3x 5dk mum) hareket yoksa kapat
```

#### 3.2.2 Binance-Spesifik Parametreler

```json
{
  "binanceScalping": {
    "enabled": true,
    "strategy": "MeanReversionMomentum",
    "riskPerTrade": 0.003,
    "maxOpenPositions": 2,
    "stopLossPercent": 0.3,
    "takeProfitPercent": 0.45,
    "tp1Ratio": 0.60,
    "tp2Ratio": 0.40,
    "timeStopMinutes": 15,
    "enabledSymbols": ["BTCUSDT", "SOLUSDT", "XRPUSDT", "BNBUSDT"],
    "minVolumeFactor": 1.2,
    "counterTrendSizeMultiplier": 0.5
  }
}
```

#### 3.2.3 Degisecek Dosyalar

| Dosya | Degisiklik |
|-------|-----------|
| `Application/Engines/BinanceScalpEngine.cs` | **YENI** — Scalp motoru |
| `Application/Models/ScalpTrade.cs` | **YENI** — TP1/TP2 + TimeStop modeli |
| `Binance/Services/BinanceOrderService.cs` | Spot order (OCO: SL+TP) |
| `Application/Interfaces/IScalpEngine.cs` | **YENI** — Interface |
| `Domain/Entities/Trade.cs` | TP1Hit, TP2Hit, TimeStopHit alanlari |

### 3.3 Kabul Kriteri
- [ ] Maker order ile Polymarket giris (fee = 0)
- [ ] Quarter Kelly position sizing aktif
- [ ] Binance scalp motoru calisiyor (paper mode)
- [ ] TP1/TP2 partial exit calisiyor
- [ ] Time stop 15dk sonra otomatik kapatma

---

## FAZ 4: REJIM TESPITI VE ADAPTASYON

### 4.1 Volatilite Rejimi

```
ATR(14) 5dk basis:
  LowVol:  ATR < %0.3 of price → Mean Reversion agirligi %80
  MedVol:  ATR %0.3-0.8 → Hibrit (50/50)
  HighVol: ATR > %0.8 → Momentum agirligi %70, WIDER SL/TP

Rejim degistiginde:
  - Indikator agirliklari otomatik degisir
  - SL/TP oranlar rejime gore ayarlanir
  - Position size HighVol'da %50 kucultulur
```

### 4.2 Trend Rejimi (1sa Timeframe)

```
1sa EMA(26) slope:
  Bullish:  Son 3 mum yukselen → UP trade'lere oncelik
  Bearish:  Son 3 mum dusen → DOWN trade'lere oncelik (dikkatli)
  Neutral:  Yatay → Sadece guclu sinyallere gir (edge > 0.12)

Multi-TF Dogrulama:
  5dk sinyal UP + 1sa trend UP → STRONG (full size)
  5dk sinyal UP + 1sa trend NEUTRAL → NORMAL (normal size)
  5dk sinyal UP + 1sa trend DOWN → WEAK (yarım size veya SKIP)
```

### 4.3 Degisecek Dosyalar

| Dosya | Degisiklik |
|-------|-----------|
| `Application/Services/RegimeDetector.cs` | Volatilite + Trend rejim |
| `Application/Models/MarketRegime.cs` | LowVol/MedVol/HighVol + Bullish/Bearish/Neutral |
| `Application/Services/SignalGenerator.cs` | Rejime gore agirlik ayari |
| `Application/Services/PositionSizer.cs` | Rejime gore size ayari |

---

## FAZ 5: IZLEME VE ITERASYON

### 5.1 Performans Dashboard Metrikleri

```
ZORUNLU METRIKLER (Admin UI):
- PnL (toplam, gunluk, saatlik)
- Win Rate (genel, UP/DOWN ayri, symbol bazli)
- Sharpe Ratio (rolling 24h)
- Profit Factor
- Max Drawdown
- Ortalama trade suresi
- Indikator skor dagilimi
- Rejim dagilimi (ne kadar zaman hangi rejimde)
- Entry fiyat dagilimi
- Edge dagilimi
```

### 5.2 Otomatik Alarm

```
ALARM KOSULLARI:
- Saatlik PnL < -$100 → Analyst'e bildir
- Win Rate (son 20 trade) < %45 → Analyst'e bildir
- Drawdown > %15 → ACIL — tum trade'leri durdur
- 0 trade / 2 saat → Sinyal esikleri cok yuksek uyarisi
```

### 5.3 A/B Test Cercevesi

```
2 engine paralel calisir:
- Engine A: Mevcut config
- Engine B: Yeni config onerisi
- 200+ trade sonra karsilastir
- Kazanan yeni "best-config" olur
```

---

## UYGULAMA TAKVIMI

| Faz | Oncelik | Bagimlilk | Tahmini Dosya Sayisi |
|-----|---------|-----------|---------------------|
| FAZ 1: Veri Altyapisi | **P0** | Yok | 6 dosya |
| FAZ 2: Sinyal Motoru | **P0** | Faz 1 | 5 dosya |
| FAZ 3: Trade Motoru | **P1** | Faz 2 | 5 dosya (Poly) + 5 dosya (Binance) |
| FAZ 4: Rejim Tespiti | **P2** | Faz 1+2 | 4 dosya |
| FAZ 5: Izleme | **P3** | Faz 1-4 | Dashboard guncelleme |

**Faz 1+2 birlikte yapilabilir** (bagimsiz parcalari paralel).
**Faz 3 ikiye bolunebilir:** Once Polymarket iyilestirmeleri, sonra Binance scalping.

---

## ONERILEN YENi BEST-CONFIG (FAZ 2 SONRASI)

```json
{
  "configId": "analyst-v5-research-based",
  "parameters": {
    "minCandlesForSignal": 100,
    "minEdge": 0.08,
    "minEdgeLowVol": 0.10,
    "minEdgeHighVol": 0.06,
    "kellyMultiplier": 0.25,
    "maxPositionFraction": 0.03,
    "maxExposureFraction": 0.12,
    "minConfirmation": 3,
    "minBearishConfirmations": 4,
    "disableDown": false,
    "rsiShortPeriod": 7,
    "rsiLongPeriod": 14,
    "macdFast": 12,
    "macdSlow": 26,
    "macdSignal": 9,
    "bollingerPeriod": 20,
    "bollingerStdDev": 2.0,
    "emaPeriod": 9,
    "volumePeriod": 20,
    "atrPeriod": 14,
    "indicatorWeights": {
      "rsi7": 0.25,
      "macd": 0.20,
      "bollingerBands": 0.20,
      "ema9": 0.15,
      "volume": 0.10,
      "rsi14": 0.10
    },
    "signalThresholdUp": 0.60,
    "signalThresholdDown": 0.40,
    "stopLossPercent": 0.3,
    "takeProfitPercent": 0.45,
    "tp1Ratio": 0.60,
    "timeStopMinutes": 15,
    "minMarketPrice": 0.30,
    "maxMarketPrice": 0.55,
    "dynamicEntryEnabled": true,
    "makerOrderEnabled": true,
    "enabledSymbols": ["BTCUSDT", "SOLUSDT", "XRPUSDT", "BNBUSDT", "DOTUSDT"],
    "enabledTimeframes": {
      "PaperBinance": ["5m"],
      "PaperPoly": ["5m", "15m"]
    },
    "bufferSizes": {
      "1m": 120,
      "5m": 500,
      "1h": 48
    },
    "regimeDetection": {
      "lowVolThreshold": 0.003,
      "highVolThreshold": 0.008,
      "trendLookback": 3
    },
    "maxOpenPositions": 3,
    "bankroll": 10000
  }
}
```

---

## BASARI KRITERLERI

| Kriter | Hedef | Olcum |
|--------|-------|-------|
| PnL | POZITIF | 200+ trade sonrasi |
| Win Rate | > %55 | Genel |
| UP Win Rate | > %60 | UP trade'ler |
| Sharpe Ratio | > 1.0 | Rolling 24h |
| Profit Factor | > 1.5 | Tum trade'ler |
| Max Drawdown | < %15 | Herhangi bir anda |
| Trade Frekansi | 50-150/gun | Poly + Binance toplam |
| Maker Order Orani | > %70 | Polymarket'te |

---

> Bu plan arastirma verisine dayanir ve iteratif olarak guncellenecektir.
> Her faz tamamlandiginda Analyst performansi olcecek ve bir sonraki fazi sekllendirecektir.
