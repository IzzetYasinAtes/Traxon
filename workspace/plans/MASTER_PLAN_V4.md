# MASTER PLAN V4 — Komisyon + Gerçek API + Strateji + UI Fix

**Tarih:** 2026-03-31
**Hazırlayan:** Commander Agent
**Kaynak:** Analyst Deep Research Raporu

---

## ÖZET

Paper trading'den gerçek piyasaya geçiş hazırlığı. Sistem paper çalışmaya devam edecek ama:
1. Komisyonlar gerçekçi hesaplanacak
2. Gerçek API bağlantıları hazır olacak (ama paper mode aktif kalacak)
3. Binance stratejisi düzeltilecek (SL/TP + filtreler)
4. Dashboard UI boş alanları düzeltilecek

---

## BRANCH: feature/real-trading-prep

## DEĞİŞİKLİK LİSTESİ

### BÖLÜM 1: KOMİSYON SİMÜLASYONU

#### 1.1 PaperBinanceEngine.cs
**Dosya:** `src/CryptoTrader/Traxon.CryptoTrader.Infrastructure/Engines/PaperBinanceEngine.cs`

- Yeni sabit ekle: `CommissionRate = 0.00075m` (BNB ile taker %0.075)
- OpenPositionAsync: entry price'a komisyon ekle
  ```
  entryPrice = lastCandle.Close * (1 + SlippageRate + CommissionRate)
  ```
- CloseTradeInternalAsync: exit price'dan komisyon düş
  ```
  UP: exitPrice *= (1 - CommissionRate)
  DOWN: exitPrice *= (1 + CommissionRate)
  ```

#### 1.2 PaperPolymarketEngine.cs
**Dosya:** `src/CryptoTrader/Traxon.CryptoTrader.Infrastructure/Engines/PaperPolymarketEngine.cs`

- Yeni sabit ekle: `TakerFeeRate = 0.018m` (Crypto markets max %1.8)
- ResolveTradeAsync: Win durumunda fee düş
  ```
  fee = positionSize * TakerFeeRate
  pnl = (1.00 - entryPrice) / entryPrice * positionSize - fee
  ```

### BÖLÜM 2: BİNANCE STRATEJİ DÜZELTMESİ

#### 2.1 SL/TP Daraltma — PaperBinanceEngine.cs
```
ESKİ:  SlPercent = 0.015m (1.5%), TpPercent = 0.030m (3.0%)
YENİ:  SlPercent = 0.005m (0.5%), TpPercent = 0.010m (1.0%)
```
Risk/ödül oranı 1:2 korunuyor, ama 5m scalping'e uygun hale geliyor.

#### 2.2 MaxHold Kısaltma — PaperBinanceEngine.cs
```
ESKİ:  MaxHoldCandles = 20 (100 dakika)
YENİ:  MaxHoldCandles = 6 (30 dakika)
```

#### 2.3 Sinyal Filtresi Güçlendirme — SignalGenerator.cs
```
ESKİ:  MinBullishConfirmations = 2
YENİ:  MinBullishConfirmations = 3
```

### BÖLÜM 3: GERÇEK API HAZIRLIĞI

#### 3.1 BinanceOptions.cs güncelleme
**Dosya:** `src/CryptoTrader/Traxon.CryptoTrader.Binance/Options/BinanceOptions.cs`

Yeni alanlar ekle:
```csharp
public string TestnetBaseUrl { get; set; } = "https://testnet.binance.vision";
public decimal CommissionRate { get; set; } = 0.00075m;
public bool UseTestnet { get; set; } = false;
```

#### 3.2 appsettings.json — API Key placeholder
**Dosya:** `src/CryptoTrader/Traxon.CryptoTrader.Worker/appsettings.json`

Binance bölümüne ekle:
```json
"Binance": {
  "Enabled": false,
  "ApiKey": "YOUR_BINANCE_API_KEY_HERE",
  "ApiSecret": "YOUR_BINANCE_API_SECRET_HERE",
  "UseTestnet": true,
  "TestnetBaseUrl": "https://testnet.binance.vision",
  "CommissionRate": 0.00075
}
```

Polymarket bölümüne ekle:
```json
"Polymarket": {
  "Enabled": false,
  "ApiKey": "YOUR_POLYMARKET_API_KEY_HERE",
  "ApiSecret": "YOUR_POLYMARKET_API_SECRET_HERE",
  "TakerFeeRate": 0.018
}
```

### BÖLÜM 4: DASHBOARD UI FIX

#### 4.1 CandlestickChart — Parametre değişiminde güncelleme
**Dosya:** `src/CryptoTrader/Traxon.CryptoTrader.Dashboard/Components/Charts/CandlestickChart.razor`

- `OnParametersSetAsync` override et — Symbol/Interval değiştiğinde DB'den veri çek
- `OnAfterRenderAsync`'te parametre değişimini algıla ve chart'ı yeniden render et
- "No candle data" mesajını chart container'ının içine taşı

#### 4.2 RecentTrades — DB'den başlangıç yükleme
**Dosya:** `src/CryptoTrader/Traxon.CryptoTrader.Dashboard/Services/LiveFeedService.cs`

- `IHostedService` implement et (StartAsync/StopAsync)
- StartAsync'te DB'den son 50 trade'i yükle
- DI registration'ı güncelle: `AddHostedService` olarak kaydet

#### 4.3 MarketPage — RecentTrades veri bağlama
**Dosya:** `src/CryptoTrader/Traxon.CryptoTrader.Dashboard/Components/Pages/MarketPage.razor`

- RecentTrades bileşeninin LiveFeedService'ten veri aldığını doğrula
- Gerekirse DB'den doğrudan son trade'leri çek

---

## UYGULAMA SIRASI

1. Feature branch oluştur: `git checkout -b feature/real-trading-prep`
2. Komisyon sabitleri ekle (PaperBinanceEngine + PaperPolymarketEngine)
3. SL/TP + MaxHold değiştir
4. MinBullishConfirmations güncelle
5. BinanceOptions + appsettings güncelle
6. Dashboard CandlestickChart fix
7. Dashboard RecentTrades fix
8. `dotnet build` — 0 hata 0 uyarı
9. Commit + push

---

## TEST KRİTERLERİ

- [ ] dotnet build başarılı
- [ ] Worker başlar, trade açar/kapatır
- [ ] PnL hesabında komisyon görünür (öncekinden düşük kar)
- [ ] Dashboard'da chart güncellenir (symbol değişince)
- [ ] Dashboard'da RecentTrades dolu görünür
- [ ] appsettings'te API key placeholder'ları var
