# Loop 35 - Rapor

## Loop34 → Loop35 Geçiş

### Loop34 Sonuçları
- 608 trade, %48.5 WR, -$27.63 PnL
- **Peak $48.40** (+$18.40) → sonra tamamen geri verdi
- İlk saat %80 WR, +$20.86 — GERÇEK EDGE
- 16 UTC'de rejim değişimi → %35 WR, -$27.33 (peak yedi)
- SOL Down: -$16.51 (tek başına felaket)

### Kullanıcı Kararı
**"Yapıyı kökten değiştirmicez, bunu iyileştiricez"**
- Loop34 Binance-Polymarket arbitrage = kalıcı core
- Sadece 3 matematiksel iyileştirme uygulandı

### Kod Değişiklikleri (3 iyileştirme)

**1. EWMA Volatility (RiskMetrics λ=0.94)**
```
ESKİ: σ² = (1/n-1) Σ(r_i - μ)²  (60-bar rolling mean variance)
YENİ: σ²_t = λ·σ²_{t-1} + (1-λ)·r²_t  (EWMA recursive)
```
Neden: Regime change'de (örn. 16 UTC) rolling 30-60dk gecikme var. EWMA anında adapte.

**2. Drift Term (Brownian'a μ eklendi)**
```
ESKİ: z = (ln(S/S₀) + 0.5σ²τ) / (σ√τ)  (drift=0 varsayımı)
YENİ: z = (ln(S/S₀) + (μ - 0.5σ²)τ) / (σ√τ)  (full Brownian)
μ = mean(son 15 log return)  (per-minute trend bias)
```
Neden: Polymarket drift'i zaten fiyatlıyor, Brownian'ımız drift=0 varsayımı yanlış.

**3. DOWN Midpoint Fix**
```
ESKİ: marketPrice = 1m - polyMidUp  (yanlış — Polymarket spread var)
YENİ: marketPrice = await _polyClient.GetMidpointAsync(marketDown.RelevantTokenId)
```
Neden: DOWN token'ın gerçek mid'i ≠ (1 - UP mid). Loop34'te DOWN %42 WR bu yüzden.

### Core Değişmedi
- Binance-Polymarket implied probability arbitrage (Benjamin-Cup formülü)
- Edge threshold 0.03 (3 cent)
- T+2s giriş
- Window opening reference price
- Position size: MAX(Bakiye × 2%, $1)

### Beklenen İyileşme
- EWMA: regime change hızlı adaptasyon → 16 UTC benzeri çöküşler azalır
- Drift: UP/DOWN asimetrisi düzelir (Polymarket drift'ini yakalar)
- DOWN fix: DOWN WR %42'den %50+'a çıkmalı

## Loop35 Başlangıç
- **Başlangıç:** 13.04.2026 01:40 (TR)
- **Bitiş (planlanan):** 13.04.2026 09:40 (TR)
- **Engine:** PaperPoly
- **Başlangıç Bakiye:** $30.00
- **Loop Süresi:** 8 saat

## Loop35 Sonuçları
_(Loop devam ediyor)_
