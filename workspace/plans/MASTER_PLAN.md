# CryptoTrader Master Plan — Traxon

> Araştırma tamamlandı. Bu doküman tüm bulguların sentezini ve uygulama planını içerir.
> Tarih: 2026-03-27

---

## 1. ARAŞTIRMA SENTEZI — KRİTİK BULGULAR

### 1.1 Polymarket Gerçekleri
- **%92.4 cüzdan zarar ediyor** — sadece %7.6 kârlı
- Top 20 kârlı trader'ın **14'ü bot**
- 2026 itibariyle **latency arbitraj öldü** — dinamik taker fee ile edge yeniliyor
- **Maker = %0 fee + günlük USDC rebate** — tek sürdürülebilir avantaj
- Crypto kategorisi taker fee: `fee = 0.072 × p × (1-p) × C` (exponent=1)
- Crypto maker rebate: taker fee havuzunun **%20'si** günlük dağıtılır
- **Heartbeat zorunlu**: 5sn'de bir `/heartbeats` gönderilmezse tüm orderlar cancel
- Resmi C#/.NET SDK yok — kendimiz yazacağız (Nethereum + HttpClient + WebSocket)
- 5dk marketlerde max fee %0.44, 15dk'da ~%1.8 (p=0.50'de)

### 1.2 Binance Gerçekleri
- Public market data için **API key gerekmez**
- `Binance.Net` (JKorf) NuGet paketi — v12.11.1, mükemmel .NET desteği
- Tek WebSocket bağlantısı ile 7 symbol × 2 interval + 7 miniTicker = **21 stream** (limit 1024)
- REST ile startup'ta 200 candle çek (14 istek, 28 weight — limit 6000/dk)
- Kline stream **2sn'de bir** güncellenir, `x: true` = candle kapandı

### 1.3 Strateji Gerçekleri
- **Sadece tahmin ile para kazanılmaz** — doküman bunu kanıtlıyor
- 5-15dk arası **tek kârlı zaman aralığı** (Run 3 verisi: 24 trade, %46 win, +$7.78)
- Giriş fiyatı $0.30-$0.50 arası **çok daha güvenli** ($0.40+ %100 win rate)
- Fair value: `P(Up) = Φ(μ√T / σ)` — basit ama etkili
- Kelly: `f* = (fair_value - market_price) / (1 - market_price)` — Half Kelly kullan
- Parkinson volatilite estimatörü close-to-close'dan **5x daha verimli**
- **200+ trade** lazım istatistiksel anlamlılık için, 400+ ideal

---

## 2. SİSTEM MİMARİSİ

### 2.1 Solution Yapısı (Onion Architecture + Clean Architecture)

```
Traxon.CryptoTrader.sln
│
├── src/
│   ├── Core/
│   │   └── Traxon.CryptoTrader.Domain/              # Entity, Value Object, Domain Event
│   │
│   ├── Application/
│   │   └── Traxon.CryptoTrader.Application/          # CQRS Handlers, Interfaces, DTOs
│   │
│   ├── Infrastructure/
│   │   ├── Traxon.CryptoTrader.Infrastructure/        # EF Core, SQL, Generic infra
│   │   ├── Traxon.CryptoTrader.Binance/               # Binance REST + WebSocket adapter
│   │   └── Traxon.CryptoTrader.Polymarket/            # Polymarket CLOB + WebSocket adapter
│   │
│   └── Presentation/
│       ├── Traxon.CryptoTrader.Worker/                # Background service (ana motor)
│       ├── Traxon.CryptoTrader.Dashboard/             # Blazor Server — Trading Dashboard (UI #1)
│       └── Traxon.CryptoTrader.Admin/                 # Blazor Server — Admin & Analytics (UI #2)
│
├── tests/
│   ├── Traxon.CryptoTrader.Domain.Tests/
│   ├── Traxon.CryptoTrader.Application.Tests/
│   ├── Traxon.CryptoTrader.Binance.Tests/
│   ├── Traxon.CryptoTrader.Polymarket.Tests/
│   └── Traxon.CryptoTrader.Integration.Tests/
│
├── Directory.Build.props
├── Directory.Packages.props
└── global.json
```

### 2.1.1 İki UI Projesi — Aynı Veritabanı, Farklı Amaç

Her iki Blazor Server projesi aynı `Infrastructure` katmanını (DbContext, Repository) kullanır.
`IDbContextFactory<AppDbContext>` pattern ile Blazor Server circuit güvenliği sağlanır.

| Proje | Amaç | Hedef Kullanıcı | Port |
|-------|------|-----------------|------|
| **Dashboard** | Gerçek zamanlı trading ekranı | Trader | :5001 |
| **Admin** | Analitik, feedback loop, sistem yönetimi | Geliştirici/Analist | :5002 |

### 2.2 Katman Sorumlulukları

