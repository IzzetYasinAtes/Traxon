# CryptoTrader Master Plan V3 — Traxon
# SAATTE 560+ POLYMARKET + 50-100 BINANCE TRADE

> 60+ WebSearch bulgusu, 6 paralel arastirma agent'i, tam Polymarket API dokumantasyonu ile desteklenmistir.
> Tarih: 2026-03-29
> Onceki: MASTER_PLAN_V2.md (2026-03-29)

---

## OZET: V2 → V3 FARKI

**V2 Sorunlari:**
- Trade frekansi dusuk (~50-150/gun hedefi)
- Polymarket mekanizmasi tam anlasilmadan plan yapildi
- Tek entry stratejisi (taker order)
- Binance scalping henuz yok
- 7 coin paralel trading yok

**V3 Hedefleri:**
- Saatte **560+ Polymarket trade** (7 coin x 5dk=420 + 7 coin x 15dk=140)
- Saatte **50-100 Binance scalp trade**
- **Maker-first order stratejisi** (fee = $0 + rebate)
- **Multi-strategy:** Momentum + Mean Reversion + Market Making
- **Regime-adaptive:** Volatilite ve trende gore strateji degistir
- **Correlation-clustered risk:** 7 coini cluster bazli yonet

---

## KISIM 1: POLYMARKET TAM MEKANIZMA

### 1.1 Market Lifecycle (Her 5dk Dongusu)

```
T=0:    Yeni market acilir (slug: btc-updown-5m-{unix_ts})
        ├── Gamma API'den condition_id ve clob_token_ids al
        ├── CLOB WebSocket'e subscribe ol (orderbook updates)
        └── Binance WebSocket'ten canli fiyat akisi baslat

T=0-240s: ANALIZ FAZISI
        ├── Binance fiyat momentum hesapla
        ├── RSI(7) + MACD + BB + EMA + Volume hesapla
        ├── Regime tespiti (LowVol/MedVol/HighVol)
        ├── Monte Carlo simulation (1000 path, kalan sure)
        └── Edge hesapla: model_prob vs market_implied_prob

T=240-270s: KARAR VE ENTRY (Son 30-60 saniye)
        ├── Edge > fee + min_edge_threshold?
        │   ├── EVET → Direction sec (UP veya DOWN)
        │   │   ├── Maker limit order ver (GTC, price = midpoint - 0.01)
        │   │   ├── Dolmazsa → FAK taker order (son 15s)
        │   │   └── Position size: Quarter Kelly
        │   └── HAYIR → Bu market'i ATLA
        └── Heartbeat gonder (her 5s)

T=300s: RESOLUTION
        ├── Chainlink oracle snapshot
        ├── End_price >= Start_price → UP wins
        ├── End_price < Start_price → DOWN wins
        └── Settlement: Kazanan token = $1.00, kaybeden = $0.00

T=300+: SONRAKI MARKET
        ├── PnL hesapla ve DB'ye yaz
        ├── Yeni market slug hesapla
        ├── Gamma API'den yeni market bilgisi al
        └── Dongu tekrar baslar
```

### 1.2 7 Coin Paralel Islem Akisi

```
Her 5dk pencerede (ornek T=0 → T=300):

PARALEL THREAD 1: BTC  ─┐
PARALEL THREAD 2: ETH  ─┤
PARALEL THREAD 3: SOL  ─┤ Hepsi ayni anda analiz + order
PARALEL THREAD 4: XRP  ─┤ Her biri bagimsiz market
PARALEL THREAD 5: DOGE ─┤ Her birinin kendi token_id'si var
PARALEL THREAD 6: BNB  ─┤
PARALEL THREAD 7: HYPE ─┘

5dk'da 7 trade = 84 trade/saat (5dk)
15dk'da 7 trade = 28 trade/saat (15dk)

Toplam: 84 + 28 = 112 trade/saat (KONSERVATIF, her market'e girilirse)

AGRESIF MOD (coin basina 5dk'da 5 market, scalp + main):
7 x 12/saat x ~5 entry = 420/saat (5dk)
7 x 4/saat x ~5 entry = 140/saat (15dk)
Toplam: 560/saat
```

**NOT:** 560/saat icin her coin'in her 5dk marketinde BIRDEN FAZLA entry yapilmali (scalp in + scalp out + main position). Bu agresif bir hedeftir. Gercekci baslangic: **112-200 trade/saat**.

### 1.3 Fee Optimizasyonu

```
STRATEJI 1: MAKER-FIRST (Tercih edilen)
  Order: GTC limit @ midpoint - $0.01 (veya best_bid + $0.01)
  Fee: $0.00 + maker rebate (%20 of taker fees)
  Bekleme: Max 30 saniye, dolmazsa FAK'a gec
  Avantaj: Trade basina ~$1.50-3.00 tasarruf

STRATEJI 2: PROBABILITY EXTREMES
  50/50 odds'ta fee: ~%3.12 (EN YUKSEK)
  80/20 odds'ta fee: ~%0.64
  90/10 odds'ta fee: ~%0.18
  → Guclu sinyal varsa (>%70 olasilik) fee cok dusuk!
  → Son 30 saniyede olasilik extrem'e kaydiginda gir

STRATEJI 3: POST-ONLY ORDERS
  GTC + post-only flag: Spread'i gecerse reject olur
  Maker statüsü garantili
  Sadece GTC ve GTD ile uyumlu

BEKLENEN FEE TASARRUFU:
  Taker: 100 trade/gun x $2.00 ortalama = $200/gun fee
  Maker: 100 trade/gun x $0.00 + $40 rebate = +$40/gun
  FARK: $240/gun = $7,200/ay ek kar
```

