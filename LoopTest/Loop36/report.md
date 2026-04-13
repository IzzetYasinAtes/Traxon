# Loop 36 - Rapor

## Loop35 → Loop36 Geçiş

### Loop35 Fiyaskosu
- 76 trade / 2 saat, %30.3 WR, -$30.57 PnL
- Bakiye negatif ($-0.57) — fatal, erken kapatıldı
- KRITIK BULGU: Drift term edge'i ÖLDÜRDÜ

### Kök Neden
Loop34 edge kaynağı: Brownian `drift=0` varsayımı Polymarket 2-saniye lag'ini yakalıyordu.
Loop35 drift ekleyince formül Polymarket'i taklit etmeye başladı → edge kayboldu.

### Loop36 = Loop34 + 2 İyileştirme (drift YOK)

**1. EWMA Volatility (λ=0.94) — KORUNDU**
Regime change'de hızlı adaptasyon için. Direction'ı etkilemez, sadece σ tahmini.

**2. DOWN Midpoint Fix — KORUNDU**
Polymarket'ten ayrı fetch. Spread nedeniyle `1 - upMid` yanlıştı.

**3. Drift Term — KALDIRILDI**
Formül tekrar `z = (ln(S/S_0) + 0.5σ²τ) / (σ√τ)` (drift=0)

### Core (Aynı)
- Binance-Polymarket implied probability arbitrage
- Edge threshold 0.03
- T+2s giriş
- Position size: MAX(bal × 2%, $1)

## Loop36 Başlangıç
- **Başlangıç**: 13.04.2026 03:46 (TR)
- **Bitiş (planlanan)**: 13.04.2026 11:46 (TR)
- **Engine**: PaperPoly
- **Başlangıç Bakiye**: $30.00

## Loop36 Sonuçları
_(Loop devam ediyor)_
