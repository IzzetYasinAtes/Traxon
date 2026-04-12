# Traxon Signal Algorithm — Loop34 (Current)

## Overview
5-minute crypto binary prediction for Polymarket via Binance-Polymarket implied probability arbitrage.

Predicts UP/DOWN based on price mispricing, not directional forecasting.

Coins: BTC, ETH, SOL, XRP, DOGE, BNB, HYPE.

## Academic Foundation
- Black-Scholes (1973) — option pricing under geometric Brownian motion
- Reiner-Rubinstein (1991) — one-touch options
- Benjamin-Cup (Medium, Feb 2026) — published Polymarket arbitrage strategy

## Data Sources
1. **Binance Spot 1m Candles** (WebSocket) — OHLCV
2. **Polymarket CLOB** (REST) — midpoint at T+2s
3. **Polymarket Gamma API** — market discovery + resolution

## Signal Pipeline

Triggered at 5-minute window boundaries (:00, :05, :10, :15, ...). Entry at T+2s.

### Step 1: Realized Volatility
From last 60 1-minute log returns:
```
r_t = ln(close_t / close_{t-1})
μ = mean(r_t)
σ² = (1/n-1) Σ (r_t - μ)²
σ_per_minute = √σ²
```

### Step 2: Window Reference Price
`S_0` = Open price of the 5-minute window's first 1m candle (minute % 5 == 0).

### Step 3: Brownian Implied Probability
Under geometric Brownian motion with zero drift:
```
z = (ln(S_t/S_0) + 0.5·σ²·τ) / (σ·√τ)
impliedProbUp = Φ(z)
```

Where:
- `S_t` = current spot (T+2s)
- `τ` = 5 - 2/60 ≈ 4.967 minutes remaining
- `Φ` = standard normal CDF (Abramowitz-Stegun approximation)

### Step 4: Polymarket Comparison
```
polyMidUp = Polymarket UP token midpoint (from CLOB /midpoint)
edge = impliedProbUp - polyMidUp
```

### Step 5: Trade Decision
```
if |edge| < 0.03: SKIP (below 3-cent threshold, fee eats profit)
if edge > 0:  BUY UP   (theoretical > market, UP underpriced)
if edge < 0:  BUY DOWN (theoretical < market, DOWN underpriced)
```

### Step 6: Entry
- T=0+2 seconds after window opens
- FAK (Fill-And-Kill) market order at Polymarket midpoint
- Position size: MAX(Balance × 2%, $1)

## Fee Structure
```
taker_fee = shares × 0.072 × price × (1 - price)
```

- At price 0.50: max fee ≈ 1.8% of notional
- Breakeven WR: ~53% (accounting for fees)
- Target WR: 55-65% on arbitrage opportunities

## Current Performance (Loop34, ongoing)
- First 100 trades: %62 WR, +$25 PnL (strong edge demonstrated)
- After 155 trades: %48 WR, -$7.15 PnL (regime change issue)
- Issue: realized volatility not adapting fast enough to regime changes

## Planned Improvements
1. EWMA volatility (λ=0.94, RiskMetrics) instead of rolling mean
2. Drift term: `μ = mean(last 15 log returns)` in Brownian formula
3. Multi-window σ ensemble (15 + 30 + 60 bar)
4. Polymarket order book skew as confirmation signal

## Historical Context
See `LoopTest/Loop1..Loop34/report.md` for full history of 34 iterations. Previously tried:
- Microstructure: OFI, OBI, OBI Momentum, VWAP Z-Score
- Trend: Price momentum multi-timeframe, BTC Lead-Lag
- Filters: Feature agreement, Permutation entropy, Edge gates
- Follow-market: Pure Polymarket price following

None of these produced consistent positive edge over 8 hours. Loop34 arbitrage is the first approach with demonstrated mathematical foundation + live edge (first hour +$25).

## Why This Works (Hypothesis)
- Polymarket midpoint is set by thin order book (~$5-15k/side depth)
- Retail traders anchor to 0.50 or recent price, lag true probability
- Brownian motion gives risk-neutral benchmark; mispricing has statistical edge
- Small capital ($30) fits comfortably in thin order book