### 1.4 Polymarket API Entegrasyon Plani

```
YENi SERVISLER:

1. PolymarketGammaService (Market Discovery)
   - GetActiveMarkets(coin, timeframe) → Market[]
   - GetMarketBySlug(slug) → Market
   - CalculateSlug(coin, timeframe, now) → string
   - ExtractTokenIds(market) → (yesTokenId, noTokenId)

2. PolymarketClobService (Trading)
   - GetOrderBook(tokenId) → OrderBook
   - GetMidpoint(tokenId) → decimal
   - GetBestBidAsk(tokenId) → (bid, ask)
   - PlaceLimitOrder(tokenId, side, price, size) → OrderResult
   - PlaceMarketOrder(tokenId, side, amount) → OrderResult
   - CancelOrder(orderId) → bool
   - CancelAllOrders() → CancelResult
   - SendHeartbeat() → HeartbeatResult

3. PolymarketAuthService (Authentication)
   - GenerateL1Signature(message) → EIP712Signature
   - GenerateL2Headers(method, path, body) → Headers
   - SignOrder(order) → SignedOrder
   - CreateApiCredentials() → (apiKey, secret, passphrase)

4. PolymarketWebSocketService (Real-Time)
   - SubscribeMarket(tokenIds) → IObservable<MarketEvent>
   - SubscribeUser(conditionIds) → IObservable<UserEvent>
   - SubscribeCryptoPrices(symbols) → IObservable<PriceEvent>
   - SendPing() → void (her 5 saniye)

5. PolymarketSettlementService (Pozisyon Yonetimi)
   - GetOpenPositions() → Position[]
   - RedeemWinningTokens(conditionId) → RedeemResult
   - GetTradeHistory(since) → Trade[]
```

---

## KISIM 2: SINYAL MOTORU V3

### 2.1 Multi-Indikator Skorlama (V2'den Devam, Iyilestirilmis)

```
INDIKATORLER VE AGIRLIKLAR:

| Indikator    | Parametre      | Agirlik | Rol                    |
|-------------|----------------|---------|------------------------|
| RSI(7)      | Period=7       | %20     | Kisa vadeli momentum   |
| MACD        | 3-10-16 (HIZLI)| %20     | Momentum yonu (5dk opt)|
| BB(20,2)    | Standart       | %15     | Mean reversion seviye  |
| EMA(9/21)   | Cross          | %15     | Trend yonu             |
| Volume      | SMA(20)        | %10     | Dogrulama              |
| VWAP        | Intraday       | %10     | Seviye referans        |
| OB Imbalance| Bid/Ask ratio  | %10     | Microstructure         |

TOPLAM: %100
```

### 2.2 Regime-Adaptive Agirliklar

```
REGIME DETECTION:
  ADX(14) + BB Width + ATR(14) 5dk bazinda

  LowVol (ADX < 20, BB dar):
    → Mean Reversion agirligi %80
    → RSI(7) + BB agirlik artir
    → Daha dar SL/TP

  MedVol (ADX 20-30):
    → Hibrit %50/%50
    → Standart agirliklar

  HighVol (ADX > 30, BB genis):
    → Momentum agirligi %70
    → MACD + EMA agirlik artir
    → Daha genis SL/TP
    → Position size %50 kucult

TREND DETECTION (1sa EMA(26) slope):
  Bullish:  UP trade'lere oncelik, DOWN icin ekstra dogrulama
  Bearish:  DOWN trade'lere oncelik (DIKKATLI), UP icin ekstra dogrulama
  Neutral:  Sadece guclu sinyallere gir (edge > 0.12)
```

### 2.3 Polymarket Spesifik Sinyal

```
POLYMARKET ENTRY KARARI:

1. Model Probability Hesapla:
   model_prob = weighted_indicator_score (0.0-1.0)
   UP olasiligi = model_prob
   DOWN olasiligi = 1 - model_prob

2. Market Implied Probability Al:
   market_up = midpoint fiyati (CLOB API)
   market_down = 1 - market_up

3. Edge Hesapla:
   edge_up = model_prob - market_up
   edge_down = (1 - model_prob) - market_down

4. Fee Hesapla:
   fee = C * p * 0.072 * (p * (1-p))^1    // 30 Mart sonrasi formula
   fee_pct = fee / (C * p)

5. Net Edge:
   net_edge_up = edge_up - fee_pct
   net_edge_down = edge_down - fee_pct

6. Karar:
   IF net_edge_up > min_edge → BUY UP token
   IF net_edge_down > min_edge → BUY DOWN token
   ELSE → SKIP (bu market'e girme)

7. Position Size:
   Kelly: f = net_edge / ((1-p)/p)
   Actual: f * 0.25 (quarter kelly)
   Cap: max %3 bankroll
```

