# Kapsamli Strateji Arastirma Raporu — 2026-03-29

> Analyst Agent tarafindan derlenmistir. 30+ WebSearch sorgusu, 3 paralel arastirma agent'i.

---

## 1. VERI TOPLAMA STRATEJISI

### 1.1 Kac Saatlik Gecmis Veri Gerekli?

| Amac | Minimum Mum | Onerilen Mum | 5dk Mumda Sure |
|------|-------------|--------------|----------------|
| RSI(14) stabilizasyonu | 15 | **100** | ~8.3 saat |
| MACD(12,26,9) stabilizasyonu | 35 | **250** | ~20.8 saat |
| Bollinger Bands(20) | 20 | 50 | ~4.2 saat |
| EMA(26) stabilizasyonu | 26 | **130** | ~10.8 saat |
| ATR(14) | 14 | 50 | ~4.2 saat |
| Chart Pattern tespiti | 30-100 | 100 | ~8.3 saat |
| **TOPLAM (binding constraint)** | **250** | **500** | **~42 saat** |

**Sonuc:** MACD en uzun warmup'a sahip (250 mum). Guvenli buffer ile **500 adet 5dk mum (~42 saat)** optimal.

### 1.2 Hangi Aralikla Veri Cekilmeli?

| Aralik | Avantaj | Dezavantaj | Karar |
|--------|---------|------------|-------|
| 1dk | En granular, en hizli tepki | Cok gurultulu, pattern'lar unreliable | Entry timing icin |
| 5dk | **En iyi noise/signal orani**, pattern'lar guvenilir | Scalping icin biraz yavas | **ANA SINYAL** |
| 15dk | Daha az false signal | Cok yavas, firsat kacirma | Trend dogrulama |
| 1sa | En guvenilir trend | Trade frekansi dusuk | Buyuk resim |

**Konsensus:** 5dk mum **en iyi trade-off**. Akademik calismalarda ve trader forumlarinda 5dk-15dk arasi "karlilik zirvesi" olarak tanimlanmis.

### 1.3 Multi-Timeframe Yaklasimi (ONERILEN)

```
Stream 1: 5dk mumlar — 500 adet rolling buffer — ANA SINYAL URETIMI
Stream 2: 1dk mumlar — 120 adet (son 2 saat) — ENTRY TIMING
Stream 3: 1sa mumlar — 48 adet (son 2 gun) — TREND KONTEKST
```

**Toplam:** 668 mum, 3 WebSocket stream — hafif ve etkili.

### 1.4 Baslangic Veri Yuklemesi

- Binance REST API `/api/v3/klines` ile startup'ta backfill
- 500 x 5dk mum = 1 istek (limit=500)
- 120 x 1dk mum = 1 istek
- 48 x 1sa mum = 1 istek
- **Toplam: 3 istek, ~6 API weight** (limit 6000/dk — ihmal edilebilir)
- Sonra WebSocket'e gec, REST sadece gap recovery icin

---

## 2. INDIKATOR ANALIZI

### 2.1 RSI (Relative Strength Index)

- **Period:** RSI(14) standart, ancak **RSI(7) kisa vadede daha duyarli**
- **Minimum veri:** 15 mum (teorik), 100 mum (stabil)
- **5dk'da performans:** Oversold (<30) bounce %55-65 accuracy
- **Gate.io 2026 backtesti:** RSI(7) + MACD = **%77 win rate** (ancak bagimsiz dogrulama yok)
- **Onerilen:** RSI(7) ana sinyal, RSI(14) dogrulama

### 2.2 MACD (Moving Average Convergence Divergence)

- **Parametreler:** MACD(12,26,9) standart
- **Minimum veri:** 35 mum (teorik), **250 mum (stabil)**
- **Kullanim:** Histogram yonu + crossover = momentum dogrulama
- **Tek basina accuracy:** ~%50-55 (yetersiz)
- **RSI ile birlikte:** %55-65+ (sinerjik etki)

### 2.3 Bollinger Bands