**Domain (Core — sıfır dış bağımlılık)**
- `Asset` value object (BTC, ETH, SOL, XRP, DOGE, HYPE, BNB)
- `TimeFrame` value object (FiveMinute, FifteenMinute)
- `Candle` entity (OHLCV + timestamp)
- `Signal` value object (Direction, Confidence, FairValue, Mu, Sigma)
- `Trade` entity (entry, exit, PnL, outcome, reason)
- `Position` entity (asset, direction, size, entryPrice)
- `Portfolio` aggregate (balance, positions, equity curve)
- `TechnicalIndicators` value objects (RSI, MACD, BollingerBands, ATR, VWAP)
- Domain Events: `SignalGenerated`, `TradeOpened`, `TradeClosed`, `CandleReceived`

**Application (MediatR + FluentValidation)**
- `IMarketDataProvider` interface — Binance adapter implement eder
- `ITradingEngine` interface — 4 engine implement eder
- `ISignalGenerator` interface — karar motoru
- `IIndicatorCalculator` interface — teknik analiz
- `IFairValueCalculator` interface — binary opsiyon fair value
- `IPositionSizer` interface — Kelly Criterion
- `ITradeLogger` interface — feedback loop için loglama
- Commands: `OpenTrade`, `CloseTrade`, `UpdatePortfolio`
- Queries: `GetSignal`, `GetPortfolioStatus`, `GetTradeHistory`

**Infrastructure**
- `BinanceMarketDataProvider` — Binance.Net ile REST + WebSocket
- `PolymarketClient` — Custom HTTP + WebSocket + EIP-712 signing
- `SqlTradeRepository` — EF Core ile trade/candle persistence
- `InMemoryCandleBuffer` — Rolling window (ConcurrentDictionary)

**Presentation**
- `Worker` — BackgroundService, ana orchestration loop
- `Dashboard` — Blazor Server, trading dashboard (gerçek zamanlı fiyat, chart, sinyal, trade)
- `Admin` — Blazor Server, admin & analytics panel (feedback loop, performans, konfigürasyon)

### 2.3 Dört Motor Tasarımı

```
                    ┌─────────────────────────────┐
                    │      IMarketDataProvider      │
                    │    (Binance WebSocket/REST)   │
                    └──────────┬──────────────────┘
                               │ Candle, Tick, Volume
                               ▼
                    ┌─────────────────────────────┐
                    │      ISignalGenerator        │
                    │  (Fair Value + Indicators)    │
                    └──────────┬──────────────────┘
                               │ Signal (Direction, Confidence, FairValue)
                               ▼
              ┌────────────────┼────────────────┐
              │                │                │
     ┌────────▼──────┐ ┌──────▼───────┐ ┌──────▼───────┐
     │ ITradingEngine │ │ITradingEngine│ │ITradingEngine│  ...
     │ PaperPoly      │ │ PaperBinance │ │  RealPoly    │
     └────────┬──────┘ └──────┬───────┘ └──────┬───────┘
              │                │                │
              ▼                ▼                ▼
        ┌──────────┐   ┌──────────┐     ┌──────────┐
        │ITradeLog │   │ITradeLog │     │ITradeLog │
        └──────────┘   └──────────┘     └──────────┘
```

| # | Motor | Açıklama | Trade Nasıl Yapılır |
|---|-------|----------|---------------------|
| 1 | **PaperPolymarketEngine** | Polymarket simülasyonu | Sanal bakiye, YES/NO pozisyon, 5/15dk sonra resolve |
| 2 | **PaperBinanceEngine** | Binance spot simülasyonu | Sanal bakiye, al/sat, SL/TP simülasyonu |
| 3 | **PolymarketEngine** | Gerçek Polymarket | CLOB API ile limit/market order |
| 4 | **BinanceEngine** | Gerçek Binance | Spot API ile al/sat |

**Her motor `ITradingEngine` interface'ini implement eder:**
```
ITradingEngine
├── OpenPositionAsync(Signal signal) → Result<Trade>
├── ClosePositionAsync(Trade trade) → Result<TradeResult>
├── GetOpenPositionsAsync() → Result<IReadOnlyList<Position>>
├── GetPortfolioAsync() → Result<Portfolio>
└── IsReadyAsync() → Result<bool>
```

---

## 3. KARAR MOTORU DETAYLARı

### 3.1 Signal Generation Pipeline

