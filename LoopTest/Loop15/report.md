# Loop 15 - Rapor

## Loop14 -> Loop15 Gecis Arastirmasi

### Loop14 Sonuclari (Basarisiz)
- 91 trade, %47.3 win rate, -$7.79 PnL
- BTC %36 (Loop13'te %52 idi) — convergence scoring BTC'yi bozdu
- SOL %60 (Loop13'te %35) — adaptive threshold ise yaradi
- Sinyal hacmi 11/saat (Loop13'te 40/saat) — filtreler cok siki

### Arastirma Kaynaklari
- Liu (2026) - "AI-Augmented Arbitrage in Polymarket 5-Min BTC Binary Options" — live %25-27 win rate vs paper %522x
- Wen et al. (2022) - "Intraday return predictability in cryptocurrency markets: Momentum, reversal, or both" — 5dk'da mean reversion baskin
- Hua et al. (Oxford Bioinformatics) - "Optimal number of features as function of sample size" — kucuk orneklemde 2-5 feature optimal
- QuantifiedStrategies - "Simple vs Complex Trading Strategies" — basit stratejiler complex'i yener
- Benjamin-Cup (2026) - "Unlocking Edges in Polymarket's 5-Minute Crypto Markets"
- Oracle Lag Sniper Bot (GitHub) — T-10s girisinde %61.4 win rate
- Polymarket fee analysis — $0.50 giriste breakeven %53

### Arastirma Bulgulari (KRITIK)

1. **Basitlik kazanir**: Kendi verilerimiz kanit:
   - Loop8 (basit weighted scoring): **%53** — EN IYI
   - Loop13 (autocorrelation eklendi): %50
   - Loop14 (convergence + RSI + doji + MTF + adaptive): **%47** — EN KOTU
   - Her eklenen katman performansi DUSURDU

2. **UP bias**: Polymarket'te flat candle (close >= open) = UP olarak resolve ediliyor.
   5dk'lik candle'larin %50.5-51.5'i yesil. "Her zaman UP oyna" stratejisi bile %51.

3. **Feature sayisi**: Kucuk orneklemde (200-500 trade) optimal feature sayisi 2-5.
   Loop14'te 7+ feature + 10 filtre = asiri overfitting.

4. **Breakeven %53**: $0.50 giriste Polymarket fee + spread = %53 win rate gerek.
   %47 ile her trade'de ortalama -$0.09 kaybediyoruz.

5. **OFI Delta en guclu**: Akademik literaturde kisa vadeli crypto tahmininde
   OFI DEGISIMI (seviye degil) en guclu tekli ozellik. %55-58 tek basina.

### Kod Degisiklikleri

**Dosya: `MarketDataWorker.cs` — TryGenerateAndDispatchSignalAsync tamamen yeniden yazildi**

KALDIRILAN (Loop14'ten):
- EWMA Autocorrelation (agir hesaplama, BTC'de ters cikti)
- Volume confirmation multiplier (0.6x-1.3x)
- RSI continuous multiplier (0.85x-1.25x)
- Doji filtresi (0.5x)
- Strong candle confirmation (0.75x-1.15x)
- Convergence scoring (0.6x-1.5x) — EN COK ZARARI VEREN
- Multi-timeframe SMA alignment (3/4 gereksinimi)
- Adaptive threshold per asset
- Per-asset autocorrelation gate
- Per-asset BTC lag

EKLENEN/DEGISTIRILEN:
1. **OFI Delta** (%60 agirlik): Son 2 bar vs onceki 5 bar TakerBuy orani FARKI
   - ofiDelta = ofiRecentRatio - ofiBaselineRatio
   - scoreOFI = clamp(ofiDelta * 12, -1, 1)

2. **VWAP Z-Score** (%30 agirlik): 60-bar volume-weighted ortalama fiyattan sapma
   - Pozitif Z (VWAP ustu) = overbought = DOWN bekle
   - Negatif Z (VWAP alti) = oversold = UP bekle
   - scoreVWAP = clamp(-vwapZ / 2.0, -1, 1)

3. **UP Bias** (+0.05 sabit): Polymarket'te %51+ candle yesil, belirsizde UP'a yaklas

4. **Volume filtresi**: Sadece olum piyasalari atla (volRatio < 0.3), sinyal olarak KULLANMA

5. **Tek sabit threshold**: 0.06 — tum coinler esit, per-asset ayrimi YOK

6. **effectiveDelta = compositeScore / 3.0** (onceki /4.0) — daha guclu FairValue farki

## Loop15 Sonuclari

| Metrik | Deger |
|--------|-------|
| Kapali Islem | 175 |
| Win/Loss | 91W / 84L |
| Win Rate | **%52** |
| Net PnL | **+$2.16** |
| Baslangic Bakiye | $30.00 |
| Ort Win | +$0.98 |
| Ort Loss | -$1.04 |

### Onemli Bulgular
1. Basitlik yaklasimi (2 feature) onceki karmasik loop'lardan daha iyi performans gosterdi
2. %52 win rate, breakeven (%53) sinirinda — fee asimetrisi (win +$0.98 vs loss -$1.04) kar yiyor
3. Dashboard'da InitialBalance hesaplama bug'i tespit edildi ve duzeltildi
4. HYPE %38 ile en kotu performans gosteren coin
