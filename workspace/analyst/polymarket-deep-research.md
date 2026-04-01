# Polymarket & Crypto Trading: Complete Deep Research
**Date:** 2026-03-29 | **Analyst:** Traxon Research Agent | **Searches:** 60+ WebSearch queries

---

## A. POLYMARKET 5-MINUTE MARKET MEKANIZMASI

### A.1 Nasil Calisir?
- Her 5dk market basit bir binary bet: Kripto fiyati 5dk sonunda baslangica gore UP mi DOWN mi?
- Kullanicilar $0.01-$0.99 USDC arasinda UP veya DOWN token satin alir
- Fiyat = zımni olasilik (ornek: $0.55 = %55 UP olasiligi)
- **UP + DOWN = $1.00** (Conditional Token Framework ile garanti)
- Kazanan token $1.00 oduyor, kaybeden $0.00
- Resolution kaynak: **Chainlink Data Streams** oracle

### A.2 Desteklenen Varliklar (Mart 2026)
BTC, ETH, SOL, XRP, DOGE, BNB, HYPE — **7 adet**

### A.3 Tam Yasam Dongusu

#### Faz 1: Market Olusturma
- Otomatik **rolling 5dk dongusu** — her 5dk'da yeni market acilir
- Gunluk: **288 market/varlik** (24 saat x 12/saat)
- 7 varlikla: **2,016 market/gun** (sadece 5dk)
- Slug format: `btc-updown-5m-{unix_timestamp}`
- Onceki market resolve olunca yenisi hemen acilir (kesintisiz)

#### Faz 2: Trading (0-300 saniye)
- CLOB order book tum 5dk pencere boyunca acik
- Stratejik trading: **son 30-60 saniye** (T=240-270s)
- %15-20 market son 10 saniyedeki hareketlerle resolve olur
- Polygon latency (2-5s) nedeniyle son 5s bet pratik degil

#### Faz 3: Resolution
- T=300s'de Chainlink oracle zaman damgali fiyat snapshot'i verir
- **Bitis fiyati >= Baslangic fiyati = UP kazanir**
- **Bitis fiyati < Baslangic fiyati = DOWN kazanir**
- Tamamen otomatik, insan mudahalesi yok

#### Faz 4: Settlement
- Polygon uzerinde atomik on-chain settlement
- Kazanan tokenlar $1.00 USDC icin redeem edilir
- Kaybeden tokenlar $0.00 olur
- Resolution sonrasi aninda settlement

### A.4 UP/DOWN Token Fiyatlandirma Mekanizmasi
- ERC1155 tokenlar, Polygon uzerinde Gnosis CTF
- **Splitting:** 1 USDC → 1 UP token + 1 DOWN token (her zaman)
- **Merging:** 1 UP + 1 DOWN → 1 USDC (her zaman)
- Bu UP+DOWN=$1.00 garantisi saglar
- Fiyatlar tamamen **arz-talep** ile belirlenir (CLOB order book)
- Gosterilen fiyat: bid-ask spread'in **midpoint**'i
- Spread > $0.10 ise: **son islem fiyati** gosterilir

### A.5 Fee Yapisi (KRITIK — Mart 2026)

#### Mevcut (29 Mart 2026'ya kadar)
- feeRate: 0.25, exponent: 2, peak rate: **%1.56**
- Formula: `fee = C x p x feeRate x (p x (1-p))^exponent`
- $0.50'de: ~100 share icin ~$3.12 fee (%3.12)
- Ekstrem olasiliklarda (0.01 veya 0.99): fee ~$0

#### YENi (30 Mart 2026'dan itibaren)
- feeRate: **0.072**, exponent: **1**, peak rate: **%1.80**
- **ARTIS:** %1.56 → %1.80 peak
- Yeni formula: exponent=1 (eski 2) — fee'ler probability range'inde daha esit dagiliyor
- 60/40 veya 70/30 olasiliklarda eskisinden DAHA FAZLA fee

#### Maker Rebates
- Crypto marketlerde taker fee'lerin **%20'si** maker'lara dagitilir
- Gunluk USDC olarak odenir
- Maker: GTC/GTD limit order koyanlar

