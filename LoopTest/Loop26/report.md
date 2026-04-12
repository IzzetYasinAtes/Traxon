# Loop 26 - Rapor

## Loop25 -> Loop26 Gecis

### Loop25 Sonuclari (erken durduruldu, ~4 saat)
- 282 kapali trade, %51.4 WR, -$10.79 PnL
- Ilk 2 saat mukemmeldi (%58.2, +$13.20), son 2 saat cokus (-$23.99)
- Sqrt scaling + fixed confidence ilk saatlerde calisip sonra etkisini kaybetti

### Veri Analizi Bulgusu (Loop25 verisi)
- EntryPrice < 0.47: %30 WR, -$21.68 — ZARARIN TAMAMI
- EntryPrice >= 0.47: %57.5 WR, +$10.89 — KARLI
- Market midpoint'ten (0.50) uzak fiyatlar = market yonu zaten biliyor, biz gec kaliyoruz
- Edge >= 0.12 filtresi de ayni trade'leri yakaliyor ama EntryPrice daha dogrudan

### Kod Degisiklikleri

**MarketDataWorker.cs — EntryPrice Filter**
- `if (marketPrice < 0.47m) return;` eklendi
- DOWN fiyat duzeltmesinden SONRA, signal generator'dan ONCE
- Cheap token'lara (market yonu cok belli olanlara) girilmeyecek

### GERI ALMA KURALI
Bu degisiklik Loop26'da ise yaramaz ise (WR iyilesmez veya sinyal hacmi cok duserse), Loop27'de geri alinacak ve farkli yaklasim denenecek.

## Loop26 Baslangic
- **Baslangic:** 11.04.2026 12:03 (TR)
- **Bitis (planlanan):** 11.04.2026 20:03 (TR)
- **Engine:** PaperPoly
- **Baslangic Bakiye:** $30.00
- **Loop Suresi:** 8 saat

## Loop26 Sonuclari
_(Loop devam ediyor, sonuclar loop bitiminde eklenecek)_