### 2.4 Binance Spesifik Sinyal

```
BINANCE SCALP ENTRY (Ayri motor):

LONG:
  1. RSI(7) < 30 (oversold)
  2. Fiyat <= BB alt band
  3. MACD histogram yukseliyor (son 2 bar)
  4. Volume > 1.2x ortalama
  5. 1sa trend: yukari veya yatay
  6. Order book: bid_volume / ask_volume > 1.3

SHORT:
  1. RSI(7) > 70 (overbought)
  2. Fiyat >= BB ust band
  3. MACD histogram dusuyor
  4. Volume > 1.2x ortalama
  5. 1sa trend: asagi veya yatay
  6. Order book: ask_volume / bid_volume > 1.3

EXIT:
  OTOCO bracket:
  TP: BB orta band (%60 pozisyon) + karsi BB (%40 pozisyon)
  SL: Entry'den %0.5 (STOP_LOSS_LIMIT)
  Time stop: 15dk hareketsizse kapat
```

---

## KISIM 3: TRADE MOTORU MIMARISI

### 3.1 Dual Engine Yapisi

```
                    ┌─────────────────────────┐
                    │    Signal Generator      │
                    │  (Shared Indicators)     │
                    └────────┬────────────────┘
                             │
              ┌──────────────┼──────────────┐
              │                              │
   ┌──────────▼──────────┐      ┌──────────▼──────────┐
   │   PolymarketEngine  │      │   BinanceScalpEngine │
   │                      │      │                      │
   │ - 7 coin x 5dk      │      │ - 7 coin x 5dk      │
   │ - 7 coin x 15dk     │      │ - OTOCO bracket      │
   │ - Maker-first order  │      │ - LIMIT_MAKER entry  │
   │ - Binary outcome     │      │ - Partial TP (60/40) │
   │ - Fee: dynamic taker │      │ - Fee: 0.075% maker  │
   │ - Settlement: auto   │      │ - Exit: SL/TP/Time   │
   └─────────────────────┘      └─────────────────────┘
```

### 3.2 PolymarketEngine Detaylari

```csharp
// Her 5dk (ve 15dk) dongusu icin:

class PolymarketTradeLoop
{
    // 7 coin paralel
    coins = [BTC, ETH, SOL, XRP, DOGE, BNB, HYPE]
    timeframes = [5m, 15m]

    async Task RunCycle(timeframe)
    {
        // 1. Mevcut market'leri bul
        foreach (coin in coins) // PARALEL
        {
            slug = CalculateSlug(coin, timeframe, now)
            market = await gamma.GetMarketBySlug(slug)
            tokenIds = market.ClobTokenIds // [UP, DOWN]

            // 2. Fiyat ve orderbook al
            midpoint = await clob.GetMidpoint(tokenIds.UP)
            bestBidAsk = await clob.GetBestBidAsk(tokenIds.UP)

            // 3. Sinyal hesapla
            signal = signalGenerator.Calculate(coin, timeframe)
            edge = signal.ModelProb - midpoint
            fee = CalculateFee(midpoint)
            netEdge = edge - fee

            // 4. Karar
            if (netEdge > config.MinEdge)
            {
                direction = signal.ModelProb > 0.5 ? UP : DOWN
                tokenId = direction == UP ? tokenIds.UP : tokenIds.DOWN
                price = bestBidAsk.Bid + 0.01 // maker
                size = CalculateKellySize(netEdge, midpoint)

                // 5. Order ver
                order = await clob.PlaceLimitOrder(tokenId, BUY, price, size)

                // 6. Dolmazsa taker'a gec (son 15s)
                if (!order.Filled && RemainingSeconds < 15)
                    await clob.PlaceMarketOrder(tokenId, BUY, size)
            }
        }
    }
}
```

### 3.3 BinanceScalpEngine Detaylari

```csharp
class BinanceScalpLoop
{
    coins = [BTCUSDT, ETHUSDT, SOLUSDT, XRPUSDT, DOGEUSDT, BNBUSDT]

    async Task OnCandleClose(coin, candle_5m)
    {
        // 1. Sinyal hesapla
        signal = signalGenerator.CalculateBinance(coin)

        if (signal.HasEntry)
        {
            // 2. OTOCO bracket order
            entry = signal.EntryPrice
            tp1 = signal.Direction == LONG ? entry * 1.003 : entry * 0.997
            tp2 = signal.Direction == LONG ? entry * 1.005 : entry * 0.995
            sl = signal.Direction == LONG ? entry * 0.995 : entry * 1.005

            // 3. OTOCO: Entry → OCO (TP + SL)
            await binance.PlaceOTOCO(
                symbol: coin,
                workingSide: signal.Direction == LONG ? BUY : SELL,
                workingPrice: entry,
                quantity: CalculateSize(coin, signal),
                pendingTP: tp1,   // %60 quantity
                pendingSL: sl,
                pendingTF: GTC
            )

            // 4. Time stop: 15dk sonra hala aciksa kapat
            scheduler.After(15min, () => CloseIfStillOpen(coin))
        }
    }
}
```