```
[Binance Candle Stream]
        │
        ▼
[Rolling Buffer (200 candle)]
        │
        ▼
[Indicator Calculation]
├── RSI(14)
├── MACD(12, 26, 9)
├── Bollinger Bands(20, 2)
├── ATR(14)
├── VWAP(20)
├── SMA(10) / SMA(30) crossover
└── Stochastic(14, 3, 3)
        │
        ▼
[Fair Value Calculation]
├── μ (momentum) = EMA(log_returns, 12)
├── σ (volatility) = Parkinson estimator(14 candle)
├── T = time to resolution (saniye)
├── d2 = (ln(S/K) + (μ - σ²/2) * T) / (σ * √T)
└── P(Up) = Φ(d2)
        │
        ▼
[Multi-Confirmation Filter]
├── MACD histogram > 0?
├── RSI > 50? (veya < 50 for SHORT)
├── Price > VWAP?
├── Fast SMA > Slow SMA?
├── Stochastic %K > %D?
└── En az 3/5 uyuşma gerekli
        │
        ▼
[Regime Detection]
├── vol_short = σ(last 12 candles)
├── vol_long = σ(last 144 candles)
├── regime = HIGH_VOL if vol_short > 1.5 × vol_long
└── ATR expansion/contraction
        │
        ▼
[Position Sizing — Half Kelly]
├── edge = |fair_value - market_price|
├── min_edge = 0.03 (3 cent) → altındaysa trade ETME
├── f* = (fair_value - market_price) / (1 - market_price)
├── bet_size = (f* / 2) × bankroll
└── max_position = %5 bankroll
        │
        ▼
[Signal Output]
{
  asset: "BTCUSDT",
  timeframe: "5m",
  direction: "UP",         // veya "DOWN"
  confidence: 0.62,        // fair value
  entry_price: 0.55,       // market fiyatı
  edge: 0.07,              // 7 cent edge
  kelly_fraction: 0.078,   // half kelly
  position_size: 78,       // $78 (on $10,000 bankroll)
  indicators: { rsi: 58, macd_hist: 0.003, ... },
  regime: "LOW_VOL"
}
```

### 3.2 Giriş Kuralları

| Kural | Değer | Gerekçe |
|-------|-------|---------|
| Minimum edge | $0.03 (3 cent) | Spread + execution cost'u karşılamalı |
| Giriş fiyat aralığı | $0.30 - $0.60 | Araştırma: $0.20 altı %73 kayıp, $0.40+ %100 kazanç |
| Minimum confidence | 3/5 indicator uyuşması | Multi-confirmation = daha az false signal |
| Max pozisyon | Bankroll'un %5'i | Risk yönetimi |
| Max toplam exposure | Bankroll'un %25'i | Aynı anda max 5 pozisyon |
| Resolution'a min süre | 5 dakika | 0-5dk arası %0 win rate (araştırma verisi) |

### 3.3 Çıkış Kuralları

**Polymarket:**
- Resolution'a kadar tut (binary market — ya $1 ya $0)
- SL/TP anlamsız (doküman teyit ediyor)
- MaxHold: resolution süresine kadar

**Binance (Paper/Real):**
- Stop Loss: entry - (1.5 × ATR)
- Take Profit: entry + (2 × ATR) → 1.33:1 risk-reward minimum
- Trailing stop: ATR bazlı

---

## 4. VERİ STRATEJİSİ KARARI

### Hibrit Yaklaşım (Araştırma Sonucu Karar)

| Veri | Nerede | Neden |
|------|--------|-------|
| Son 200 candle (her symbol/interval) | **In-Memory** (ConcurrentDictionary) | Real-time signal hesaplama, <1ms latency |
| Kapanan candlelar | **SQL Server** (async yazma) | Backtesting, audit, feedback loop analizi |
| Trade logları | **SQL Server** | Feedback loop, performans analizi |
| Indicator değerleri | **In-Memory** (hesaplanır) | Her candle'da yeniden hesaplanır |

### Startup Akışı
1. REST: `GET /api/v3/klines?symbol=X&interval=5m&limit=200` × 7 symbol × 2 interval = 14 istek
2. Toplam weight: 28 (limit 6000/dk — ihmal edilebilir)
3. Buffer'ı doldur, indicator warm-up yap
4. WebSocket bağlantısı aç, real-time akışa geç

### SQL Schema (Temel Tablolar)

