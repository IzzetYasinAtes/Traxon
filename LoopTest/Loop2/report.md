# Loop 2 - Performans Raporu

## Genel Bilgi
- **Baslangic:** 06.04.2026 21:17 (TR)
- **Bitis:** 07.04.2026 01:47 (TR)
- **Sure:** ~4.5 saat
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $20.00
- **Strateji:** Window Delta + Late Window Trading

## Strateji Degisikligi (Loop1 → Loop2)
| Parametre | Loop1 | Loop2 |
|-----------|-------|-------|
| Sinyal zamani | T=300s (pencere kapanisi) | T=240s (60sn once) |
| Birincil sinyal | Hurst + Z-Score + TakerRatio | Window Delta |
| MinEdge | 0.12 / 0.15 | 0.03 / 0.05 |
| UP penalty | 0.8x | Kaldirildi |
| Confidence | Z-Score/3 | Delta magnitude * 50 |

## Sonuclar

| Metrik | Loop1 | Loop2 | Degisim |
|--------|-------|-------|---------|
| Toplam Islem | 35 | 29 | -6 |
| Kazanc | 14 | 15 | +1 |
| Kayip | 21 | 14 | -7 |
| **Basari Orani** | **%40.0** | **%51.7** | **+11.7 puan** |
| Toplam PnL | -$8.11 | +$0.75 | **+$8.86** |
| Ort PnL/Islem | -$0.23 | +$0.03 | +$0.26 |
| Son Bakiye | $10.89 | $17.46 | +$6.57 |

## Asset Bazli Performans

| Asset | Yon | Islem | W | L | Win% | PnL |
|-------|-----|-------|---|---|------|-----|
| XRPUSDT | Down | 2 | 2 | 0 | %100 | +$2.03 |
| BTCUSDT | Up | 1 | 1 | 0 | %100 | +$1.21 |
| DOGEUSDT | Up | 3 | 2 | 1 | %67 | +$1.10 |
| ETHUSDT | Down | 1 | 1 | 0 | %100 | +$0.98 |
| BNBUSDT | Down | 1 | 1 | 0 | %100 | +$0.96 |
| SOLUSDT | Up | 2 | 1 | 1 | %50 | +$0.05 |
| SOLUSDT | Down | 2 | 1 | 1 | %50 | -$0.05 |
| HYPEUSDT | Up | 2 | 1 | 1 | %50 | -$0.07 |
| DOGEUSDT | Down | 6 | 3 | 3 | %50 | -$0.09 |
| ETHUSDT | Up | 3 | 1 | 2 | %33 | -$1.13 |
| BTCUSDT | Down | 3 | 1 | 2 | %33 | -$1.13 |
| HYPEUSDT | Down | 3 | 0 | 3 | %0 | -$3.11 |

## Saatlik Trend (TR Saati)

| Saat | Trade | W/L | Win% | Not |
|------|-------|-----|------|-----|
| 21:19-21:54 | 8 | 2/6 | %25 | Kotu baslangic |
| 21:59-22:24 | 8 | 7/1 | %88 | Korele Down batch kazandi |
| 22:49-23:29 | 8 | 4/4 | %50 | Stabil |
| 00:04 | 1 | 1/0 | %100 | Son trade |
| (bos saat) | 0 | - | - | PortfolioInsufficient nedeniyle islem yapilamadi |

## Red Edilen Sinyaller

| Sebep | Adet |
|-------|------|
| PortfolioInsufficient | 27 |
| DuplicatePosition | 0 |

## Bulunan ve Duzeltilen Buglar (4 kritik fix)

### 1. Gamma API Lookback (commit 084bf98)
- **Sorun:** 20dk lookback — eski marketler bulunamiyor
- **Cozum:** 180dk paralel HTTP + 30sn cache

### 2. Token ID Restore (commit 3693dfc)
- **Sorun:** Restart sonrasi _tradeToTokenId kayboluyordu
- **Cozum:** EntryReason'dan Token: cikarilarak restore

### 3. 30dk Timeout (commit b842c1b)
- **Sorun:** Gamma resolve edemedigi trade'ler sonsuza kadar acik kaliyordu
- **Cozum:** 30dk'dan eski trade'ler otomatik Loss olarak kapatiliyor

### 4. Portfolio Double-Deduction (commit 58a76ed)
- **Sorun:** snapshot.Balance zaten exposure dusulmus, restart'ta tekrar dusuluyordu
- **Cozum:** InitialBalance + TotalPnL ile unencumbered balance hesaplama
- **Etki:** 27 sinyal PortfolioInsufficient ile reddedildi bu bug yuzunden

## Analiz

### Olumlu
- Win rate %40 → %51.7 — **belirgin iyilesme**
- PnL -$8.11 → +$0.75 — **zaradan kara gecis**
- Window delta stratejisi calisiyor, ozellikle makro hareketlerde (korele batch %88)
- 4 kritik bug bulunup duzeltildi

### Olumsuz
- PortfolioInsufficient 27 sinyal kaybettirdi (gercek win rate muhtemelen daha yuksek)
- HYPEUSDT Down %0 win rate — bu asset/yon kombinasyonu sorunlu
- Ilk 35dk cok kotu (%25) — sistem isinma suresi gerekiyor
- Window delta tek basina %75'e yetmiyor

### Hedef Mesafesi
- Hedef: %75 win rate
- Mevcut: %51.7
- Kalan fark: 23.3 puan
