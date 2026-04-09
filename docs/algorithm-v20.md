# Traxon Signal Algorithm v20

## Overview
5-minute crypto binary prediction for Polymarket. Predicts UP/DOWN direction for BTC, ETH, SOL, XRP, DOGE, BNB, HYPE.

## Data Sources
1. **Binance Spot 1m Candles** (WebSocket): OHLCV + TakerBuyBaseVolume
2. **Binance Futures Order Book** (WebSocket, 500ms): Top 20 levels depth
3. **Binance Futures Funding Rate** (WebSocket, 1s): Real-time funding rate
4. **Binance Futures Open Interest** (REST, 60s polling): Current OI per coin

## Signal Pipeline
Triggered at 5-minute window boundaries (:00, :05, :10, :15, :20, :25, :30, :35, :40, :45, :50, :55).
Entry at T=0+2 seconds after market opens.

### Feature 1: OFI Delta (Order Flow Imbalance Change) — 40% weight
- Recent: TakerBuyBaseVolume / Volume for last 1 bar
- Baseline: TakerBuyBaseVolume / Volume for bars [-4..-1]
- Delta = Recent - Baseline
- Score = Clamp(delta * 15, -1, 1)
- Positive = buyers getting aggressive = UP signal

### Feature 2: VWAP Z-Score — 20% weight
- VWAP = volume-weighted average price over last 60 bars
- Z = (Close - VWAP) / StdDev
- Score = Clamp(-Z / 2.0, -1, 1)
- Above VWAP = overbought = DOWN, below = oversold = UP

### Feature 3: Order Book Imbalance (OBI) — 40% weight
- Weighted top 5 bid/ask levels: weights [1.0, 0.5, 0.25, 0.125, 0.0625]
- OBI = (WeightedBid - WeightedAsk) / (WeightedBid + WeightedAsk)
- Rolling 12-sample average (60 seconds)
- Persistence = how many of 12 samples agree in direction
- Final score = Clamp(avgOBI * persistence * 2.0, -1, 1)

### Composite Score
```
compositeScore = 0.40 * scoreOFI + 0.20 * scoreVWAP + 0.40 * scoreOBI
```

### Modifiers

#### Funding Rate Contrarian Filter
- If |fundingRate| > 0.0005 (extreme):
  - Funding opposite to signal direction -> 1.15x boost
  - Funding same as signal direction -> 0.85x discount

#### Volatility Gate
- Parkinson volatility: last 3 bars / last 10 bars
- volExpansion > 1.5 -> 1.2x boost (conviction move)
- volExpansion < 0.7 -> 0.5x discount (flat/noise)

### Thresholds
- Volume filter: volRatio < 0.3 -> skip dead market
- Minimum |compositeScore| > 0.05
- Direction: score > 0 = UP, score < 0 = DOWN

### Entry
- T=0+2 seconds after Polymarket window opens
- Polymarket midpoint (~$0.50) as entry price
- Position size: MAX(Balance * 2%, $1)

## Fee Structure
- Polymarket taker fee at $0.50: ~$0.018/share
- Breakeven win rate: ~53%
- Target win rate: 55%+ with L2 order book data