### 3.4 Correlation-Clustered Risk Yonetimi

```
CLUSTER TANIMI:
  Cluster A (BTC-correlated): BTC, ETH, SOL
    → Max cluster exposure: bankroll'un %15'i
    → Korelasyon > 0.9 ise: sadece 1 coin trade et

  Cluster B (Alt-correlated): XRP, BNB, DOGE
    → Max cluster exposure: bankroll'un %10'i
    → Daha kucuk position size (dusuk likidite)

  Cluster C (Low-liquidity): HYPE
    → Max exposure: bankroll'un %3'u
    → Sadece cok guclu sinyallerde

TOPLAM MAX EXPOSURE: %30 (herhangi bir anda)

KORELASYON IZLEME:
  Her 4 saatte 30 gunluk korelasyon matrisi hesapla
  Korelasyon > 0.9: cluster exposure'i %50 azalt
  Korelasyon < 0.5: normal exposure
```

---

## KISIM 4: VERI ALTYAPISI

### 4.1 Multi-Stream WebSocket Mimarisi

```
BINANCE STREAMS (7 coin x 3 timeframe = 21 stream):

  STREAM SET 1: 5dk Mumlar (ANA SINYAL)
    btcusdt@kline_5m, ethusdt@kline_5m, solusdt@kline_5m,
    xrpusdt@kline_5m, dogeusdt@kline_5m, bnbusdt@kline_5m,
    hypesdt@kline_5m (veya HYPEUSDT varsa)
    Buffer: 500 mum rolling

  STREAM SET 2: 1dk Mumlar (ENTRY TIMING)
    btcusdt@kline_1m, ethusdt@kline_1m, ...
    Buffer: 120 mum rolling (son 2 saat)

  STREAM SET 3: 1sa Mumlar (TREND KONTEKST)
    btcusdt@kline_1h, ethusdt@kline_1h, ...
    Buffer: 48 mum rolling (son 2 gun)

  STREAM SET 4: Book Ticker (REAL-TIME BID/ASK)
    btcusdt@bookTicker, ethusdt@bookTicker, ...
    → En hizli stream, order book imbalance icin

POLYMARKET STREAMS:

  STREAM 5: CLOB Market WebSocket
    → Her aktif market'in orderbook updates
    → Dynamic subscribe/unsubscribe (market acilir/kapanir)

  STREAM 6: RTDS Crypto Prices
    → btcusdt, ethusdt, solusdt, xrpusdt, dogeusdt, bnbusdt
    → Polymarket'in gordugu fiyat (Chainlink ile karsilastirma)
```

### 4.2 Buffer Implementasyonu (V2'den Devam)

```
CandleBuffer<T> {
  MaxSize: int
  Add(candle): void — FIFO
  GetLast(n): Candle[]
  GetAll(): Candle[]
  IsWarmedUp: bool — Count >= MinRequired
}

SymbolCandleStore {
  Dictionary<(Symbol, TimeFrame), CandleBuffer>
  GetBuffer(symbol, tf): CandleBuffer
  IsReady(symbol): bool
}

BufferSizes:
  1m:  120 mum (2 saat)
  5m:  500 mum (~42 saat)
  1h:  48 mum (2 gun)
  15m: 200 mum (~50 saat)
```

### 4.3 Startup Backfill

```
1. REST ile gecmis veri cek (paralel):
   GET /api/v3/klines?symbol=BTCUSDT&interval=5m&limit=500
   GET /api/v3/klines?symbol=BTCUSDT&interval=1m&limit=120
   GET /api/v3/klines?symbol=BTCUSDT&interval=1h&limit=48
   ... (7 coin x 3 timeframe = 21 istek)

2. Buffer'lara yukle
3. WebSocket baglantilari ac
4. IsWarmedUp = true olunca trading baslat
5. WebSocket disconnect'te: otomatik reconnect + gap fill
```

---

## KISIM 5: DB SCHEMA DEGISIKLIKLERI

### 5.1 Trades Tablosu Guncellemeleri