```sql
-- Candle verisi (backtesting + audit)
CREATE TABLE Candles (
    Id BIGINT IDENTITY PRIMARY KEY,
    Symbol NVARCHAR(20) NOT NULL,
    Interval NVARCHAR(5) NOT NULL,     -- '5m', '15m'
    OpenTime DATETIME2 NOT NULL,
    CloseTime DATETIME2 NOT NULL,
    Open DECIMAL(18,8) NOT NULL,
    High DECIMAL(18,8) NOT NULL,
    Low DECIMAL(18,8) NOT NULL,
    Close DECIMAL(18,8) NOT NULL,
    Volume DECIMAL(18,8) NOT NULL,
    QuoteVolume DECIMAL(18,8) NOT NULL,
    TradeCount INT NOT NULL,
    INDEX IX_Candles_Symbol_Interval_Time (Symbol, Interval, OpenTime)
);

-- Trade logları (feedback loop)
CREATE TABLE Trades (
    Id UNIQUEIDENTIFIER PRIMARY KEY,
    Engine NVARCHAR(20) NOT NULL,      -- 'PaperPoly', 'PaperBinance', 'Poly', 'Binance'
    Symbol NVARCHAR(20) NOT NULL,
    TimeFrame NVARCHAR(5) NOT NULL,
    Direction NVARCHAR(4) NOT NULL,    -- 'UP', 'DOWN'
    EntryPrice DECIMAL(18,8) NOT NULL,
    ExitPrice DECIMAL(18,8) NULL,
    FairValue DECIMAL(18,8) NOT NULL,
    Edge DECIMAL(18,8) NOT NULL,
    PositionSize DECIMAL(18,4) NOT NULL,
    KellyFraction DECIMAL(18,8) NOT NULL,
    MuEstimate DECIMAL(18,12) NOT NULL,
    SigmaEstimate DECIMAL(18,12) NOT NULL,
    Regime NVARCHAR(20) NOT NULL,
    IndicatorSnapshot NVARCHAR(MAX) NOT NULL,  -- JSON
    OpenedAt DATETIME2 NOT NULL,
    ClosedAt DATETIME2 NULL,
    Outcome NVARCHAR(10) NULL,         -- 'WIN', 'LOSS', NULL (open)
    PnL DECIMAL(18,4) NULL,
    Reason NVARCHAR(500) NOT NULL,     -- Neden bu trade açıldı
    INDEX IX_Trades_Engine_Symbol (Engine, Symbol, OpenedAt)
);

-- Portfolio durumu
CREATE TABLE PortfolioSnapshots (
    Id BIGINT IDENTITY PRIMARY KEY,
    Engine NVARCHAR(20) NOT NULL,
    Timestamp DATETIME2 NOT NULL,
    Balance DECIMAL(18,4) NOT NULL,
    OpenPositionCount INT NOT NULL,
    TotalExposure DECIMAL(18,4) NOT NULL,
    TotalPnL DECIMAL(18,4) NOT NULL,
    WinRate DECIMAL(5,2) NULL,
    TradeCount INT NOT NULL,
    INDEX IX_Portfolio_Engine_Time (Engine, Timestamp)
);
```

---

## 5. UI MİMARİSİ — BLAZOR SERVER + SIGNALR

### 5.1 Teknoloji Yığını

| Teknoloji | Amaç |
|-----------|------|
| **Blazor Server** | Ana UI framework — server-side rendering, SignalR üzerinden gerçek zamanlı |
| **SignalR** | Gerçek zamanlı veri push (fiyat, sinyal, trade güncellemeleri) — Blazor Server'a built-in |
| **Tailwind CSS v4** | Styling — standalone CLI, Node.js gerekmez, TrinkUI tasarım dili |
| **LightweightCharts.Blazor** | TradingView tarzı candlestick + volume chart |
| **Blazor-ApexCharts** | PnL, equity curve, pie chart, bar chart |
| **Inter + JetBrains Mono** | Font — Inter genel metin, JetBrains Mono sayılar/fiyatlar |

### 5.2 Tasarım Dili — TrinkUI Inspired Dark Theme

TrinkUI'ın tasarım prensiplerine uygun özel dark theme:

```css
/* Tailwind CSS v4 — input.css */
@import "tailwindcss";

@theme {
  /* Backgrounds */
  --color-bg-primary: #0a0a0a;         /* Ana arka plan — neredeyse siyah */
  --color-bg-surface: #111111;          /* Kart yüzeyleri */
  --color-bg-surface-hover: #1a1a1a;    /* Hover durumu */
  --color-bg-elevated: #161616;         /* Yükseltilmiş yüzey (sidebar, modal) */

  /* Borders */
  --color-border-default: #1f1f1f;      /* Varsayılan border */
  --color-border-subtle: #161616;       /* İnce border */

  /* Text */
  --color-text-primary: #e0e0e0;        /* Ana metin */
  --color-text-secondary: #888888;      /* İkincil metin */
  --color-text-muted: #6b7280;          /* Soluk metin */

  /* Trading Colors */
  --color-accent-green: #00d4aa;        /* Yükseliş / Buy / Profit */
  --color-accent-red: #ff4757;          /* Düşüş / Sell / Loss */
  --color-accent-gold: #ffd700;         /* Highlight / Warning */
  --color-accent-blue: #3b82f6;         /* Link / Info */
  --color-accent-purple: #a855f7;       /* Signal / Special */

  /* Spacing & Radius */
  --radius-card: 10px;
  --radius-button: 8px;
  --radius-chip: 6px;
}
```

**Kart Stili (TrinkUI pattern):**
- Semi-transparent surface: `bg-[var(--color-bg-surface)]/50`
- İnce border: `border border-[var(--color-border-default)]`
- Hafif backdrop-blur (opsiyonel): `backdrop-blur-sm`
- Generous padding: `p-4` veya `p-6`
- Rounded corners: `rounded-[10px]`

### 5.3 Dashboard #1 — Trading Dashboard (`:5001`)

**Hedef:** Trader'ın gerçek zamanlı piyasa verisi, sinyaller ve pozisyonları takip ettiği ekran.

