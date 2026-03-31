# Traxon Master Research: Gercek Piyasaya Gecis

**Tarih:** 2026-03-31  
**Hazirlayan:** Analyst Agent  
**Durum:** Paper Trading -> Live Trading Gecis Plani

---

## 1. BINANCE FEE YAPISI

### 1.1 Spot Trading Komisyonlari

| VIP Seviyesi | 30-Gun Hacim (USDT) | Min BNB | Maker Fee | Taker Fee | BNB ile Maker | BNB ile Taker |
|---|---|---|---|---|---|---|
| VIP 0 | < 1M | - | 0.1000% | 0.1000% | 0.0750% | 0.0750% |
| VIP 1 | >= 1M | 25 | 0.0900% | 0.1000% | 0.0675% | 0.0750% |
| VIP 2 | >= 5M | 100 | 0.0800% | 0.1000% | 0.0600% | 0.0750% |
| VIP 3 | >= 20M | 250 | 0.0420% | 0.0600% | 0.0315% | 0.0450% |
| VIP 4 | >= 50M | 500 | 0.0360% | 0.0540% | 0.0270% | 0.0405% |
| VIP 5 | >= 150M | 1000 | 0.0250% | 0.0310% | 0.0188% | 0.0233% |
| VIP 9 | >= 150K BTC | 11000 | 0.0200% | 0.0400% | 0.0150% | 0.0300% |

**BNB Indirimi:** BNB ile odeme aktif edildiginde tum fee'lerde %25 indirim uygulanir.

**Not:** Biz VIP 0 seviyesinde olacagiz. BNB ile odeme aktif edilirse: maker %0.075, taker %0.075.