```sql
ALTER TABLE Trades ADD
  -- Polymarket spesifik
  MarketSlug NVARCHAR(200) NULL,         -- btc-updown-5m-1753314000
  ConditionId NVARCHAR(200) NULL,        -- on-chain condition ID
  TokenId NVARCHAR(200) NULL,            -- CLOB token ID
  OrderType NVARCHAR(20) NULL,           -- GTC, GTD, FOK, FAK
  IsMaker BIT NULL DEFAULT 0,            -- maker mi taker mi?
  FeeAmount DECIMAL(18,8) NULL,          -- odenen fee
  RebateAmount DECIMAL(18,8) NULL,       -- alinan rebate

  -- Binance spesifik
  TP1Price DECIMAL(18,8) NULL,           -- ilk TP seviyesi
  TP2Price DECIMAL(18,8) NULL,           -- ikinci TP seviyesi
  StopLossPrice DECIMAL(18,8) NULL,      -- SL seviyesi
  TP1Hit BIT NULL DEFAULT 0,             -- TP1 tetiklendi mi?
  TP2Hit BIT NULL DEFAULT 0,             -- TP2 tetiklendi mi?
  TimeStopHit BIT NULL DEFAULT 0,        -- Zaman stopu mu?

  -- Ortak yeni alanlar
  ClusterId NVARCHAR(20) NULL,           -- A, B, C (korelasyon cluster)
  ModelProbability DECIMAL(8,6) NULL,    -- model tahmini
  MarketProbability DECIMAL(8,6) NULL,   -- market implied prob
  NetEdge DECIMAL(8,6) NULL,             -- net edge (fee sonrasi)
  EntryMethod NVARCHAR(20) NULL,         -- Maker, Taker, OTOCO
  IndicatorScores NVARCHAR(MAX) NULL     -- JSON: her indikator skoru
```

### 5.2 Yeni Tablolar

```sql
-- Korelasyon matrisi gecmisi
CREATE TABLE CorrelationSnapshots (
  Id UNIQUEIDENTIFIER PRIMARY KEY,
  CalculatedAt DATETIME2 NOT NULL,
  Symbol1 NVARCHAR(20) NOT NULL,
  Symbol2 NVARCHAR(20) NOT NULL,
  Correlation DECIMAL(8,6) NOT NULL,
  Period INT NOT NULL -- gun
)

-- Market regime gecmisi
CREATE TABLE RegimeHistory (
  Id UNIQUEIDENTIFIER PRIMARY KEY,
  Symbol NVARCHAR(20) NOT NULL,
  DetectedAt DATETIME2 NOT NULL,
  VolatilityRegime NVARCHAR(20) NOT NULL,  -- LowVol, MedVol, HighVol
  TrendRegime NVARCHAR(20) NOT NULL,        -- Bullish, Bearish, Neutral
  ADX DECIMAL(8,4) NULL,
  ATR DECIMAL(18,8) NULL,
  BBWidth DECIMAL(8,6) NULL
)

-- Polymarket market takibi
CREATE TABLE PolymarketMarkets (
  Id UNIQUEIDENTIFIER PRIMARY KEY,
  Slug NVARCHAR(200) NOT NULL,
  ConditionId NVARCHAR(200) NOT NULL,
  YesTokenId NVARCHAR(200) NOT NULL,
  NoTokenId NVARCHAR(200) NOT NULL,
  Symbol NVARCHAR(20) NOT NULL,
  TimeFrame NVARCHAR(10) NOT NULL,
  StartsAt DATETIME2 NOT NULL,
  EndsAt DATETIME2 NOT NULL,
  Resolution NVARCHAR(10) NULL,  -- Up, Down, NULL (henuz resolve olmadi)
  StartPrice DECIMAL(18,8) NULL,
  EndPrice DECIMAL(18,8) NULL,
  Volume DECIMAL(18,2) NULL,
  CreatedAt DATETIME2 NOT NULL DEFAULT GETUTCDATE()
)
```

---

## KISIM 6: FAZ PLANI

### FAZ 1: VERI ALTYAPISI (P0 — ILKONCE)
**Sure:** ~2-3 gun | **Dosya:** ~8

| # | Is | Dosya | Bagimlilk |
|---|---|-------|-----------|
| 1.1 | TimeFrame enum: 1m, 5m, 15m, 1h ekle | Domain/ValueObjects/TimeFrame.cs | - |
| 1.2 | CandleBuffer + SymbolCandleStore | Infrastructure/Services/CandleBufferService.cs (YENI) | - |
| 1.3 | ICandleBuffer interface | Application/Interfaces/ICandleBuffer.cs (YENI) | 1.2 |
| 1.4 | Multi-stream WebSocket (1m+5m+15m+1h+bookTicker) | Binance/Services/BinanceWebSocketService.cs | 1.1 |
| 1.5 | REST backfill (startup) | Binance/Services/BinanceRestService.cs | 1.2 |
| 1.6 | DataIngestionService (startup + WS) | Worker/BackgroundServices/DataIngestionService.cs | 1.4, 1.5 |
| 1.7 | 7 coin destegi (DOGE, HYPE ekle) | Config/appsettings.json + Symbol enum | 1.1 |
| 1.8 | DB migration (schema degisiklikleri) | Infrastructure/Persistence/Migrations/ | - |

**Kabul Kriteri:**
- [ ] 7 coin x 4 timeframe (1m, 5m, 15m, 1h) stream aktif
- [ ] 5dk buffer 500 muma ulasinca IsWarmedUp = true
- [ ] Startup backfill <10sn icinde tamamlaniyor
- [ ] WebSocket disconnect'te otomatik reconnect + gap fill

### FAZ 2: SINYAL MOTORU V3 (P0 — FAZ 1 ILE PARALEL)
**Sure:** ~2 gun | **Dosya:** ~6