#### Diger
- Gas: ~$0.01/islem (Polygon)
- Deposit/Withdrawal fee: YOK
- Holding rewards: Uygun pozisyonlarda yillik %4

### A.6 Tipik Bid-Ask Spread
| Varlik | Spread | Notlar |
|--------|--------|--------|
| BTC | $0.01 | En dar — en iyi |
| XRP | $0.01 | Dar — iyi |
| ETH | $0.04 | Orta |
| SOL | $0.05 | Orta |
| BNB | $0.04 | Orta |
| DOGE | $0.09 | Genis — riskli |
| HYPE | $0.18 | Cok genis — kacin |

### A.7 Volume ve Likidite
- Gunluk 5dk BTC volume: **$60M+**
- Pencere basina: **$5K-$50K** tipik volume
- $1K altinda volume = manipulasyon riski → ATLA
- BTC tek basina tum crypto binary prediction volume'unun **%67'si**

---

## B. POLYMARKET API TAM REFERANS

### B.1 API Mimarisi

| Servis | Base URL | Auth | Amac |
|--------|----------|------|------|
| Gamma API | `https://gamma-api.polymarket.com` | Hayir | Market kesfetme |
| CLOB API | `https://clob.polymarket.com` | Kismi | Trading, fiyatlar |
| Data API | `https://data-api.polymarket.com` | Hayir | Pozisyonlar, islem gecmisi |
| WS Market | `wss://ws-subscriptions-clob.polymarket.com/ws/market` | Hayir | Canli orderbook |
| WS User | `wss://ws-subscriptions-clob.polymarket.com/ws/user` | Evet | Canli order/trade |
| RTDS | `wss://ws-live-data.polymarket.com` | Opsiyonel | Canli kripto fiyatlar |

### B.2 Market Kesfetme (Gamma API)

```
GET /events?active=true&closed=false&order=volume_24hr&limit=100
GET /events/slug/{slug}
GET /markets
GET /markets/slug/{slug}
```

**5dk market slug hesaplama:**
```
ts = floor(now_unix / 300) * 300
slug = "btc-updown-5m-{ts}"
```

**15dk:**
```
ts = floor(now_unix / 900) * 900
slug = "btc-updown-15m-{ts}"
```

**Rate limit:** 4,000 req/10s (genel), 500/10s (events), 300/10s (markets)

### B.3 Fiyat ve Orderbook (CLOB API — Auth gereksiz)

```
GET /price?token_id={id}&side=BUY       # Mevcut fiyat
GET /midpoint?token_id={id}              # Mid price
GET /book?token_id={id}                  # Tam orderbook
GET /books?token_ids={id1},{id2}         # Toplu orderbook
GET /spread?token_id={id}               # Bid-ask spread
GET /tick-size?token_id={id}            # Minimum fiyat artisi
GET /last-trade-price?token_id={id}     # Son islem fiyati
GET /prices-history?token_id={id}       # Gecmis fiyatlar
```

**Rate limit:** 9,000/10s (genel), 1,500/10s (book, price, midpoint)

### B.4 Order Verme (CLOB API — Auth GEREKLI)

```
POST /order          # Tekli order (max 15 batch)
DELETE /order         # Tekli iptal
POST /orders         # Toplu order
DELETE /orders        # Toplu iptal (max 3000)
DELETE /cancel-all   # Tum orderlari iptal
```

**Order tipleri:**
| Tip | Davranis | Kategori |
|-----|----------|----------|
| GTC | Doldurulana/iptal edilene kadar | Limit/Maker |
| GTD | Belirtilen tarihe kadar | Limit/Maker |
| FOK | Tamamen doldur veya iptal | Market/Taker |
| FAK | Mumkun olani doldur, kalanini iptal | Market/Taker |

**Post-only:** GTC/GTD ile uyumlu. Spread'i gecerse reddedilir → maker garantisi.

**Order rate limit:** 3,500 burst/10s, 36,000 sustained/10dk (~60/s ortalama)

### B.5 Authentication

**Level 1 (L1) — Wallet Signature:** EIP-712 imza, order imzalama icin
**Level 2 (L2) — HMAC-SHA256:** Order verme/iptal icin