**Kaynak:** [Binance Fee Schedule](https://www.binance.com/en/fee/schedule), [Binance Spot Fee Rate](https://www.binance.com/en/fee/spotMaker)

### 1.2 Withdrawal Fee'leri

| Coin | Network | Withdrawal Fee | Min Withdrawal |
|---|---|---|---|
| BTC | Bitcoin | ~$1.20 | ~$10 |
| BTC | Lightning | ~$0.01 | - |
| ETH | Ethereum | ~$3.20 | - |
| ETH | Arbitrum | ~$0.20 | - |
| SOL | Solana | ~$0.50 | - |
| USDT | TRC20 | ~$1.00 | - |
| USDT | Polygon | ~$0.10 | - |

**Not:** Fee'ler network congestion'a gore degisir. Binance markup eklemez.

**Kaynak:** [Binance Crypto Fee](https://www.binance.com/en/fee/cryptoFee), [ChainFeeTracker](https://chainfeetracker.com/binance-withdrawal-fees.html)

### 1.3 Minimum Order Size ve Lot Size (Canli API Verisi - 2026-03-31)

| Symbol | Min Quantity | Step Size | Tick Size | Min Notional (USDT) |
|---|---|---|---|---|
| BTCUSDT | 0.00001 | 0.00001 | 0.01 | 5.00 |
| ETHUSDT | 0.0001 | 0.0001 | 0.01 | 5.00 |
| SOLUSDT | 0.001 | 0.001 | 0.01 | 5.00 |
| XRPUSDT | 0.1 | 0.1 | 0.0001 | 5.00 |
| DOGEUSDT | 1.0 | 1.0 | 0.00001 | 1.00 |
| BNBUSDT | 0.001 | 0.001 | 0.01 | 5.00 |
| DOTUSDT | 0.01 | 0.01 | 0.001 | 5.00 |

**Desteklenen Order Tipleri (BTCUSDT):** LIMIT, LIMIT_MAKER, MARKET, STOP_LOSS, STOP_LOSS_LIMIT, TAKE_PROFIT, TAKE_PROFIT_LIMIT

**Trailing Stop:** Destekleniyor (TRAILING_DELTA: min 10, max 2000 basis point)

**Kaynak:** Binance REST API `GET /api/v3/exchangeInfo` (canli sorgu, 2026-03-31)

### 1.4 API Rate Limit'leri (Canli Veri)

| Limit Tipi | Aralik | Limit |
|---|---|---|
| REQUEST_WEIGHT | 1 dakika | 6,000 |
| ORDERS | 10 saniye | 100 |
| ORDERS | 1 gun | 200,000 |
| RAW_REQUESTS | 5 dakika | 61,000 |

**Kaynak:** [Binance Rate Limits](https://developers.binance.com/docs/binance-spot-api-docs/rest-api/limits)

---

## 2. POLYMARKET FEE YAPISI

### 2.1 Trading Fee'leri

| Piyasa Kategorisi | Maks Taker Fee (prob=0.50) | Uygulanma Tarihi |
|---|---|---|
| Crypto | %1.80 | Aktif |
| Sports | %0.75 | Aktif |
| Finance | %1.00 | 30 Mart 2026 |
| Politics | %1.00 | 30 Mart 2026 |
| Tech | %1.00 | 30 Mart 2026 |
| Geopolitics | %0.00 (ucretsiz) | - |

**Dinamik Fee Modeli:** Fee, olasiligin %50'ye yakinligina gore degisir. %50'de en yuksek, %0 veya %100'e yaklastikca azalir.

**Maker Rebate Program:** Taker fee'lerinden toplanan USDC, market maker'lara gunluk olarak dagitilir.

**ABD Reguleli Versiyon (CFTC):**
- Taker fee: 30 bps (%0.30)
- Maker rebate: 20 bps (%0.20)

**Deposit/Withdrawal:** Polymarket uzerinde USDC deposit/withdrawal ucretsizdir. Aracilar (Coinbase, MoonPay) kendi ucretlerini alabilir.

**Kaynak:** [Polymarket Fees Docs](https://docs.polymarket.com/trading/fees), [Polymarket Help Center](https://help.polymarket.com/en/articles/13364478-trading-fees), [KuCoin Polymarket Fees 2026](https://www.kucoin.com/blog/polymarket-fees-trading-guide-2026)

---

## 3. BINANCE KARLILIK STRATEJILERI

### 3.1 Mevcut Durum Analizi

**Sorun:** %31 win rate, SL %1.5, TP %3.0 (1:2 R/R), 5m timeframe.

**Neden Zarar Ediyoruz:**

1. **SL/TP cok genis:** 5 dakikalik timeframe icin %1.5 SL ve %3.0 TP cok genis. 5m'de fiyat hareketleri genellikle %0.1-%0.5 arasinda. SL %1.5 demek 5m'de nadiren tetiklenir, fiyat genellikle MaxHold'a ulasiyor ve random kapanisla kayip oluyor.

2. **MaxHold 20 mum = 100 dakika:** Bu surede fiyat genellikle baslangic noktasina yakin kalir. TP %3'e ulasamaz, random kapaniyor.

3. **Sadece UP yonu:** DOWN tamamen kapatilmis (MinBearishConfirmations=99). Bu dogru bir karar cunku DOWN trade'ler zarardaydi, ama piyasa dususte de islem yapilamamasi firsatlari kacirir.

4. **Dusuk filtre esigi:** MinBullishConfirmations=2 cok dusuk. 5 indicator'dan sadece 2'si bullish olunca sinyal uretmek, zayif sinyaller uretir.

5. **Komisyon hesaplanmiyor:** Paper trading'de komisyon yok. Gercekte her trade'de %0.075 (BNB ile) x2 (acilis+kapanis) = %0.15 kayip.

### 3.2 Onerilen Strateji Degisiklikleri

#### A. SL/TP Darlastirma (Oncelik: YUKSEK)
```
Mevcut:  SL = %1.5, TP = %3.0 (1:2 R/R)
Onerilen: SL = %0.3, TP = %0.6 (1:2 R/R) -- scalping icin
Alternatif: SL = %0.5, TP = %1.0 (1:2 R/R) -- orta vade icin
```
**Gerekce:** Arastirmalar 5m scalping icin %0.2-%0.5 SL optimum oldugunu gosteriyor. Dar SL ile daha hizli karar verilir, MaxHold'a ulasmadan pozisyon kapanir.

#### B. MaxHold Kisaltma (Oncelik: YUKSEK)
```
Mevcut:  MaxHold = 20 mum (100 dakika)
Onerilen: MaxHold = 6 mum (30 dakika)
```
**Gerekce:** 5m scalping'de 30 dakika yeterli. Uzun tutmak pozisyonu random noise'a maruz birakir.

#### C. Konfirmasyon Esigini Yukseltme (Oncelik: ORTA)
```
Mevcut:  MinBullishConfirmations = 2 (5 icerisinden)
Onerilen: MinBullishConfirmations = 3 (5 icerisinden)
```
**Gerekce:** Daha secici olmak win rate'i arttirir. Daha az trade ama daha kaliteli.

#### D. Volume Confirmation Zorunlu Kilma (Oncelik: ORTA)
```
Mevcut:  Volume confirmation opsiyonel
Onerilen: Volume.Ratio >= 1.0 zorunlu (ortalama ustunde hacim)
```
**Gerekce:** Hacim olmadan fiyat hareketi surdurulebilir degil.

#### E. Trailing Stop Ekleme (Oncelik: DUSUK)
```
Trailing stop 5m scalping icin UYGUN DEGiL.
Sabit SL/TP daha iyi calisir.
```
**Gerekce:** Arastirmalar trailing stop'un M30+ timeframe'lerde etkili oldugunu, scalping'de ise verimsiz oldugunu gosteriyor.

#### F. Komisyon Ekleme (Oncelik: KRITIK)
```
Paper trading'e komisyon simulasyonu eklenmeli:
- Her acilis: entry_price *= (1 + 0.00075)  -- taker %0.075
- Her kapanis: exit_price *= (1 - 0.00075)  -- taker %0.075
- Toplam maliyet: ~%0.15 per round trip
```

**Kaynak:** [Crypto Scalping Strategy 2025](https://www.calibraint.com/blog/crypto-scalping-strategy-trading), [Scalping SL/TP](https://medium.com/@mintonfin/how-to-scalp-crypto-like-a-pro), [FXOpen Scalping Strategies 2026](https://fxopen.com/blog/en/5-crypto-scalping-strategies/)

### 3.3 Paper Trading vs Gercek: Farklar

| Faktor | Paper Trading | Gercek Trading |
|---|---|---|
| Komisyon | YOK | %0.075 maker/taker (BNB ile) |
| Slippage | Sabit %0.05 | Degisken, yuksek volatilitede artar |
| Order fill | Aninda | Likidite'ye bagli, partial fill olabilir |
| Latency | Yok | API latency + network |
| Minimum order | Yok | $5 minNotional |
| Order reject | Yok | LOT_SIZE, NOTIONAL, rate limit |

---

## 4. GERCEK API ENTEGRASYONU

### 4.1 Binance .NET SDK

**Paket:** `Binance.Net` (JKorf/Binance.Net)
- **NuGet:** https://www.nuget.org/packages/Binance.Net
- **GitHub:** https://github.com/JKorf/Binance.Net
- **Son Versiyon:** 12.10.0 (Mart 2026)
- **Framework:** .NET Standard - .NET 8+ uyumlu

**Ozellikler:**
- REST + WebSocket API tam destek
- Automatic WebSocket reconnection
- Client-side rate limiting
- Order book syncing
- Ed25519 signing destegi (v12.1.0+)
- Strongly typed models ve enum'lar

**Kullanim Ornegi:**
```csharp
// REST Client
var restClient = new BinanceRestClient(opts => {
    opts.ApiCredentials = new ApiCredentials("key", "secret");
});
var ticker = await restClient.SpotApi.ExchangeData.GetTickerAsync("ETHUSDT");

// WebSocket Client
var socketClient = new BinanceSocketClient();
await socketClient.SpotApi.ExchangeData
    .SubscribeToTickerUpdatesAsync("ETHUSDT", data => {
        Console.WriteLine(data.Data.LastPrice);
    });
```

**Mevcut Entegrasyon Durumu:** Projede `BinanceEngine.cs` ve `IBinanceOrderService` zaten mevcut. Binance.Net paketi kullaniliyor. `BinanceOptions.Enabled=false` ile deaktif.

### 4.2 Polymarket SDK

**Resmi SDK'lar:**
- **TypeScript:** `@polymarket/clob-client` (npm)
- **Python:** `py-clob-client` (PyPI)
- **Rust:** `rs-clob-client` (crates.io)
- **.NET:** Resmi .NET SDK YOK

**Mevcut Entegrasyon:** Projede `PolymarketEngine.cs`, `IPolymarketClient`, `IMarketDiscoveryService` interface'leri var. Custom HTTP client implementasyonu kullaniliyor.

**CLOB API Detaylari:**
- REST endpoints + WebSocket streams
- Rate limit: 15 order/batch (2025 guncellemesi)
- Ed25519 authentication
- GTC (Good-Till-Cancel) ve GTD (Good-Till-Date) order tipleri
- Maker rebate programi aktif

**Kaynak:** [Polymarket Docs](https://docs.polymarket.com/), [Polymarket py-clob-client](https://github.com/Polymarket/py-clob-client), [Polymarket US API](https://www.quantvps.com/blog/polymarket-us-api-available)

### 4.3 Testnet vs Mainnet

**Binance Testnet:**
- URL: `https://testnet.binance.vision`
- Sanal bakiye ile gercek API davranisi
- Tam order matching simulation
- WebSocket stream'leri mevcut
- **Oneri:** Gercege gecmeden once 1 hafta testnet'te calistirilmali

**Binance Mainnet:**
- URL: `https://api.binance.com`
- API Key: Binance hesabindan olusturulur
- IP whitelist zorunlu (guvenlik icin)
- 2FA + withdrawal password gerekli

### 4.4 Order Tipleri

| Tip | Aciklama | Kullanim |
|---|---|---|
| MARKET | Aninda fiyattan islem | Hizli giris/cikis |
| LIMIT | Belirlenen fiyattan islem | Daha iyi fiyat, fill garanti degil |
| STOP_LOSS | Fiyat esige ulasinca MARKET | SL otomasyonu |
| STOP_LOSS_LIMIT | Fiyat esige ulasinca LIMIT | SL + fiyat kontrolu |
| TAKE_PROFIT | Fiyat esige ulasince MARKET | TP otomasyonu |
| TAKE_PROFIT_LIMIT | Fiyat esige ulasinca LIMIT | TP + fiyat kontrolu |

**Oneri:** Giris icin LIMIT (maker fee daha dusuk), SL/TP icin STOP_LOSS_LIMIT kullanilmali.

---

## 5. KOMISYON EKLEME NOKTALARI

### 5.1 PaperBinanceEngine.cs Degisiklikleri

**Dosya:** `src/CryptoTrader/Traxon.CryptoTrader.Infrastructure/Engines/PaperBinanceEngine.cs`

**Eklenmesi Gereken Sabitler:**
```csharp
private const decimal CommissionRate = 0.00075m; // %0.075 BNB ile taker fee
```

**Degisiklik 1 - OpenPositionAsync (satir 139):**
```csharp
// MEVCUT:
var entryPrice = lastCandle.Close * (1 + SlippageRate);

// OLMASI GEREKEN:
var entryPrice = lastCandle.Close * (1 + SlippageRate + CommissionRate);
```

**Degisiklik 2 - CloseTradeInternalAsync (satir 328-331):**
```csharp
// MEVCUT:
decimal rawPnl = trade.Direction == SignalDirection.Up
    ? (exitPrice - trade.EntryPrice) / trade.EntryPrice * trade.PositionSize
    : (trade.EntryPrice - exitPrice) / trade.EntryPrice * trade.PositionSize;

// OLMASI GEREKEN:
var exitAfterCommission = trade.Direction == SignalDirection.Up
    ? exitPrice * (1 - CommissionRate)
    : exitPrice * (1 + CommissionRate);
decimal rawPnl = trade.Direction == SignalDirection.Up
    ? (exitAfterCommission - trade.EntryPrice) / trade.EntryPrice * trade.PositionSize
    : (trade.EntryPrice - exitAfterCommission) / trade.EntryPrice * trade.PositionSize;
```

### 5.2 BinanceEngine.cs Degisiklikleri

**Dosya:** `src/CryptoTrader/Traxon.CryptoTrader.Binance/Adapters/BinanceEngine.cs`

**Sorun:** BinanceEngine gercek order gonderiyor ama komisyonu PnL hesabina dahil etmiyor. Gercek Binance zaten fee alacak, ancak PnL tracking icin bu fee'nin hesaba katilmasi lazim.

**Degisiklik - CloseTradeInternalAsync (satir 277-279):**
```csharp
// Komisyon dahil PnL hesabi
var totalCommission = (trade.PositionSize * CommissionRate) + (trade.PositionSize * CommissionRate);
decimal rawPnl = trade.Direction == SignalDirection.Up
    ? (exitPrice - trade.EntryPrice) / trade.EntryPrice * trade.PositionSize - totalCommission
    : (trade.EntryPrice - exitPrice) / trade.EntryPrice * trade.PositionSize - totalCommission;
```

### 5.3 PaperPolymarketEngine.cs Degisiklikleri

**Dosya:** `src/CryptoTrader/Traxon.CryptoTrader.Infrastructure/Engines/PaperPolymarketEngine.cs`

**Eklenmesi Gereken Sabitler:**
```csharp
private const decimal TakerFeeRate = 0.018m; // Crypto markets max %1.8 (prob=0.50'de)
```

**Degisiklik - ResolveTradeAsync (satir 250-251):**
```csharp
// Fee hesabi: giriş ve çıkışta taker fee
var fee = trade.PositionSize * TakerFeeRate;

if (isWin)
{
    outcome   = TradeOutcome.Win;
    exitPrice = 1.00m;
    pnl       = (1.00m - trade.EntryPrice) / trade.EntryPrice * trade.PositionSize - fee;
}
else
{
    outcome   = TradeOutcome.Loss;
    exitPrice = 0.00m;
    pnl       = -trade.PositionSize; // fee zaten kayipta onemli degil
}
```

### 5.4 SignalGenerator.cs Potansiyel Degisiklikleri

**Dosya:** `src/CryptoTrader/Traxon.CryptoTrader.Infrastructure/Signals/SignalGenerator.cs`

**Degisiklik 1 - SL/TP Darlastirma (PaperBinanceEngine icin):**
```
Satir 36-37:
MEVCUT:  SlPercent = 0.015m, TpPercent = 0.030m
ONERILEN: SlPercent = 0.005m, TpPercent = 0.010m (PaperBinanceEngine.cs icinde)
```

**Degisiklik 2 - MinBullishConfirmations yukseltme:**
```
Satir 28:
MEVCUT:  MinBullishConfirmations = 2
ONERILEN: MinBullishConfirmations = 3
```

**Degisiklik 3 - Volume filter ekleme:**
```
Generate() method'una eklenmeli:
if (indicators.Volume is null || indicators.Volume.Ratio < 1.0m)
    return Result<Signal>.Failure(Error.InsufficientVolume);
```

---

## 6. DASHBOARD UI FIX NOKTALARI

### 6.1 Candlestick Chart Sorunu

**Belirtiler:** Chart alaninda sadece 1-2 mum goruntuleniyor, buyuk alan bos.

**Kök Neden Analizi:**

1. **Veri Yetersizligi:** Chart `Take(200)` ile son 200 mumu cekiyor ama DB'de az mum olabilir. Sistem yeni basladiysa sadece birkaç mum vardir.

2. **Render Timing Sorunu:** `CandlestickChart.razor` (satir 48-54) `OnAfterRenderAsync` ile chart'i cizer ama bu sadece `firstRender`'da calisir. Eger `_candleDataJson` sonradan doluyorsa chart guncellenmez.

3. **Chart Container Boyutu:** `el.parentElement.offsetWidth` ile boyutlandiriliyor. Eger parent element henuz render olmamissa boyut 0 olabilir.

4. **LightweightCharts.Blazor NuGet + Custom JS Cakismasi:** App.razor'da hem `LightweightCharts.Blazor` NuGet paketi hem de custom `candlestick-chart.js` yukleniyor. Bu iki kutuphane cakisabilir veya `LightweightCharts` global degiskeni farkli bir namespace'de olabilir.

**Dosya:** `src/CryptoTrader/Traxon.CryptoTrader.Dashboard/Components/Charts/CandlestickChart.razor`

**Cozum 1 - Chart guncelleme:** Symbol veya interval degistiginde chart yeniden render edilmeli.
```razor
@code {
    private string _prevKey = "";
    
    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        var key = $"{Symbol}_{Interval}";
        if (_candleDataJson is not null && (firstRender || key != _prevKey))
        {
            _prevKey = key;
            await JS.InvokeVoidAsync("traxonCandlestickChart.render", _chartId, _candleDataJson, Height);
        }
    }
}
```

**Cozum 2 - Parameter degisiminde veri yeniden yukle:**
```razor
protected override async Task OnParametersSetAsync()
{
    await using var db = await DbFactory.CreateDbContextAsync();
    var candles = await db.Candles
        .Where(c => c.Asset.Symbol == Symbol && c.TimeFrame.Value == Interval)
        .OrderByDescending(c => c.OpenTime)
        .Take(200)
        .ToListAsync();
    candles.Reverse();

    _candleDataJson = candles.Count > 0
        ? System.Text.Json.JsonSerializer.Serialize(candles.Select(c => new { ... }))
        : null;
}
```

**Cozum 3 - "No candle data" mesaji duzeltme:**
Satir 9-14'te "No candle data" div'i chart container'inin DISINDA. Eger veri varsa bile mesaj gosterilmiyor ama layout kaymasi olabilir. Mesaj chart div'inin ICINDE olmali:
```razor
<div style="width:100%; height:@(Height)px; ...">
    <div id="@_chartId" style="width:100%; height:100%;"></div>
    @if (_candleDataJson is null)
    {
        <div style="position:absolute; ...">No candle data available</div>
    }
</div>
```

### 6.2 RecentTrades Tablosu Sorunu

**Belirtiler:** Dashboard ana sayfasinda (MarketPage) RecentTrades bos goruntuleniyor.

**Kök Neden Analizi:**

1. **In-Memory Data:** `LiveFeedService.RecentTrades` sadece runtime'da uretilen trade'leri tutar. Uygulama restart sonrasi in-memory liste bos baslar.

2. **DB'den Yukleme Yok:** `LiveFeedService` baslangiçta DB'den gecmis trade'leri yuklemez. Sadece `PublishTradeOpened` ve `PublishTradeClosed` cagrildiginda dolar.

**Dosya:** `src/CryptoTrader/Traxon.CryptoTrader.Dashboard/Services/LiveFeedService.cs`

**Cozum - Baslangicta DB'den yukle:**
```csharp
public sealed class LiveFeedService : ILiveFeedService, IMarketEventPublisher, IHostedService
{
    private readonly IServiceProvider _sp;
    
    public async Task StartAsync(CancellationToken ct)
    {
        using var scope = _sp.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        
        var recentTrades = await db.Trades
            .OrderByDescending(t => t.OpenedAt)
            .Take(50)
            .ToListAsync(ct);
        
        lock (_tradeLock)
        {
            foreach (var t in recentTrades.OrderBy(t => t.OpenedAt))
                _trades.Add(MapToDto(t));
        }
    }
    
    public Task StopAsync(CancellationToken ct) => Task.CompletedTask;
}
```

### 6.3 Chart Symbol/Interval Degisiminde Guncellenmeme

**Belirtiler:** BTC dropdown'dan ETH'ye gecildiginde chart guncellenmiyor.

**Kök Neden:** `CandlestickChart.razor` sadece `OnInitializedAsync` ile veri yukluyor. `Symbol` ve `Interval` parameter degistiginde yeniden sorgu yapmiyor.

**Cozum:** `OnParametersSetAsync` override edilmeli ve parametre degisiminde veri + chart yeniden render edilmeli.

---

## 7. ONERILEN DEGISIKLIKLER LISTESI (Oncelik Sirasina Gore)

### KRITIK (Gercek Piyasa Oncesi Zorunlu)

| # | Degisiklik | Dosya | Etki |
|---|---|---|---|
| 1 | Paper engine'lere komisyon simulasyonu ekle | PaperBinanceEngine.cs, PaperPolymarketEngine.cs | Gercekci PnL hesabi |
| 2 | SL/TP darlastir (SL %0.5, TP %1.0) | PaperBinanceEngine.cs | Win rate artisi |
| 3 | MaxHold 20 -> 6 mum kisalt | PaperBinanceEngine.cs | Daha az random kapanış |
| 4 | Binance ExchangeInfo'dan lot/tick size validasyonu | BinanceEngine.cs | Order reject onleme |
| 5 | BinanceOptions'a Testnet URL, CommissionRate ekle | BinanceOptions.cs | Testnet desteği |

### YUKSEK (Ilk Hafta Icinde)

| # | Degisiklik | Dosya | Etki |
|---|---|---|---|
| 6 | MinBullishConfirmations 2 -> 3 | SignalGenerator.cs | Daha kaliteli sinyaller |
| 7 | Volume confirmation zorunlu | SignalGenerator.cs | False signal azaltma |
| 8 | CandlestickChart parameter degisim destegi | CandlestickChart.razor | UI fix |
| 9 | RecentTrades DB'den baslangic yukleme | LiveFeedService.cs | UI fix |
| 10 | BinanceEngine'e komisyon tracking ekle | BinanceEngine.cs | Dogru PnL raporlama |

### ORTA (Ilk Ay Icinde)

| # | Degisiklik | Dosya | Etki |
|---|---|---|---|
| 11 | Binance STOP_LOSS_LIMIT order destegi | BinanceEngine.cs | Otomatik SL |
| 12 | Polymarket fee'yi dinamik hesapla | PaperPolymarketEngine.cs | Gercekci simulasyon |
| 13 | IP whitelist + 2FA dokumantas | - | Guvenlik |
| 14 | Risk yonetimi: gunluk max kayip limiti | Portfolio.cs | Risk kontrolu |
| 15 | 1h trend dogrulama V2 engine'i default yap | Worker/TradingWorker | Trend tasdiki |

### DUSUK (Gelecek Sprints)

| # | Degisiklik | Dosya | Etki |
|---|---|---|---|
| 16 | OrderBook widget (real-time depth) | MarketPage.razor | UI zenginlik |
| 17 | Polymarket WebSocket resolution | PolymarketEngine.cs | Otomatik resolution |
| 18 | Multi-timeframe analiz (5m + 15m + 1h) | SignalGenerator.cs | Daha iyi sinyaller |
| 19 | Binance Futures destegi | Yeni proje | Kaldiraçli islem |
| 20 | BNB oto-satin al (fee indirimi icin) | BinanceEngine.cs | Maliyet optimizasyonu |

---

## 8. GERCEGE GECIS YOLHARITASI

### Faz 1: Paper Trading Iyilestirme (1 hafta)
1. Komisyon simulasyonu ekle
2. SL/TP darlastir
3. MaxHold kisalt
4. Sinyal filtreleri guclendir
5. 1 hafta paper trade ile performans olc

### Faz 2: Testnet (1 hafta)
1. BinanceOptions'a Testnet URL ekle
2. Binance Testnet'te gercek API ile test
3. Order fill, partial fill, reject senaryolari
4. Rate limit davranisini test et
5. ExchangeInfo validasyonu implement et

### Faz 3: Kucuk Bakiye ile Canli (2 hafta)
1. Binance hesabina $100 yatir
2. AllowedSymbols: sadece BTCUSDT
3. MaxPositionSize: $20
4. BNB al (fee indirimi icin)
5. 2 hafta izle, PnL analiz et

### Faz 4: Tam Canli (devam)
1. Bakiyeyi $1000'e cikar
2. Tum semboller ac
3. MaxPositionSize: $100
4. Gunluk raporlama sistemi kur
5. Risk limitleri implement et

---

## KAYNAKLAR

### Binance
- [Binance Fee Schedule](https://www.binance.com/en/fee/schedule)
- [Binance Spot Fee Rate](https://www.binance.com/en/fee/spotMaker)
- [Binance Trading Rules](https://www.binance.com/en/trade-rule)
- [Binance API Docs](https://developers.binance.com/docs/binance-spot-api-docs)
- [Binance Rate Limits](https://developers.binance.com/docs/binance-spot-api-docs/rest-api/limits)
- [Binance Testnet](https://developers.binance.com/docs/binance-spot-api-docs/testnet)
- [Binance Crypto Fees](https://www.binance.com/en/fee/cryptoFee)
- [Binance.Net GitHub](https://github.com/JKorf/Binance.Net)
- [Binance.Net NuGet](https://www.nuget.org/packages/Binance.Net)
- [Binance Order Filters](https://developers.binance.com/docs/binance-spot-api-docs/filters)

### Polymarket
- [Polymarket Docs](https://docs.polymarket.com/)
- [Polymarket Fees](https://docs.polymarket.com/trading/fees)
- [Polymarket Help - Trading Fees](https://help.polymarket.com/en/articles/13364478-trading-fees)
- [Polymarket py-clob-client](https://github.com/Polymarket/py-clob-client)
- [Polymarket US API Unblocked](https://www.quantvps.com/blog/polymarket-us-api-available)
- [KuCoin Polymarket Fees 2026](https://www.kucoin.com/blog/polymarket-fees-trading-guide-2026)

### Strateji Kaynaklari
- [Crypto Scalping Strategy 2025](https://www.calibraint.com/blog/crypto-scalping-strategy-trading)
- [Scalp Crypto Like a Pro 2025](https://medium.com/@mintonfin/how-to-scalp-crypto-like-a-pro)
- [5 Scalping Crypto Strategies 2026](https://fxopen.com/blog/en/5-crypto-scalping-strategies/)
- [CryptoTrade Win Rate](https://www.walletfinder.ai/blog/crypto-trade-win-rate-estimator)
- [Profit Factor in Crypto](https://www.altrady.com/blog/risk-management/profit-factor-crypto)
- [Binance 5min Scalping](https://www.binance.com/en/square/post/24799294275521)
- [CryptoPotato Binance Fees](https://cryptopotato.com/binance-fees/)
- [BitDegree Binance Fees 2026](https://www.bitdegree.org/crypto/tutorials/binance-fees)
