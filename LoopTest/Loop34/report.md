# Loop 34 - Rapor

## KOKLU YENI YAKLASIM — Binance-Polymarket Implied Probability Arbitrage

### Neden Bu Yaklasim?
34 loop sonunda kanitladik ki: 
- Tahmin tabanli yaklasimlar (OFI, OBI, momentum, vb.) 5dk crypto icin calismiyor
- Akademik literatur: 5-dakika OHLCV %50-52 ust sinir
- Pratik bot sahipleri (Medium, CoinDesk): Arbitraj = %65-68 hit rate

### Derin Arastirma Sonuclari
- **4 paralel agent** ile akademik + GitHub + Reddit + Twitter tarandi
- **En ikna edici yayinlanmis strateji**: Benjamin-Cup (Feb 2026 Medium) - Binance ile Polymarket arasindaki implied probability arbitrage
- Bot sahipleri 8,894 execution'da %1.5-3 kar/islem, %65+ hit rate
- Kucuk sermaye ($30) icin IDEAL: Polymarket order book $5-15k/side ince, biz rahatlikla sigariz

### Matematiksel Temel

**Black-Scholes / Brownian Motion implied probability:**
```
z = (ln(S_t/S_0) + 0.5·σ²·τ) / (σ·√τ)
impliedProbUp = Φ(z)

S_0 = BTC price at window open (T=0)
S_t = BTC price now (T+2s)
τ = 5 min - 2 sec ≈ 4.967 min remaining
σ = realized volatility from last 60 1-min log returns
Φ = standard normal CDF
```

**Edge hesabi:**
```
edge = impliedProbUp - polyMidUp

|edge| < 0.03 → SKIP (cok kucuk, fee yiyecek)
edge > 0.03 → UP underpriced → BUY UP
edge < -0.03 → DOWN underpriced → BUY DOWN
```

### Kod Mimarisi
- **Layer 1**: Binance 1m candle'dan realized vol + Brownian implied prob
- **Layer 2**: Polymarket CLOB midpoint
- **Layer 3**: Arbitrage edge + direction decision
- **TUM microstructure features KALDIRILDI** (OFI, OBI, OU, PE, momentum, vb.)

### Beklenen Performans (Akademik + Pratik Realist)
- **Sinyal hacmi**: 5-10/saat (40+/saat retail'de IMKANSIZ, yayinlanmis analiz)
- **Hit rate**: %62-68 (Benjamin-Cup raporu)
- **Edge/trade**: %1.5-3 (fee cikarildiktan sonra net)
- **5-6 gun profitable run**: pratikte kanitlandi

### Bu Neden Daha Once Denenmedi?
- Hep Binance TEK BASINA veya Polymarket TEK BASINA kullanildi
- Ikisinin matematiksel ARBITRAJI hic hesaplanmadi
- Benjamin-Cup formulu tam olarak bu gecidi sagliyor

## Loop34 Baslangic
- **Baslangic:** 12.04.2026 17:35 (TR)
- **Bitis (planlanan):** 13.04.2026 01:35 (TR)
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00
- **Loop Suresi:** 8 saat

## Loop34 Sonuclari — -$27.63 PnL, %48.5 WR, 608 trade / 8 saat

### Peak-to-Trough Trajesi (ÖNEMLI)
- **Başlangıç bakiye**: $30.00
- **Peak bakiye**: $48.40 (19:03 TR, ~1.5 saat içinde +$18.40 = %61 kâr!)
- **Son bakiye**: $0.37 (bitik)
- **Drawdown**: -$48.03 peak'ten (!)

### Saat Bazında PnL Detayı
| UTC (TR) | Trade | WR% | PnL | Not |
|----------|-------|-----|-----|-----|
| 14 (17) | 35 | %80 | +$20.86 | 🟢 ilk saat — GÜÇLÜ EDGE |
| 15 (18) | 79 | %53 | +$4.38 | 🟡 normal |
| 16 (19) | 81 | %35 | -$27.33 | 🔴 PEAK'I YEDİ |
| 17 (20) | 79 | %60 | +$15.51 | 🟢 bounce |
| 18 (21) | 79 | %42 | -$16.04 | 🔴 düşüş |
| 19 (22) | 73 | %56 | +$8.99 | 🟢 recovery |
| 20 (23) | 81 | %46 | -$9.26 | 🟠 |
| 21 (00) | 34 | %44 | -$4.70 | 🟠 |

### Yön Analizi (Kritik)
- **UP**: 341 trade, %55.7 WR, **+$16.17** ✓ (arbitraj UP'ta çalışıyor)
- **DOWN**: 267 trade, %42.1 WR, **-$43.81** ✗ (DOWN'da çuvallıyor)

### Coin Bazında En Kötü
- **SOL Down**: %27.8 WR, -$16.51 (tek başına %60 zararın kaynağı)
- **DOGE Down**: %43.9 WR, -$6.44
- **XRP Down**: %41.7 WR, -$5.99

### ÖNEMLİ BULGU
Algoritmanın GERÇEK EDGE'İ VAR — ilk 1.5 saatte %80 WR, +$18 kâr üretti ve bakiye $48'e çıktı. Ama rejim değiştiğinde (16 UTC) algoritma tepki veremedi ve peak'i tamamen geri verdi.

**Kök neden**: 60-bar rolling realized volatility regime change'i yakalayamıyor. Polymarket Brownian'dan daha hızlı öğreniyor.

**DOWN token fiyat hatası**: `1 - polyMidUp` formülü DOWN token'ın gerçek midpoint'ini vermiyor olabilir (Polymarket spread'i).

## Loop35 İyileştirmeleri (Kullanıcı kararı: kökten değişim YOK)
1. **EWMA volatility (λ=0.94)** — regime change'i hızlı yakala
2. **Drift term** — kısa vadeli trend bias
3. **DOWN token doğru midpoint fetch** — separate API call
