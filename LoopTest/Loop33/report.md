# Loop 33 - Rapor

## MATEMATIKSEL KOKLU DEGISIKLIK — 3 Akademik Komponent

### Neden Loop33
30+ loop boyunca denenen "basit feature kombinasyonlari" calismadi. Bu loop akademik literaturden alinmis 3 matematiksel model birlestirir.

### Arastirma Sonuclari (2 paralel agent)
- **Cont-Kukanov-Stoikov 2014**: Multi-level OFI tek feature olarak %56-58 WR, R²=0.65 kisa vadede
- **Bandt-Pompe 2002**: Permutation entropy predictability olcumu, düsük entropi = guvenilir sinyal, WR +%3-5
- **Leung-Li 2015**: Ornstein-Uhlenbeck mean reversion closed-form P(up) formulu
- **GitHub gerceklemeler**: nkaz001/hftbacktest, alpacahq/example-hftish, DeepLOB

### Yeni Algoritma (3 Komponent)

**1. Cont-Kukanov Multi-Level OFI (top 5, 60s rolling)**
```
e_n = I(P_b_n ≥ P_b_{n-1})·q_b_n - I(P_b_n ≤ P_b_{n-1})·q_b_{n-1}
    - I(P_a_n ≤ P_a_{n-1})·q_a_n + I(P_a_n ≥ P_a_{n-1})·q_a_{n-1}
OFI_t = Σ_{n in (t-60s, t]} e_n (top 5 level)
```
- Bizim eski "TakerBuyRatio" YANLIS proxy idi, bu GERCEK formul
- Akademik R²=0.65 short horizon

**2. Permutation Entropy Filter (Bandt-Pompe)**
- Son 20 1dk return'un ordinal pattern entropisi (m=3)
- Normalize [0, 1]
- Esik: > 0.85 → random walk, trade YAPMA
- Esik: < 0.85 → predictability yuksek, trade YAP

**3. Ornstein-Uhlenbeck Mean Reversion**
- Son 60 log-price ile OU fit: dX = θ(μ - X)dt + σ dW
- MLE OLS fit ile θ, μ, σ
- P(up in 5min) = Φ((μ - s_t)(1 - e^(-5θ)) / std)

### Composite
```
composite = 0.60 · OFI_score + 0.40 · OU_score
```
Eger permEntropy ≥ 0.85 → skip (random walk)
Eger |composite| < 0.15 → skip (zayif)
Yoksa: direction = sign(composite), trade

### Hedef WR: %55-60 (akademik gercekci ust sinir)

### Kaldirilanlar
- Tum eski microstructure feature'lar
- VWAP, momentum, BTC lead-lag
- Funding rate, OI modifier, volatility gate
- Follow-Polymarket mantigi

## Loop33 Baslangic
- **Baslangic:** 12.04.2026 16:20 (TR)
- **Bitis (planlanan):** 13.04.2026 00:20 (TR)
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00
- **Loop Suresi:** 8 saat

## Loop33 Sonuclari
_(Loop devam ediyor, sonuclar loop bitiminde eklenecek)_