| # | Is | Dosya | Bagimlilk |
|---|---|-------|-----------|
| 2.1 | RSI(7), MACD(3-10-16), VWAP hesaplama | Infrastructure/Services/IndicatorCalculator.cs | Faz 1 |
| 2.2 | Order book imbalance hesaplama | Infrastructure/Services/OrderBookAnalyzer.cs (YENI) | 1.4 |
| 2.3 | Multi-indikator skorlama sistemi | Application/Services/SignalGenerator.cs | 2.1, 2.2 |
| 2.4 | SignalScore modeli | Application/Models/SignalScore.cs (YENI) | - |
| 2.5 | Regime detector (ADX+BB+ATR) | Application/Services/RegimeDetector.cs | 2.1 |
| 2.6 | Regime-adaptive agirlik sistemi | Application/Services/SignalGenerator.cs | 2.5 |

**Kabul Kriteri:**
- [ ] 7 indikator birlikte hesaplaniyor
- [ ] Regime detection calisiyor (3 rejim)
- [ ] Agirliklar rejime gore dinamik degisiyor
- [ ] Final skor 0.0-1.0 arasi

### FAZ 3: POLYMARKET ENGINE V2 (P0 — EN KRITIK)
**Sure:** ~3-4 gun | **Dosya:** ~10

| # | Is | Dosya | Bagimlilk |
|---|---|-------|-----------|
| 3.1 | PolymarketGammaService (market discovery) | Polymarket/Services/PolymarketGammaService.cs (YENI) | - |
| 3.2 | PolymarketAuthService (L1+L2 auth) | Polymarket/Services/PolymarketAuthService.cs (YENI) | - |
| 3.3 | PolymarketClobService (order verme) | Polymarket/Services/PolymarketClobService.cs (YENI) | 3.2 |
| 3.4 | PolymarketWebSocketService (real-time) | Polymarket/Services/PolymarketWebSocketService.cs (YENI) | - |
| 3.5 | Maker-first order stratejisi | Application/Services/PolymarketOrderStrategy.cs (YENI) | 3.3 |
| 3.6 | Fee hesaplama (yeni formula) | Application/Services/FeeCalculator.cs (YENI) | - |
| 3.7 | 7 coin paralel trade loop | Application/Engines/PolymarketEngine.cs | 3.1-3.6, Faz 2 |
| 3.8 | Settlement + redeem | Polymarket/Services/PolymarketSettlementService.cs (YENI) | 3.2 |
| 3.9 | Heartbeat service (5s interval) | Polymarket/Services/PolymarketHeartbeatService.cs (YENI) | 3.2 |
| 3.10 | Nethereum EIP-712 entegrasyonu | Polymarket/Crypto/Eip712Signer.cs (YENI) | - |

**Kabul Kriteri:**
- [ ] Market discovery calisiyor (slug → market → tokenIds)
- [ ] L1+L2 auth calisiyor
- [ ] Maker limit order verebiliyor
- [ ] 7 coin paralel trade ediyor
- [ ] Heartbeat her 5s gonderiliyor
- [ ] Settlement/redeem calisiyor
- [ ] Fee dogru hesaplaniyor (yeni formula)

### FAZ 4: BINANCE SCALP ENGINE (P1)
**Sure:** ~2 gun | **Dosya:** ~5

| # | Is | Dosya | Bagimlilk |
|---|---|-------|-----------|
| 4.1 | BinanceOrderService (OTOCO bracket) | Binance/Services/BinanceOrderService.cs | - |
| 4.2 | BinanceScalpEngine | Application/Engines/BinanceScalpEngine.cs (YENI) | 4.1, Faz 2 |
| 4.3 | Partial TP (60/40) yonetimi | Application/Services/PartialExitManager.cs (YENI) | 4.1 |
| 4.4 | Time stop (15dk) | Application/Services/TimeStopService.cs (YENI) | 4.2 |
| 4.5 | User Data Stream listener | Binance/Services/BinanceUserStreamService.cs (YENI) | - |

**Kabul Kriteri:**
- [ ] OTOCO bracket order verebiliyor
- [ ] Partial TP (%60 TP1, %40 TP2) calisiyor
- [ ] Time stop 15dk sonra otomatik kapatma
- [ ] User Data Stream ile fill bildirimi

### FAZ 5: RISK YONETIMI + KORELASYON (P1)
**Sure:** ~1-2 gun | **Dosya:** ~4

| # | Is | Dosya | Bagimlilk |
|---|---|-------|-----------|
| 5.1 | Korelasyon matrisi hesaplama (30g rolling) | Application/Services/CorrelationService.cs (YENI) | Faz 1 |
| 5.2 | Cluster bazli exposure limitleri | Application/Services/RiskManager.cs | 5.1 |
| 5.3 | Position sizer (Quarter Kelly + cluster) | Application/Services/PositionSizer.cs | 5.1, 5.2 |
| 5.4 | Circuit breakers (saatlik/gunluk PnL) | Application/Services/CircuitBreaker.cs (YENI) | - |