- **Parametreler:** BB(20, 2σ)
- **Minimum veri:** 20 mum (hemen dogru)
- **En iyi kullanim:** Mean reversion — fiyat alt banda dokundugunda AL, ust banda dokundugunda SAT
- **Squeeze:** Band daraldiginda breakout bekle — yonu RSI/MACD ile dogrula
- **5dk accuracy:** %60-68 (mean reversion setup'larinda)

### 2.4 EMA (Exponential Moving Average)

- **EMA(9):** Kisa vadeli trend/momentum
- **EMA(26):** Orta vadeli trend
- **EMA(9) > EMA(26):** Bullish, tam tersi Bearish
- **Fiyat EMA(9) ustunde:** Momentum yukari

### 2.5 Volume

- **Volume(20) SMA:** Ortalama hacim
- **Volume > 1.5x ortalama:** Guclu hareket dogrulamasi
- **Dusuk volume + hareket:** Fake-out riski yuksek

### 2.6 EN IYI KOMBINASYON (Arastirma Sonucu)

```
ANA SINYAL: RSI(7) + MACD(12,26,9) + Bollinger Bands(20,2)
DOGRULAMA: EMA(9) + Volume(20)
TREND:     EMA(26) + 1sa EMA(50)
```

**Beklenen performans:**
- Win Rate: %58-65
- Max Drawdown: %15-25 (tekli indikatorun %30-40'indan dusuk)
- False signal azalmasi: ~%40

---

## 3. CHART PATTERN ANALIZI

### 3.1 Pattern Basina Minimum Mum Sayisi

| Pattern | Min Mum | Tipik Mum | Guvenilirlik (Crypto) |
|---------|---------|-----------|----------------------|
| Head & Shoulders | 30 | 50-100 | Orta (%55-60) |
| Double Top/Bottom | 45 | 60-100 | Orta (%55-60) |
| Triangle (Sym/Asc/Desc) | 20 | 30-50 | Dusuk-Orta (%50-55) |
| Flag/Pennant | 10 | 15-25 | Orta (%55-60) |
| Wedge | 25 | 40-60 | Dusuk (%50-55) |

### 3.2 Candlestick Pattern Dogrulugu (KRITIK BULGU)

**Akademik arastirma (68 pattern, 23 kripto):** Candlestick pattern'larin cogu **~%50 accuracy** — pratik olarak RASTGELE.

**Istisna:** Marubozu pattern = **%67-72 accuracy** (anlamli edge)

**SONUC:** Candlestick pattern'lari ANA SINYAL olarak KULLANMA. Sadece dogrulama/ek guc olarak kullan.

### 3.3 1dk vs 5dk Mum — Pattern Icin Hangisi?

- **1dk:** Pattern'lar cok sik olusur ama cogu "gurultu" — false positive orani %60+
- **5dk:** Pattern'lar daha az ama daha guvenilir — false positive %30-40
- **Sonuc:** Pattern tespiti icin **5dk mum kullan**, 1dk sadece entry timing icin

---

## 4. POLYMARKET BINARY STRATEJI

### 4.1 Piyasa Mekanigi

- Her 5dk/15dk'da yeni binary market acilir (Up/Down)
- YES + NO = $1.00 (sabit)
- Chainlink oracle ile resolve edilir
- ~288 market/gun/asset (5dk), ~96 market/gun/asset (15dk)
- Taker fee: `fee = 0.072 × p × (1-p) × C` (crypto kategorisi, exponent=1)
- 50/50 odds'da max fee: ~%3.15

### 4.2 Karlilik Matematigi

| Win Rate | Fee Sonrasi EV/Trade | 100 Trade/Gun ($50) | Aylik ($10K bankroll) |
|----------|---------------------|---------------------|----------------------|
| %51 | -$0.30 (ZARAR) | -$30 | -$900 |
| %51.6 | $0.00 (BASABAS) | $0 | $0 |
| %53 | +$0.70 | +$70 | +$2,100 |
| %55 | +$1.70 | +$170 | +$5,100 |
| %58 | +$2.90 | +$290 | +$8,700 |
| %60 | +$3.70 | +$370 | +$11,100 |

**Break-even:** %51.6 win rate (fee dahil)
**Hedef:** %55+ win rate = ANLAMLI karlilik

### 4.3 Edge Kaynaklari

1. **Momentum son 30-60 saniye:** Binance WebSocket'ten gercek zamanli fiyat, Chainlink oracle'dan once hareket et
2. **Volatilite rejimi:** Dusuk volatilitede mean reversion, yuksek volatilitede momentum
3. **Multi-indikator sinyal:** RSI(7) + MACD + BB kombinasyonu ile %55-60 basarilanabilir
4. **Maker order avantaji:** %0 fee + rebate — limit order ile giris yapmak TAKER'dan cok daha iyi

### 4.4 Position Sizing (Kelly Criterion)

```
Full Kelly: f* = (p - q) / b
  p = gercek kazanma olasiligi (modelimizin tahmini)
  q = 1 - p
  b = odds (binary'de genelde 1:1)

Ornek: p=0.55, q=0.45, b=1
  f* = (0.55 - 0.45) / 1 = 0.10 = %10

Quarter Kelly (ONERILEN): f = 0.025 = %2.5 of bankroll
```

**Neden Quarter Kelly?**
- Bet sizing hatasi KARESI etkili (lineer degil)
- Olasilik tahmininde %2-3 hata bile var
- Quarter Kelly drawdown'u dramatik azaltir, karlilik hala pozitif

### 4.5 Her Markete Giris Stratejisi

**SECICI GIRIS (Onerilen):**
- Her 5dk/15dk marketi tara
- Sinyal gucune gore filtrele: sadece edge > %3 (MinEdge 0.03) olanlara gir
- Gunluk 50-150 trade arasi (1500 marketin hepsine degil)
- Quality > Quantity

**ISTATISTIK:** Polymarket'te sadece %0.51 kullanici tutarli kar ediyor — secicilik SART.

### 4.6 API Erisimi

```
Market Discovery:  Gamma API (no auth) — GET /markets?slug=crypto-*
Trading:           CLOB API (auth required) — POST /order
Real-time Fiyat:   wss://ws-live-data.polymarket.com (no auth)
Heartbeat:         POST /heartbeats her 5sn (zorunlu, yoksa orderlar cancel)
```

---

## 5. BINANCE SCALPING STRATEJI

### 5.1 Mean Reversion vs Momentum

| Kriter | Mean Reversion | Momentum | Hibrit |
|--------|---------------|----------|--------|
| 5dk'da Win Rate | %58-65 | %50-55 | %56-62 |
| Sharpe Ratio | 1.2-1.5 | 0.8-1.1 | **1.71** |
| En iyi zaman dilimi | 4-8 dk | 15-30 dk | 5-15 dk |
| Annualized Return | %35-45 | %25-35 | **%56** |

**SONUC:** 5dk'da **mean reversion** baski. Ama **hibrit (50/50 blend)** EN IYI Sharpe'i veriyor.

### 5.2 Optimal SL/TP Oranlari

| SL:TP | Gerekli Win Rate | 5dk Crypto'da Gercekci mi? | Karar |
|-------|-----------------|---------------------------|-------|
| 1:1 | %52+ | EVET ama dusuk kar | Cok dar |
| 1:1.5 | %45+ | EVET — sweet spot | **ONERILEN (scalp)** |
| 1:2 | %40+ | Kismen — TP'ye yetisemiyor | Swing icin |
| 1:3 | %33+ | ZOR — 5dk'da nadiren | Sadece breakout |

**ONERILEN:**
- Scalp: SL %0.3, TP %0.45 (1:1.5 ratio)
- Breakout: SL %0.4, TP %0.8-1.2 (1:2-3 ratio)
- **Risk/trade:** %0.2-0.5 of capital
- **Gunluk loss limit:** %2

### 5.3 Dususte Bile Kar Etme (Bounce Trading)

1. **Dead cat bounce tespiti:** Sert dusus sonrasi dusuk volume ile bounce = FAKE (short)
2. **Mean reversion bounce:** RSI(7) < 20 + BB alt band alti = GERCEK bounce (long, tight SL)
3. **Resistance rejection:** Bounce, onceki destek (simdi direnc) seviyesine gelince SHORT
4. **Kural:** Counter-trend trade'lerde position size %50 kucult

### 5.4 Komple Scalping Stratejisi: "Mean Reversion Momentum Scalper"

**LONG Entry:**
1. RSI(7) < 30 (oversold)
2. Fiyat BB alt bandina dokundu veya altta
3. MACD histogram yukselen (momentum donuyor)
4. Fiyat EMA(9) yakininda veya altinda
5. Volume > 1.2x ortalama (dogrulama)

**SHORT Entry:**
1. RSI(7) > 70 (overbought)
2. Fiyat BB ust bandina dokundu veya ustte
3. MACD histogram dusen
4. Fiyat EMA(9) yakininda veya ustunde
5. Volume > 1.2x ortalama

**Exit:**
- TP1: Orta BB (pozisyonun %50'si)
- TP2: Karsi BB (kalan %50)
- SL: Entry'nin %0.3 altinda/ustunde
- Time stop: 3 mum (15dk) — hareket yoksa cik

**Beklenen:** Win Rate %58-65, Sharpe >1.0

---

## 6. MEVCUT SISTEM ANALIZI VE KARSILASTIRMA

### 6.1 Mevcut Config (v4) Performansi

| Metrik | Mevcut | Hedef | Gap |
|--------|--------|-------|-----|
| PnL | -$527 | POZITIF | Negatif |
| Win Rate | %55.7 | %58+ | Yakin ama yetersiz |
| Sharpe | -0.35 | >1.0 | Cok uzak |
| Profit Factor | 0.90 | >1.5 | Yetersiz |
| UP-only PnL | +$320 | — | KARLI! |
| UP-only WR | %61.1 | — | MUKEMMEL |

### 6.2 Ana Sorunlar

1. **DOWN trade'ler ZARAR ediyor** — v4 ile kapatildi (dogru karar)
2. **Entry fiyati hala yuksek** — 0.53-0.55 arasi, 0.40-0.50 arasi daha guvenli
3. **Indikator seti yetersiz** — Sadece RSI(14), MACD kullaniliyor. RSI(7) + BB + Volume LAZIM
4. **Tek timeframe** — Multi-timeframe dogrulama yok
5. **Veri yetersizligi** — 20 mum ile sinyal uretiliyor, 100-250 gerekli
6. **Position sizing** — Quarter Kelly degil, flat fraction kullaniliyor
7. **Binance scalping YOK** — Sadece Polymarket, Binance spot SL/TP ile scalp eklenebilir

### 6.3 Neden UP Trades Karli?

- Piyasalarda genel yukari bias var (crypto genelde yukselen piyasa)
- Mean reversion bounces daha predictable
- Entry fiyat dusuk olunca risk/reward daha iyi
- **Bu avantaji koruyarak gelistirmeliyiz**

---

## 7. ONERILER OZETI

### Oncelik 1: Veri Altyapisi (EN ACIL)
- 1dk + 5dk + 1sa multi-stream WebSocket
- 500 mum rolling buffer (5dk)
- Startup'ta REST backfill

### Oncelik 2: Indikator Guncellemesi
- RSI(14) → RSI(7) (ana) + RSI(14) (dogrulama)
- BB(20,2) eklenmesi (mean reversion)
- Volume(20) eklenmesi (fake signal filtre)
- EMA(9) eklenmesi (momentum)

### Oncelik 3: Sinyal Motoru Yeniden Tasarimi
- Multi-indikator skorlama sistemi
- Her indikator 0-1 arasi skor
- Agirlikli ortalama → final skor
- Edge = final skor - market fiyati

### Oncelik 4: Binance Scalping
- Spot trade (SL/TP ile)
- Mean reversion strategy
- 1:1.5 SL/TP ratio
- %0.3 risk/trade

### Oncelik 5: Position Sizing
- Quarter Kelly (f = edge × 0.25)
- Dinamik — edge buyukse pozisyon buyuk
- Max %2.5 bankroll/trade

---

## KAYNAKLAR

- Gate.io 2026 Scalping Strategy Backtest
- Polymarket Fee Structure Documentation
- Binance API Documentation (klines, WebSocket)
- Academic: "Candlestick Pattern Recognition on 23 Cryptocurrencies" (68 pattern, ~50% accuracy)
- Kelly Criterion academic literature (bet sizing error = quadratic impact)
- Multiple trader forums and educational resources (2025-2026)
