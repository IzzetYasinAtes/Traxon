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

## Loop34 Sonuclari
_(Loop devam ediyor, sonuclar loop bitiminde eklenecek)_