```
┌──────────────────────────────────────────────────────────────┐
│ [Logo] Trading Dashboard    [BTC ▲$87,234 +1.2%] [ETH ▼$3,..│  ← Ticker Strip
├──────────┬───────────────────────────────────┬───────────────┤
│          │                                   │               │
│ Sidebar  │   Candlestick Chart               │  Order Book   │
│          │   (LightweightCharts.Blazor)       │  Bid/Ask      │
│ • Market │   [5m] [15m] selector              │  Depth Bar    │
│ • Signals│                                   │  Spread       │
│ • Trades │                                   │               │
│ • Portf. │                                   │               │
│          ├───────────────────────────────────┼───────────────┤
│          │  Active Signals Panel              │  Quick Trade  │
│          │  ┌─────┐ ┌─────┐ ┌─────┐         │  [Engine ▼]   │
│          │  │BTC▲ │ │ETH▼ │ │SOL▲ │         │  [Amount]     │
│          │  │62%  │ │58%  │ │55%  │         │  [BUY] [SELL] │
│          │  └─────┘ └─────┘ └─────┘         │               │
│          ├───────────────────────────────────┴───────────────┤
│          │  Open Positions / Recent Trades Table              │
│          │  Symbol | Direction | Entry | PnL | Status        │
│          │  BTC    | UP        | 0.55  | +$12| OPEN          │
│          │  ETH    | DOWN      | 0.42  | -$3 | CLOSED        │
├──────────┴───────────────────────────────────────────────────┤
│  Portfolio Summary: Balance $9,847 | PnL -$153 | WR 54%     │  ← Footer Bar
└──────────────────────────────────────────────────────────────┘
```

**Bileşenler:**

| Bileşen | Açıklama | Güncelleme |
|---------|----------|------------|
| **Ticker Strip** | 7 coin fiyat + 24h değişim + mini sparkline | SignalR, 1-2sn |
| **Candlestick Chart** | OHLCV + volume + indicator overlay | SignalR, 2sn (kline) |
| **Order Book** | Bid/Ask depth + spread (Polymarket veya Binance) | SignalR, 1sn |
| **Signal Cards** | Aktif sinyaller: asset, yön, confidence, edge | SignalR, sinyal üretildiğinde |
| **Trade Table** | Açık + son kapatılmış pozisyonlar | SignalR, trade açıldığında |
| **Quick Trade** | Motor seçici + hızlı trade butonu | User interaction |
| **Portfolio Footer** | Bakiye, toplam PnL, win rate, toplam trade | SignalR, 5sn |

### 5.4 Dashboard #2 — Admin & Analytics Panel (`:5002`)

**Hedef:** Sistemin performansını analiz etme, feedback loop görselleştirme, konfigürasyon yönetimi.

```
┌──────────────────────────────────────────────────────────────┐
│ [Logo] Admin Panel     [System: ● RUNNING] [Uptime: 3h 42m] │
├──────────┬───────────────────────────────────────────────────┤
│          │                                                   │
│ Sidebar  │  Performance Overview                             │
│          │  ┌──────────┐ ┌──────────┐ ┌──────────┐ ┌──────┐│
│ • Perf.  │  │ Win Rate │ │ Total PnL│ │ Sharpe   │ │Trades││
│ • Calib. │  │  54.2%   │ │ +$847    │ │  1.34    │ │ 312  ││
│ • Trades │  └──────────┘ └──────────┘ └──────────┘ └──────┘│
│ • Engine │  ┌─────────────────────────────────────────────┐ │
│ • Config │  │  Equity Curve (ApexCharts — area chart)      │ │
│ • Logs   │  │  ~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~~   │ │
│          │  └─────────────────────────────────────────────┘ │
│          │  ┌─────────────────────┐ ┌─────────────────────┐ │
│          │  │ Calibration Chart   │ │ PnL by Engine       │ │
│          │  │ Predicted vs Actual │ │ PaperPoly: +$320    │ │
│          │  │ (scatter / line)    │ │ PaperBinance: +$527 │ │
│          │  └─────────────────────┘ └─────────────────────┘ │
│          ├───────────────────────────────────────────────────┤
│          │  Engine Comparison Table                          │
│          │  Engine      | Trades | WR   | PnL   | Sharpe   │
│          │  PaperPoly   | 156    | 52%  | +$320 | 1.12     │
│          │  PaperBinance| 156    | 57%  | +$527 | 1.56     │
│          ├───────────────────────────────────────────────────┤
│          │  Recent Decisions Log (scrollable)                │
│          │  [14:32] BTC 5m UP — FV:0.62 Edge:0.07 ✓ WIN    │
│          │  [14:27] ETH 15m DOWN — FV:0.58 Edge:0.04 ✗ LOSS│
└──────────┴───────────────────────────────────────────────────┘
```

**Sayfalar:**

| Sayfa | İçerik |
|-------|--------|
| **Performance** | KPI kartları, equity curve, engine karşılaştırma |
| **Calibration** | Predicted vs actual scatter plot, Brier score trend |
| **Trade History** | Filtrelenebilir trade tablosu (engine, symbol, regime, outcome) |
| **Engine Status** | Her motorun durumu, açık pozisyonlar, bakiye |
| **Configuration** | Trading parametreleri (min edge, kelly fraction, max position) — runtime değişiklik |
| **System Logs** | Serilog çıktısı, hata logları, bağlantı durumu |