```
Headers: POLY_ADDRESS, POLY_API_KEY, POLY_PASSPHRASE, POLY_TIMESTAMP, POLY_SIGNATURE
Signing: HMAC-SHA256(secret, timestamp + method + path + body)
```

**ONEMLI:** L2 ile bile order payload'lari L1 ile imzalanmali (wallet private key).

### B.6 WebSocket Real-Time Data

**Market channel subscription:**
```json
{
  "assets_ids": ["token_id_1", "token_id_2"],
  "type": "market"
}
```
Mesaj tipleri: `book`, `price_change`, `best_bid_ask`, `new_market`, `market_resolved`

**RTDS (Crypto fiyatlar):**
```json
{
  "action": "subscribe",
  "subscriptions": [{
    "topic": "crypto_prices",
    "type": "price_update",
    "filters": "btcusdt,ethusdt,solusdt,xrpusdt,dogeusdt,bnbusdt"
  }]
}
```
Kaynaklar: Binance (auth yok), Chainlink (sponsored key gerekli)
Keepalive: PING her 5 saniye

### B.7 Session Heartbeat (KRITIK)
```
POST /heartbeat her 5 saniyede bir
10 saniye icinde heartbeat gonderilmezse TUM acik orderlar IPTAL edilir!
```

### B.8 SDK'lar
- **Python:** `py-clob-client` (resmi)
- **TypeScript:** `clob-client` (resmi)
- **.NET:** YOK — manuel implement gerekli (HTTP + HMAC + EIP-712 via Nethereum)

---

## C. BOT STRATEJILERI VE KARLILIK

### C.1 Basarili Strateji Turleri

#### Strateji 1: Latency Arbitrage (En Yuksek Kar)
- Binance fiyat hareketi ile Polymarket odds guncellenmesi arasi **30-90 saniye gecikme**
- Bot 0x8dxd: $313 → $438,000, 1 ayda, **%98 win rate**
- **ANCAK:** Dinamik taker fee'ler (Ocak 2026) bu stratejiyi buyuk olcude oldurdu
- %3.15 fee tipik arbitrage marjini asar

#### Strateji 2: Market Making (Yeni Meta)
- Iki tarafa limit order koy, spread yakala
- Maker fee = $0 + %20 rebate
- Win rate: %78-85, aylik %1-3 getiri, <%1 drawdown
- Ornek: $10K sermaye ile $1,247 kar (3 haftada %12.47)

#### Strateji 3: AI-Powered Probability Arbitrage
- Ensemble model (GPT-4 + Claude + fine-tuned) consensus olasilik uretir
- Market fiyatindan >%15 sapma varsa trade et
- Win rate: %65-75, aylik %3-8 getiri

#### Strateji 4: Last-Second Momentum (5dk Spesifik)
- Son 30-60 saniyede Monte Carlo (1000 path) ile gercek olasilik hesapla
- Edge > fee + slippage ise trade et
- Win rate: %55-60 (backtest)
- Fee artisi sonrasi: %5-10 edge esigi gerekli (eskiden %2-3)

### C.2 Neden Cogu Bot Zarar Eder?
- **%92.4 Polymarket cuzdani zarar eder**, sadece %0.51 $1000+ kazanir
- Fee'ler karli gorunen stratejileri zarara cevirir
- Polymarket kural degisiklikleri (500ms delay kaldirildi, dinamik fee eklendi) botlari kirar
- Yetersiz likidite kontrolu — ince order book'larda slippage
- Altyapi yetersizligi — public RPC endpoint'ler olumcul latency ekler
- Arbitrage penceresi: 2024'te 12.3s → 2026'da **2.7s**
- Karlarin %73'u sub-100ms botlara gidiyor

### C.3 Bilinen Basarili Operatorler
| Kimlik | Strateji | Performans |
|--------|----------|------------|
| 0x8dxd | Latency arb (BTC/ETH/SOL 15dk) | $313 → $550K+, %98 WR |
| Igor Mikerin | Ensemble probability | $2.2M kar, 2 ayda |
| Claude AI botlari | AI yonlu trading | "Yuz binlerce" kar (Mart 2026) |
| Top 14/20 leaderboard | Cesitli otomasyon | ~$40M toplam (2024-2025) |

---