**Kabul Kriteri:**
- [ ] Korelasyon matrisi her 4 saatte guncelleniyor
- [ ] Cluster exposure limitleri uygulanıyor
- [ ] Korelasyon > 0.9'da cluster exposure %50 azaliyor
- [ ] Gunluk %5 zarar → tum trading durur

### FAZ 6: IZLEME + DASHBOARD (P2)
**Sure:** ~1 gun | **Dosya:** ~3

| # | Is | Dosya |
|---|---|-------|
| 6.1 | Yeni metrikler (maker %, fee tasarrufu, cluster exposure) | Admin UI |
| 6.2 | Polymarket spesifik dashboard (market lifecycle, settlement) | Admin UI |
| 6.3 | Alarm sistemi (PnL, WR, drawdown) | Worker/BackgroundServices/ |

---

## KISIM 7: KONFIGÜRASYON

### 7.1 Onerilen V3 Config

```json
{
  "configId": "analyst-v3-master-plan",
  "polymarket": {
    "enabled": true,
    "gammaApiUrl": "https://gamma-api.polymarket.com",
    "clobApiUrl": "https://clob.polymarket.com",
    "wsMarketUrl": "wss://ws-subscriptions-clob.polymarket.com/ws/market",
    "wsRtdsUrl": "wss://ws-live-data.polymarket.com",
    "enabledCoins": ["BTC", "ETH", "SOL", "XRP", "DOGE", "BNB", "HYPE"],
    "enabledTimeframes": ["5m", "15m"],
    "orderStrategy": "maker-first",
    "makerFallbackToTakerSeconds": 15,
    "heartbeatIntervalMs": 5000,
    "minEdge": 0.05,
    "minEdgeLowVol": 0.07,
    "minEdgeHighVol": 0.04,
    "minVolume": 1000,
    "maxSpread": 0.10,
    "skipCoinsWithWideSpread": ["HYPE"],
    "feeParams": {
      "feeRate": 0.072,
      "exponent": 1,
      "peakRate": 0.018
    }
  },
  "binanceScalping": {
    "enabled": true,
    "enabledSymbols": ["BTCUSDT", "ETHUSDT", "SOLUSDT", "XRPUSDT", "DOGEUSDT", "BNBUSDT"],
    "strategy": "RegimeAdaptive",
    "riskPerTrade": 0.002,
    "maxOpenPositions": 3,
    "stopLossPercent": 0.5,
    "takeProfitPercent": 0.8,
    "tp1Ratio": 0.60,
    "tp2Ratio": 0.40,
    "timeStopMinutes": 15,
    "minVolumeFactor": 1.2,
    "useLimitMaker": true,
    "useBnbForFees": true,
    "useOTOCO": true
  },
  "signalEngine": {
    "indicators": {
      "rsi": {"period": 7, "weight": 0.20},
      "macd": {"fast": 3, "slow": 10, "signal": 16, "weight": 0.20},
      "bollingerBands": {"period": 20, "stdDev": 2.0, "weight": 0.15},
      "ema": {"fast": 9, "slow": 21, "weight": 0.15},
      "volume": {"period": 20, "weight": 0.10},
      "vwap": {"weight": 0.10},
      "orderBookImbalance": {"weight": 0.10}
    },
    "regimeDetection": {
      "adxPeriod": 14,
      "lowVolThreshold": 20,
      "highVolThreshold": 30,
      "trendLookback": 3
    },
    "signalThresholdUp": 0.58,
    "signalThresholdDown": 0.42
  },
  "riskManagement": {
    "kellyMultiplier": 0.25,
    "maxPositionFraction": 0.03,
    "maxExposureFraction": 0.30,
    "clusters": {
      "A": {"coins": ["BTC", "ETH", "SOL"], "maxExposure": 0.15},
      "B": {"coins": ["XRP", "BNB", "DOGE"], "maxExposure": 0.10},
      "C": {"coins": ["HYPE"], "maxExposure": 0.03}
    },
    "correlationThreshold": 0.9,
    "correlationRecalcHours": 4,
    "circuitBreakers": {
      "hourlyLossLimit": 0.01,
      "dailyLossLimit": 0.05,
      "maxConsecutiveLosses": 15,
      "winRateThreshold": 0.45,
      "winRateWindow": 50
    },
    "bankroll": 10000
  },
  "dataIngestion": {
    "bufferSizes": {
      "1m": 120,
      "5m": 500,
      "15m": 200,
      "1h": 48
    },
    "backfillOnStartup": true,
    "reconnectDelayMs": 5000,
    "maxReconnectAttempts": 10
  }
}
```

---

## KISIM 8: KARLILIK PROJEKSIYONU

### 8.1 Polymarket Senaryolari