### 5.5 SignalR Hub Tasarımı

Worker service (BackgroundService) → SignalR Hub → Her iki Dashboard

```
[Worker Service]
    │
    ├── Binance WebSocket → candle update → CandleHub.SendCandleUpdate(symbol, candle)
    ├── Signal generated   → SignalHub.SendNewSignal(signal)
    ├── Trade opened/closed → TradeHub.SendTradeUpdate(trade)
    └── Portfolio changed   → PortfolioHub.SendPortfolioUpdate(portfolio)
         │
         ▼
[SignalR Hub] ──push──→ [Dashboard :5001] (trader ekranı)
                   └──→ [Admin :5002] (analytics ekranı)
```

**Hub'lar:**

```
ICryptoHub
├── SendCandleUpdate(string symbol, string interval, CandleDto candle)
├── SendTickerUpdate(string symbol, decimal price, decimal change24h)
├── SendSignalGenerated(SignalDto signal)
├── SendTradeOpened(TradeDto trade)
├── SendTradeClosed(TradeDto trade)
├── SendPortfolioUpdate(PortfolioDto portfolio)
└── SendSystemStatus(SystemStatusDto status)
```

**İstemci tarafı (Blazor component):**
Blazor Server zaten SignalR üzerinde çalıştığı için ek WebSocket bağlantısı gerekmez.
Singleton `IPriceService` event raise eder → component `OnInitializedAsync`'de subscribe olur → `InvokeAsync(StateHasChanged)` ile UI güncellenir.

### 5.6 Tailwind CSS v4 Entegrasyonu (Blazor Server)

Tailwind CSS v4 standalone CLI kullanılacak — Node.js/npm gerekmez:

1. `tailwindcss-windows-x64.exe` solution root'a indirilir
2. Her UI projesinde `Styles/input.css` oluşturulur
3. `.csproj` build target'ına eklenir:
```xml
<Target Name="Tailwind" BeforeTargets="Build">
  <Exec Command="$(SolutionDir)tailwindcss -i Styles/input.css -o wwwroot/css/app.css --minify" />
</Target>
```
4. Development'ta `--watch` mode ile çalıştırılır

### 5.7 IDbContextFactory Pattern (Blazor Server + EF Core)

Her iki Blazor projesi aynı DbContext'i kullanır ama `IDbContextFactory` ile:

```
// Infrastructure/DependencyInjection.cs
services.AddDbContextFactory<AppDbContext>(options =>
    options.UseSqlServer(connectionString));

// Her iki Blazor projesinin Program.cs'inde:
builder.Services.AddInfrastructure(connectionString);
```

**Neden IDbContextFactory?**
Blazor Server circuit'leri HTTP request'lerden uzun yaşar. Normal scoped DbContext thread-safe değildir.
`IDbContextFactory` her operasyon için yeni DbContext üretir, hemen dispose eder.

---

## 6. NuGet PAKETLERİ

| Paket | Katman | Amaç |
|-------|--------|------|
| `Binance.Net` 12.x | Infrastructure.Binance | REST + WebSocket |
| `Nethereum.Signer.EIP712` | Infrastructure.Polymarket | EIP-712 order signing |
| `Nethereum.Web3` | Infrastructure.Polymarket | Ethereum interaction |
| `MathNet.Numerics` | Domain | Normal CDF (Φ), istatistik |
| `MediatR` | Application | CQRS |
| `FluentValidation` | Application | Input validation |
| `Polly` v8 | Infrastructure | Resilience (retry, circuit breaker) |
| `Serilog` + sinks | Infrastructure | Structured logging |
| `Microsoft.EntityFrameworkCore.SqlServer` | Infrastructure | SQL Server |
| `LightweightCharts.Blazor` 5.x | Dashboard/Admin | TradingView candlestick chart |
| `Blazor-ApexCharts` 6.x | Dashboard/Admin | Area, bar, pie, equity curve charts |
| `xUnit` + `FluentAssertions` | Tests | Test framework |
| `NSubstitute` | Tests | Mocking |

**CSS Tooling (NuGet değil):**
- `tailwindcss-windows-x64.exe` (v4 standalone CLI) — build pipeline'a entegre

---

## 6. UYGULAMA PLANI (FAZLAR)

### Faz 1: Temel Altyapı + Binance Veri (Öncelik: EN YÜKSEK)
**Hedef:** Binance'den gerçek zamanlı veri akışı + in-memory buffer + indicator hesaplama

