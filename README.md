# Traxon

Polymarket 5-dakika binary crypto prediction botu + multi-agent orkestrasyon sistemi.

## Nedir?

7 coin (BTC, ETH, SOL, XRP, DOGE, BNB, HYPE) için her 5 dakikada bir Polymarket Up/Down binary market'inde otomatik trade açar.

## Nasıl Çalışır? (Loop34 — Güncel)

**Binance-Polymarket Implied Probability Arbitrage**:

1. Binance spot fiyatından Brownian motion ile theoretical up probability hesaplar
2. Polymarket midpoint ile karşılaştırır
3. Fark (edge) > 3 cent ise underpriced tarafa trade açar

Formül:
```
z = (ln(S_t/S_0) + 0.5σ²τ) / (σ√τ)
impliedProbUp = Φ(z)
edge = impliedProbUp - polymarketMid
```

Detaylar: [Loop Tarihçesi](http://localhost:5001/loops) (dashboard çalışırken)

## Mimari

Onion Architecture (Domain → Application → Infrastructure → Worker/Dashboard) + 5 Claude Code agent (Commander, Architect, Developer, Tester, Analyst) orkestrasyonu.

## Başlatma

```bash
# Worker (signal generation + trading)
dotnet run --project src/CryptoTrader/Traxon.CryptoTrader.Worker

# Dashboard (http://localhost:5001)
dotnet run --project src/CryptoTrader/Traxon.CryptoTrader.Dashboard
```

## Dokümantasyon

Dashboard çalışırken:
- `/guide` — Finansal rehber (temel kavramlar)
- `/docs` — Teknik dokümantasyon (mimari, algoritma)
- `/loops` — **34 loop boyunca denenen algoritmalar (en önemli)**
- `/polymarket-setup` — Live trading kurulumu
- `/livepoly-transition` — PaperPoly → LivePoly geçişi
- `/py-clob-client` — Python SDK referansı

## Lisans

Özel proje — dağıtım yok.