```
SENARYO A: Konservatif (112 trade/saat, %54 WR, maker orders)
  Trade basina: $50 ortalama
  Fee: ~$0 (maker) + $20/gun rebate
  EV/trade: $50 x 0.034 = $1.70
  Saatlik: 112 x $1.70 = $190
  Gunluk (24h): $4,560
  Aylik: $136,800 (TEORiK — gercekte %50-70'i)
  GERCEKCI AYLIK: $68,000-$96,000

SENARYO B: Orta (200 trade/saat, %55 WR, mixed maker/taker)
  Fee: ~%1 ortalama (mixed)
  EV/trade: $50 x 0.034 = $1.70
  Saatlik: 200 x $1.70 = $340
  Gunluk: $8,160
  GERCEKCI AYLIK (x0.5): $122,400

SENARYO C: Agresif (400 trade/saat, %53 WR, mostly taker)
  Fee: ~%2 ortalama
  EV/trade: $50 x 0.014 = $0.70
  Saatlik: 400 x $0.70 = $280
  Gunluk: $6,720
  GERCEKCI AYLIK (x0.5): $100,800

NOT: Bu rakamlar TEORiK. Gercekte:
  - Model %55 WR tutturamayabilir
  - Likidite yetersiz olabilir
  - Slippage ve latency EV dusurur
  - Ilk ay hedef: gunluk +$100-500 (aylik +$3,000-15,000)
```

### 8.2 Binance Senaryolari

```
SENARYO: 50 scalp/gun, %60 WR, 1:1.5 R:R
  Ortalama kar/trade: $15 (TP) vs $10 (SL)
  EV/trade: 0.60 x $15 - 0.40 x $10 - $0.15 fee = $4.85
  Gunluk: 50 x $4.85 = $242.50
  Aylik: $7,275

GERCEKCI ILK AY: $100-200/gun → $3,000-6,000/ay
```

---

## KISIM 9: BASARI KRITERLERI

| Kriter | V3 Hedef | Olcum Periyodu |
|--------|----------|----------------|
| Polymarket PnL | POZITIF | 500+ trade sonrasi |
| Polymarket Win Rate | > %54 | Rolling 200 trade |
| Polymarket Maker Orani | > %60 | Gunluk |
| Binance PnL | POZITIF | 200+ trade sonrasi |
| Binance Win Rate | > %58 | Rolling 100 trade |
| Trade Frekansi (Poly) | > 100/saat | Saatlik |
| Trade Frekansi (Binance) | > 30/gun | Gunluk |
| Sharpe Ratio | > 1.0 | Rolling 7 gun |
| Profit Factor | > 1.3 | Tum trade'ler |
| Max Drawdown | < %10 | Herhangi bir an |
| Fee Tasarrufu (maker) | > $100/gun | Gunluk |
| Korelasyon Cluster Compliance | %100 | Her trade |

---

## KISIM 10: KRITIK RISKLER VE MITIGASYON

| Risk | Olasilik | Etki | Mitigasyon |
|------|----------|------|------------|
| Polymarket fee artisi (30 Mart) | %100 | Yuksek | Maker-first strateji, edge threshold artir |
| Model WR < %51.6 (break-even) | Orta | Kritik | Circuit breaker, otomatik durdurma |
| API rate limit asilmasi | Dusuk | Orta | Rate limiter, batch operations |
| Polymarket kural degisikligi | Orta | Yuksek | Esnek mimari, config-driven |
| Korelasyon spike (kriz) | Orta | Yuksek | Cluster limitleri, exposure azaltma |
| Chainlink oracle gecikmesi | Dusuk | Orta | Binance fiyati referans, oracle lag takibi |
| WebSocket disconnect | Orta | Orta | Otomatik reconnect, gap fill, heartbeat |
| .NET icin Polymarket SDK yok | %100 | Orta | Manuel implement (HTTP + Nethereum) |
| Yetersiz likidite (DOGE, HYPE) | Yuksek | Dusuk | Skip wide-spread markets, min volume filtre |
| Binance OTOCO desteklenmemesi | Dusuk | Dusuk | Fallback: ayri SL + TP orderlari |

---

## UYGULAMA ONCELIK SIRASI

```
HAFTA 1:
  FAZ 1 (Veri Altyapisi) + FAZ 2 (Sinyal Motoru) → PARALEL

HAFTA 2:
  FAZ 3 (Polymarket Engine V2) → EN KRITIK

HAFTA 3:
  FAZ 4 (Binance Scalp Engine) + FAZ 5 (Risk Yonetimi) → PARALEL

HAFTA 4:
  FAZ 6 (Dashboard) + Fine-tuning + A/B test

ILKONCE BASLAT:
  1. FAZ 1 — Veri olmadan hicbir sey calismaz
  2. FAZ 3.1-3.4 — Polymarket API entegrasyonu (en cok is burada)
  3. FAZ 2 — Sinyal motoru (Faz 1 verilerine bagimli)
  4. FAZ 3.5-3.10 — Trading logic
  5. FAZ 4+5 — Binance + Risk
```

---

> Bu plan 60+ WebSearch bulgusu, Polymarket resmi dokumantasyonu, Reddit/Medium topluluk deneyimleri,
> akademik arastirmalar ve Binance API referanslari ile desteklenmistir.
> Her faz tamamlandiginda Analyst performansi olcecek ve stratejileri iteratif olarak iyilestirecektir.
> 30 Mart 2026 fee degisikligi dikkate alinmistir.