1. Solution ve proje yapısını oluştur (Onion Architecture)
2. Domain katmanı: Asset, TimeFrame, Candle, Signal, TechnicalIndicators
3. Application katmanı: IMarketDataProvider, IIndicatorCalculator, IFairValueCalculator
4. Binance adapter: REST (startup klines) + WebSocket (real-time stream)
5. InMemoryCandleBuffer: ConcurrentDictionary ile rolling window
6. Indicator hesaplama: RSI, MACD, BB, ATR, VWAP, SMA crossover, Stochastic
7. Unit testler
8. **Çıktı:** 7 coin × 2 interval gerçek zamanlı veri akıyor, indicatorlar hesaplanıyor

### Faz 2: Karar Motoru + Fair Value (Öncelik: YÜKSEK)
**Hedef:** Signal generation pipeline tamam

1. FairValueCalculator: P(Up) = Φ(d2) implementasyonu
2. Momentum estimator (μ): EMA of log returns
3. Volatility estimator (σ): Parkinson estimator
4. Multi-confirmation filter (3/5 kural)
5. Regime detection (volatility ratio)
6. PositionSizer: Half Kelly
7. SignalGenerator: Tüm pipeline birleştir
8. Unit testler (özellikle fair value doğruluğu)
9. **Çıktı:** Her candle kapanışında signal üretiyor

### Faz 3: Paper Trading Motorları (Öncelik: YÜKSEK)
**Hedef:** Simülasyon ile trade yapılıyor, loglanıyor, analiz ediliyor

1. ITradingEngine interface
2. PaperPolymarketEngine: Sanal bakiye, YES/NO pozisyon, resolution simülasyonu
3. PaperBinanceEngine: Sanal bakiye, al/sat, SL/TP simülasyonu
4. Portfolio aggregate: Balance, positions, equity curve tracking
5. TradeLogger: Her trade kararı SQL'e yazılır
6. SQL schema + EF Core migration
7. Worker service: BackgroundService olarak ana loop
8. Integration testler
9. **Çıktı:** Paper trade yapıyor, sonuçları logluyor

### Faz 4: Trading Dashboard — Blazor Server UI #1 (Öncelik: YÜKSEK)
**Hedef:** Trader'ın gerçek zamanlı piyasa verisi, sinyaller ve pozisyonları takip ettiği ekran

1. Blazor Server proje oluştur (`Traxon.CryptoTrader.Dashboard`)
2. Tailwind CSS v4 standalone CLI entegrasyonu + TrinkUI dark theme
3. SignalR Hub: `ICryptoHub` — candle, ticker, signal, trade, portfolio push
4. Layout: Sidebar + Ticker Strip + Main Content
5. Candlestick Chart sayfası (LightweightCharts.Blazor) — symbol/interval seçici
6. Ticker Strip component — 7 coin gerçek zamanlı fiyat + 24h değişim
7. Signal Cards component — aktif sinyaller, confidence, edge
8. Open Positions / Trade History table
9. Portfolio Footer — bakiye, PnL, win rate
10. Quick Trade widget — engine seçici + hızlı trade
11. **Çıktı:** Gerçek zamanlı trading dashboard çalışıyor

### Faz 5: Admin & Analytics Panel — Blazor Server UI #2 (Öncelik: ORTA)
**Hedef:** Sistemin performansını analiz etme, feedback loop, konfigürasyon

1. Blazor Server proje oluştur (`Traxon.CryptoTrader.Admin`)
2. Aynı Tailwind dark theme + shared component library
3. Performance sayfası: KPI kartları (win rate, PnL, Sharpe, trade sayısı)
4. Equity Curve chart (ApexCharts — area chart)
5. Calibration sayfası: Predicted vs Actual scatter plot, Brier score trend
6. Engine Comparison table: motor bazlı performans karşılaştırma
7. Trade History sayfası: filtrelenebilir (engine, symbol, regime, outcome)
8. Configuration sayfası: runtime parametre değişikliği (min edge, kelly, max position)
9. System Logs sayfası: Serilog çıktısı, bağlantı durumu
10. Feedback Loop Analytics: Regime bazlı performans, feature importance
11. **Çıktı:** Admin panel ile tam analitik görünürlük

### Faz 6: Polymarket Entegrasyonu (Öncelik: DÜŞÜK — gerçek para)
**Hedef:** Gerçek Polymarket trading

1. PolymarketClient: HTTP + HMAC auth + EIP-712 signing
2. PolymarketWebSocketClient: Market + User channel
3. MarketDiscovery: Gamma API ile aktif crypto Up/Down marketleri bul
4. PolymarketEngine: Gerçek order (maker-side limit order stratejisi)
5. Heartbeat service (5sn)
6. **Çıktı:** Gerçek Polymarket'te trade

### Faz 7: Binance Trading (Öncelik: DÜŞÜK — gerçek para)
**Hedef:** Gerçek Binance spot trading

1. BinanceEngine: Binance.Net ile order management
2. Aynı signal generator, gerçek execution
3. **Çıktı:** Gerçek Binance'te trade

---

## 7. TEKNİK DETAYLAR

### 7.1 Polymarket Client (Custom C# — Faz 5)

Polymarket'in resmi C#/.NET SDK'sı yok. Şunları port edeceğiz:

**Authentication:**
- L1: Nethereum ile EIP-712 structured data signing → API credentials derive et
- L2: `System.Security.Cryptography.HMACSHA256` ile request signing
- Headers: `POLY_ADDRESS`, `POLY_API_KEY`, `POLY_PASSPHRASE`, `POLY_TIMESTAMP`, `POLY_SIGNATURE`

**REST Client:**
- `HttpClient` + custom `DelegatingHandler` (header injection)
- Polly retry policy (429 handling)

**WebSocket Client:**
- `System.Net.WebSockets.ClientWebSocket`
- Market channel: `wss://ws-subscriptions-clob.polymarket.com/ws/market`
- Subscribe by `assets_ids` (token IDs)
- Heartbeat: her 10sn PING gönder

**Order Flow:**
1. Market keşfet (Gamma API) → condition_id ve token_id'leri al
2. Order book oku (`GET /book?token_id=X`)
3. Fair value hesapla
4. Limit order koy (`POST /order`, type: GTC, maker)
5. WebSocket ile fill/cancel takibi
6. `/heartbeats` her 5sn (yoksa tüm orderlar cancel)

### 7.2 Binance WebSocket URL (Tüm Streamler)

```
wss://stream.binance.com:9443/stream?streams=
  btcusdt@kline_5m/ethusdt@kline_5m/solusdt@kline_5m/
  xrpusdt@kline_5m/dogeusdt@kline_5m/hypeusdt@kline_5m/bnbusdt@kline_5m/
  btcusdt@kline_15m/ethusdt@kline_15m/solusdt@kline_15m/
  xrpusdt@kline_15m/dogeusdt@kline_15m/hypeusdt@kline_15m/bnbusdt@kline_15m/
  btcusdt@miniTicker/ethusdt@miniTicker/solusdt@miniTicker/
  xrpusdt@miniTicker/dogeusdt@miniTicker/hypeusdt@miniTicker/bnbusdt@miniTicker
```

21 stream, tek bağlantı, limit 1024.

### 7.3 Normal CDF Implementasyonu

MathNet.Numerics kullanacağız:
```csharp
using MathNet.Numerics.Distributions;

double fairValue = Normal.CDF(0, 1, d2); // Φ(d2)
```

### 7.4 Parkinson Volatility

```csharp
double parkinsVolatility = Math.Sqrt(
    (1.0 / (4.0 * N * Math.Log(2))) *
    candles.Sum(c => Math.Pow(Math.Log(c.High / c.Low), 2))
);
```

---

## 8. RİSK YÖNETİMİ

| Risk | Etki | Önlem |
|------|------|-------|
| Model overfitting | Yanlış sinyaller | 200+ trade ile validation, out-of-sample test |
| Regime change | Trending → ranging geçişinde kayıp | Volatility ratio filter, küçük pozisyon |
| API downtime | Veri kaybı | Polly retry, fallback REST polling |
| Single-side exposure | Büyük kayıp | Max %5 pozisyon, %25 toplam exposure |
| Stale quotes (Polymarket) | Adverse selection | Heartbeat + hızlı cancel-replace |
| SQL bottleneck | Signal gecikme | Async yazma, in-memory öncelik |

---

## 9. BAŞARI METRİKLERİ

| Metrik | Hedef | Açıklama |
|--------|-------|----------|
| Win Rate | > %52 | Break-even üzeri (taker fee dahil) |
| Brier Score | < 0.24 | Coin flip'ten iyi (%50 → 0.25) |
| Sharpe Ratio | > 1.0 | Risk-adjusted return |
| Max Drawdown | < %20 | Bankroll koruması |
| Profit Factor | > 1.5 | Gross profit / gross loss |
| Calibration | ±%5 | Predicted vs actual her bin'de |
| Paper trade sayısı | 200+ | İstatistiksel anlamlılık |

---

## 10. FAZ ÖZET TABLOSU

| Faz | İçerik | Öncelik | Bağımlılık |
|-----|--------|---------|------------|
| **1** | Solution + Domain + Binance veri akışı + Indicatorlar | EN YÜKSEK | — |
| **2** | Karar motoru + Fair value + Signal pipeline | YÜKSEK | Faz 1 |
| **3** | Paper Polymarket + Paper Binance motorları | YÜKSEK | Faz 2 |
| **4** | Trading Dashboard (Blazor Server UI #1) | YÜKSEK | Faz 3 |
| **5** | Admin & Analytics Panel (Blazor Server UI #2) | ORTA | Faz 3 |
| **6** | Gerçek Polymarket entegrasyonu | DÜŞÜK | Faz 3 |
| **7** | Gerçek Binance trading | DÜŞÜK | Faz 3 |

> Faz 4 ve 5 (UI'lar) Faz 3'ten sonra paralel başlatılabilir.

---

## 11. SONRAKI ADIM

**Faz 1'e başla:** Solution yapısı + Domain + Binance veri akışı.

Bu plan Architect'e gönderilecek, detaylı task breakdown yapılacak, Developer uygulayacak.
