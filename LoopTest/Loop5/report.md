# Loop 5 - Performans Raporu

## Genel Bilgi
- **Baslangic:** 07.04.2026 14:13 (TR) (son restart)
- **Bitis:** 07.04.2026 17:58 (TR)
- **Sure:** ~3.75 saat
- **Engine:** PaperPoly
- **Strateji:** T=0 entry + 2sn bekleme + prev window delta + filtreler

## Altin Kural Uygulamalari
- T=0 entry (market acilisinda gir) — UYGULANDI
- 2sn bekleme (market acildiktan sonra) — UYGULANDI
- Timeout fake loss kaldirildi — UYGULANDI
- Saat filtresi yok — UYGULANDI

## Sonuclar

| Metrik | Loop1 | Loop2 | Loop3 | Loop4 | Loop5 |
|--------|-------|-------|-------|-------|-------|
| Islem | 35 | 29 | 20 | 12 | 12 |
| Kazanc | 14 | 15 | 10 | 3 | 4 |
| Kayip | 21 | 14 | 10 | 9 | 7 |
| Win% | %40 | %52 | %50 | %25 | %36 |
| PnL | -$8.11 | +$0.75 | -$1.80 | -$6.35 | -$2.83 |
| Sinyal/saat | 17.5 | 12 | 5 | 3 | **3** |

## Tum Islemler (TR Saati)

| # | Asset | Yon | Acilis | Kapanis | Sonuc | PnL | Entry |
|---|-------|-----|--------|---------|-------|-----|-------|
| 1 | HYPEUSDT | Up | 14:15:02 | 14:25 | Loss | -$1.04 | 0.42 |
| 2 | XRPUSDT | Down | 14:20:02 | 14:28 | Win | +$1.11 | 0.47 |
| 3 | SOLUSDT | Down | 14:20:02 | 14:29 | Loss | -$1.03 | 0.53 |
| 4 | DOGEUSDT | Down | 14:20:02 | 14:32 | Win | +$1.16 | 0.46 |
| 5 | BNBUSDT | Down | 14:20:02 | 14:31 | Win | +$1.02 | 0.49 |
| 6 | BNBUSDT | Up | 14:40:01 | 14:49 | Loss | -$1.03 | 0.60 |
| 7 | ETHUSDT | Up | 14:40:01 | 14:52 | Loss | -$1.04 | 0.44 |
| 8 | BTCUSDT | Up | 14:40:01 | 14:50 | Loss | -$1.04 | 0.47 |
| 9 | XRPUSDT | Up | 14:40:02 | 14:52 | Loss | -$1.04 | 0.51 |
| 10 | SOLUSDT | Up | 14:40:02 | 14:50 | Loss | -$1.04 | 0.49 |
| 11 | DOGEUSDT | Up | 14:40:02 | — | **STUCK** | — | 0.57 |
| 12 | HYPEUSDT | Up | 14:40:03 | 14:51 | Win | +$1.14 | 0.46 |

## Kritik Sorunlar

### 1. Trade Resolution Bug (STUCK OPEN)
DOGEUSDT Up 3+ saat acik kaldi.
**Kok neden:** GammaApiClient deduplication `.GroupBy().First()` resolved market'i atiyor.
Cozum: `.OrderByDescending(m => m.Closed).ThenByDescending(m => m.ResolvedPrice.HasValue).First()`

### 2. Sinyal Hacmi Cok Dusuk (3/saat vs 40+/saat hedef)
14:40'tan sonra 3+ saat boyunca 0 sinyal.
**Kok neden:** Delta 0.08% + trend + acceleration + volume filtreleri birlikte her seyi olduruyor.
Kullanici: "84 potansiyel sinyal/saat var, hic uretmemek kabul edilemez"

### 3. T=0 Entry Zamanlama
Sinyal :54:59'da uretiliyor (Binance candle boundary). 2sn bekleme eklendi → trade :55:01-02'de aciliyor. CALISYOR.