## D. 5DK KRIPTO FIYAT TAHMIN YONTEMLERI

### D.1 Ulasilabilir Win Rate
- Akademik: %52-67 out-of-sample
- Backtest (indikator combo): %77'ye kadar ideal kosullarda
- **Gercekci hedef: %55-62**

### D.2 En Iyi Indikatorler (Tier Bazli)
**Tier 1 (Zorunlu):**
- RSI(7) — kisa vadeli momentum
- MACD(3-10-16) — hizli ayarlar, 5dk icin optimize
- EMA 9/21 — trend yonu
- VWAP — intraday seviye
- Bollinger Bands(20,2) — volatilite ve mean reversion

**En Iyi Combo:** RSI + MACD + BB = %77 backtest WR (vs %40-60 tek basina)

**Profesyonel standart:** 1 trend filtre + 1 momentum + 1 volume dogrulama

### D.3 Momentum vs Mean Reversion
- Mean reversion: 4-8dk ufukta teknik olarak optimal
- Momentum: trend eden marketlarda ustun
- **50/50 blend: Sharpe 1.71, %56 yillik getiri**
- **En iyi yaklasim: Regime detector (ADX/volatilite) ile mod degistir**

### D.4 En Prediktif Feature
- **10-5dk onceki fiyat degisimi** (son 1dk degil!) en onemli feature
- Order book imbalance: %71-73 F1 score (cok kisa vadede)
- Funding rate: Kontrariyan sinyal (>%0.1 veya <%−0.1'de)

### D.5 Optimal Sistem Tasarimi
```
Regime Detection (ADX + BB width)
  ├── Momentum Mode: MACD + EMA crossover
  └── Mean Reversion Mode: RSI extremes + VWAP bounce
Volume/CVD ile dogrulama
ATR-bazli dinamik SL/TP
```

---

## E. BINANCE SCALPING DETAYLARI

### E.1 Order Tipleri
| Tip | Aciklama |
|-----|----------|
| LIMIT | Standart limit order |
| MARKET | Aninda market order |
| STOP_LOSS_LIMIT | SL tetiklenince limit order |
| TAKE_PROFIT_LIMIT | TP tetiklenince limit order |
| LIMIT_MAKER | Post-only (maker garanti) |
| **OTOCO** | Entry + SL + TP tek seferde (HOLY GRAIL) |

### E.2 OTOCO (One-Triggers-One-Cancels-Other)
- Tek API cagrisi: Entry LIMIT + Pending OCO (TP + SL)
- Entry dolunca SL+TP otomatik aktif olur
- Bir tarafi dolunca digeri otomatik iptal
- **Scalping icin ideal** — 3 order 1 cagri

### E.3 Fee Yapisi
| Tier | 30g Volume | Maker | Taker | BNB ile |
|------|-----------|-------|-------|---------|
| VIP 0 | <1M | %0.10 | %0.10 | %0.075/%0.075 |
| VIP 1 | >=1M | %0.09 | %0.10 | %0.0675/%0.075 |
| VIP 3 | >=20M | %0.042 | %0.06 | %0.0315/%0.045 |

Round-trip VIP0+BNB: **%0.15** — scalp karinin %30-50'si fee'ye gider!
**LIMIT_MAKER kritik** — maker fee garantisi.

### E.4 Rate Limitler
- **100 order/10s** (hesap basina)
- **200,000 order/24s** (hesap basina)
- WebSocket: 5 mesaj/s, max 1024 stream, 300 baglanti/5dk

### E.5 WebSocket Streamleri
| Stream | Format | Hiz |
|--------|--------|-----|
| Kline | `{symbol}@kline_5m` | 2000ms |
| Book Ticker | `{symbol}@bookTicker` | Real-time (en hizli) |
| Depth | `{symbol}@depth20@100ms` | 100ms |
| Agg Trade | `{symbol}@aggTrade` | Real-time |

### E.6 Scalping Stratejileri
1. **EMA Crossover + RSI + BB:** 5-EMA/20-EMA cross, RSI<30, BB alt band → LONG
2. **VWAP + Order Flow:** VWAP altinda al, ustunde sat, order book imbalance dogrulama
3. **BB Squeeze Breakout:** Dar BB → breakout yonune gir
4. **Regime-Adaptive:** ATR ratio (20/100 period) ile ranging/trending ayir

---

## F. YUKSEK FREKANLI TRADING ANALIZI

### F.1 500+ Trade/Saat Mumkun mu?
- Binance: 100 order/10s = teorik max 18,000/saat
- **500 trade/saat = ~71/coin/saat = ~1.2/coin/dk — MUMKUN**
- Gercek kisit: API limitleri degil, **fee erozyonu**
- VIP0 ile %0.2 round-trip: 500 trade/saat = **saatte sermayenin %1'i fee'ye gider**

### F.2 Korelasyon Riski (7 Coin)
- BTC-ETH: **0.85-0.89**
- BTC-SOL: **0.72-0.99** (cok tehlikeli)
- Krizlerde: korelasyon ~1.0'a cikar
- **Cluster yaklasimi gerekli:**
  - Cluster 1 (BTC-correlated): BTC, ETH, SOL → TEK risk birimi
  - Cluster 2 (Alt): XRP, BNB, DOGE → farkli risk birimi
  - Cluster 3 (Prediction): Polymarket → yapisal olarak farkli

### F.3 Paralel 5dk + 15dk Trading
- 15dk: trend yonu ve S/R seviyeleri
- 5dk: giris/cikis zamanlama
- 1sa (opsiyonel): makro filtre
- **Catisma: 15dk > 5dk (buyuk timeframe oncelikli)**

### F.4 Polymarket + Binance Kombinasyonu
- Polymarket, Binance'in **30-90 saniye gerisinde** kalir
- Binance'te fiyat hareketini gor → Polymarket'te pozisyon ac
- $40M bu strateji ile cikartildi (2024-2025)
- Hedging: Polymarket DOWN + Binance LONG = market-neutral

---

## G. BREAK-EVEN ANALIZI

### Polymarket (50/50 odds, $0.50 token):
```
Fee: ~%3.12 (100 share icin ~$3.12)
Net win: $0.484/share
Net loss: -$0.516/share

Break-even win rate: %51.6

55% WR → +$0.034/dolar → 100 trade/gun $50 → +$170/gun
54% WR → +$0.020/dolar → 100 trade/gun $50 → +$100/gun
53% WR → +$0.014/dolar → 100 trade/gun $50 → +$70/gun
52% WR → +$0.004/dolar → 100 trade/gun $50 → +$20/gun
51% WR → -$0.006/dolar → ZARAR
```

### Binance (VIP0 + BNB):
```
Round-trip fee: %0.15
0.30% TP hedefi ile net kar: %0.15 (fee'nin %50'si)
0.45% TP hedefi ile net kar: %0.30

Break-even: TP/SL oraniyla degisir
1:1 R:R ile break-even WR: ~%52
1:1.5 R:R ile break-even WR: ~%45
```

---

## Kaynaklar

### Polymarket
- Polymarket Fees Documentation (docs.polymarket.com)
- Polymarket CLOB API Documentation
- Polymarket Gamma API Documentation
- Polymarket Real-Time Data Socket
- Polymarket Maker Rebates Program
- CoinMarketCap: Polymarket 5-Min BTC Markets
- Finance Magnates: Dynamic Fees
- The Block: Chainlink Integration
- KuCoin: Polymarket Fees 2026
- CoinCu: Fee Expansion March 30

### Bot Stratejileri
- Medium: Unlocking Edges in 5-Min Markets (Benjamin BigDev)
- Medium: 4 Strategies Bots Profit From in 2026
- Medium: Why 92% of Polymarket Traders Lose
- Finbold: Bot $313 to $438K
- Yahoo Finance: Arbitrage Bots Dominate Polymarket
- QuantVPS: Latency Impact on Bot Performance
- GitHub: Polymarket/agents, poly-maker, polymarket-hft-engine

### Crypto Prediction
- Academic papers on 5-min crypto prediction (%52-67 OOS)
- QuantifiedStrategies: Mean Reversion backtests
- 3Commas: Mean Reversion bots

### Binance
- Binance Trading Endpoints Documentation
- Binance WebSocket Streams Documentation
- Binance OCO/OTO/OTOCO FAQ
- Binance Fee Schedule 2026
