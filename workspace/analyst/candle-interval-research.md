# Candle Interval & Data Length Research Report
**Date:** 2026-03-29
**Analyst:** Claude Opus 4.6 (Web Research)

---

## A. Optimal Data Length (in Hours) for 5-Minute Predictions

### Findings

There is no single universally agreed-upon number, but research and practice converge on the following:

| Purpose | Data Length (5m candles) | Hours | Candle Count |
|---------|------------------------|-------|-------------|
| **Indicator warmup (minimum)** | ~21 hours | 21h | ~250 candles |
| **Pattern recognition (minimum)** | ~8 hours | 8h | ~100 candles |
| **Reliable indicator + pattern** | 24 hours | 24h | 288 candles |
| **Multi-cycle coverage** | 48-72 hours | 48-72h | 576-864 candles |
| **ML model training** | 30 days+ | 720h+ | 8,640+ candles |

### Key Data Points
- A MACD+RSI indicator implementation (TrendSpider) uses a **warmup period of 250 candles** for reliable signals.
- RSI(14) needs minimum **15 candles** for first value, but ~100+ candles for EMA smoothing to stabilize.
- MACD(12,26,9) needs minimum **35 candles** (26+9) for first signal line value, but **250 candles** recommended for stability.
- Research papers on Bitcoin 5-minute prediction typically use **30 days** of historical data.
- Industry best practice for backtesting uses **10+ years** covering multiple market cycles.

### Recommendation
For **real-time 5-minute trading signals**: maintain a rolling buffer of **300-500 candles** (25-42 hours of 5m data). This covers:
- Full MACD warmup (250 candles)
- Chart pattern formation (50-100 candles for H&S, double tops)
- Sufficient context for trend identification

---

## B. Best Candle Interval: 1m vs 5m vs 15m

### Comparison Table

| Factor | 1-Minute | 5-Minute | 15-Minute |
|--------|----------|----------|-----------|
| **Noise level** | Very high | Moderate | Low |
| **False signals** | Frequent | Moderate | Infrequent |
| **Trade frequency** | Very high | High | Moderate |
| **Pattern reliability** | Low (patterns fail often) | Good | High (15-20% better than 1m) |
| **Scalping suitability** | Yes (HFT only) | Best balance | Too slow for scalping |
| **Indicator accuracy** | Poor (RSI/MACD noisy) | Good | Very good |
| **Execution speed needed** | Sub-second | Seconds | Minutes |
| **Data volume (24h)** | 1,440 candles | 288 candles | 96 candles |
| **Overtrading risk** | Very high | Moderate | Low |

### Pros & Cons

**1-Minute Candles:**
- PROS: Fastest entry/exit detection, most granular data, maximum trade opportunities
- CONS: Extremely noisy, candlestick patterns (doji, hammer, engulfing) frequently fail, requires sub-second execution, high overtrading risk, emotional stress
- VERDICT: Only viable for dedicated HFT/scalping bots with very tight stop-losses

**5-Minute Candles:**
- PROS: Best noise-to-signal ratio for short-term trading, cleaner structure, fewer false signals, price levels hold more consistently, market structure is clearer
- CONS: Slightly slower than 1m for entries, still requires active monitoring
- VERDICT: **Consensus favorite** for day trading and scalping -- profitability peaks around 5m-15m intervals

**15-Minute Candles:**
- PROS: Lowest noise, best pattern reliability, less emotional trading, best for trend-following
- CONS: Fewer trades per day, slower reaction to reversals, may miss short-term scalping opportunities
- VERDICT: Best for intraday swing trading, not ideal for scalping

### Multi-Timeframe Consensus
Multiple sources recommend using **5m as the execution timeframe** with **15m or 1h as the trend confirmation timeframe**. This multi-timeframe approach is the most widely recommended strategy.

---

## C. Minimum Candles Needed for Technical Indicators

### Exact Calculations

| Indicator | Parameters | Theoretical Minimum | Recommended Warmup | Notes |
|-----------|-----------|---------------------|-------------------|-------|
| **RSI** | Period=14 | **15 candles** | **100 candles** | 15 prices for first value; Wilder's smoothing needs ~100 to stabilize |
| **MACD Line** | 12, 26 | **26 candles** | **150 candles** | Slow EMA(26) needs 26 for first value; needs ~5x period to stabilize |
| **MACD Signal** | 9 | **35 candles** (26+9) | **150-250 candles** | Signal line is EMA(9) of MACD; combined warmup is substantial |
| **MACD Histogram** | 12,26,9 | **35 candles** | **250 candles** | TrendSpider's production systems use 250 candle warmup |
| **Bollinger Bands** | 20, 2 | **20 candles** | **50 candles** | SMA(20) + StdDev(20); accurate from first calculation |
| **SMA** | 20 | **20 candles** | **20 candles** | Exact from first calculation, no warmup needed |
| **EMA** | 26 | **1 candle** (technically) | **130 candles** (~5x period) | EMA uses all past data with decaying weight; needs ~5x period to stabilize |
| **EMA** | 12 | **1 candle** (technically) | **60 candles** (~5x period) | Same principle |
| **Stochastic** | 14,3,3 | **14 candles** | **50 candles** | %K needs 14 periods, %D smooths with 3 |
| **ATR** | 14 | **15 candles** | **100 candles** | Similar to RSI (uses Wilder's smoothing) |
| **ADX** | 14 | **28 candles** | **150 candles** | Requires 2x period; uses Wilder's smoothing |
| **Volume SMA** | 20 | **20 candles** | **20 candles** | Simple moving average |

### Critical Insight: Theoretical Minimum vs Practical Minimum
The **theoretical minimum** gives you the first indicator value, but it is unreliable. EMA-based indicators (RSI, MACD, ATR, ADX) use recursive smoothing that needs approximately **5x the period length** to converge to stable values. For MACD(12,26,9), this means:
- First MACD value: candle 26
- First Signal value: candle 35
- Stable/reliable values: **candle 130-250**

---

## D. Minimum Candles Needed for Chart Patterns

### Pattern Requirements

| Pattern | Min Candles | Typical Range | Notes |
|---------|------------|---------------|-------|
| **Head & Shoulders** | ~30 candles | 30-100 candles | 3 peaks + 2 troughs; algorithmic head width ~6 bars, full pattern needs 5-7x |
| **Inverse H&S** | ~30 candles | 30-100 candles | Mirror of H&S |
| **Double Top** | ~45 candles | 45-100+ candles | Research shows ~45x timeframe multiplier; need meaningful pause between peaks |
| **Double Bottom** | ~45 candles | 45-100+ candles | Same as double top |
| **Ascending Triangle** | ~20 candles | 20-50 candles | Minimum 2-3 touches on each trendline |
| **Descending Triangle** | ~20 candles | 20-50 candles | Same as ascending |
| **Symmetrical Triangle** | ~20 candles | 20-50 candles | Converging trendlines need multiple touches |
| **Bull/Bear Flag** | ~10-15 candles | 10-25 candles | Pole (5-10 bars) + flag (5-15 bars) |
| **Pennant** | ~10-15 candles | 10-20 candles | Similar to flag, shorter consolidation |
| **Cup & Handle** | ~50 candles | 50-150 candles | Extended U-shaped formation |
| **Wedge (Rising/Falling)** | ~20 candles | 20-50 candles | Similar to triangles |

### Algorithmic Detection Parameters
For programmatic pattern detection, key tunable parameters are:
- **Depth**: Number of candles between pivot points (typically 5-10 for 5m charts)
- **Deviation**: Minimum price change percentage between pivots
- **Backstep**: Minimum bars before direction change (typically 3-5)
- **leftbars/rightbars**: Lookback for pivot detection (smaller = more sensitive, shorter patterns)

### Reliability by Timeframe
Chart patterns on daily/weekly charts have **15-20% higher success rates** than on hourly or sub-hourly charts. For 5-minute charts, patterns should be confirmed with volume and indicator convergence.

---

## E. Final Recommendation

### Primary Collection Interval: **5-Minute Candles**

**Rationale:**
1. Best noise-to-signal ratio for short-term crypto trading (consensus across multiple sources)
2. Sufficient granularity for scalping without the noise problems of 1m
3. All major indicators (RSI, MACD, Bollinger) work reliably on 5m
4. Chart patterns are recognizable and moderately reliable
5. Binance WebSocket natively supports `5m` kline streams
6. 288 candles/day is manageable data volume

### History to Maintain: **500 candles (≈42 hours)**

| Component | Candles Needed | Covered by 500? |
|-----------|---------------|-----------------|
| MACD full warmup | 250 | Yes |
| RSI stabilization | 100 | Yes |
| Bollinger Bands | 20 | Yes |
| Head & Shoulders detection | 30-100 | Yes |
| Double Top/Bottom detection | 45-100 | Yes |
| Triangle/Flag patterns | 10-50 | Yes |
| Trend context (24h) | 288 | Yes |
| Buffer for safety | +50 | Yes |

### Secondary/Optional: **1-Minute Candles (last 2 hours)**

For precise entry/exit timing after a 5m signal is generated, keeping the last **120 one-minute candles** (2 hours) can help with:
- Exact entry price optimization
- Tight stop-loss placement
- Micro-structure analysis around breakout points

### Binance WebSocket Configuration

Available kline intervals: `1m, 3m, 5m, 15m, 30m, 1h, 2h, 4h, 6h, 8h, 12h, 1d, 3d, 1w, 1M`

**Recommended subscriptions:**
```
Primary:   {symbol}@kline_5m    (main signal generation)
Optional:  {symbol}@kline_1m    (entry timing, keep last 120 only)
Context:   {symbol}@kline_1h    (trend confirmation, keep last 48)
```

### Data Storage Summary

| Stream | Interval | Buffer Size | Hours | Purpose |
|--------|----------|-------------|-------|---------|
| Primary | 5m | 500 candles | ~42h | Indicators + patterns + signals |
| Timing | 1m | 120 candles | 2h | Entry/exit optimization |
| Trend | 1h | 48 candles | 48h | Higher timeframe trend confirmation |
| **Total memory** | — | **668 candles** | — | All three streams combined |

### Startup Data Loading

On system startup, use Binance REST API to backfill:
- `GET /api/v3/klines?symbol=BTCUSDT&interval=5m&limit=500` (max 1000 per request)
- `GET /api/v3/klines?symbol=BTCUSDT&interval=1h&limit=48`
- Then switch to WebSocket for real-time updates

This ensures indicators are fully warmed up before generating the first trading signal.

---

## Sources

- [YouHodler - Time Interval Analysis 1m, 5m, 1H, 1D, 1W](https://www.youhodler.com/education/time-interval-analysis-1m-5m-15m-1h-4h-1d-1w)
- [Zondacrypto - Time Interval Analysis](https://zondacrypto.com/en/academy/time-interval-analysis-1m-5m-1h-1d-1w-)
- [altFINS - Crypto Time Frames](https://altfins.com/knowledge-base/time-frames/)
- [altFINS - Essential Candlestick Patterns](https://altfins.com/knowledge-base/essential-candlestick-patterns-for-crypto-traders/)
- [LuxAlgo - Candle Formations Every Scalper Should Know](https://www.luxalgo.com/blog/candle-formations-every-scalper-should-know/)
- [LuxAlgo - Best Timeframes for Candlestick Patterns](https://www.luxalgo.com/blog/best-timeframes-for-candlestick-patterns/)
- [QuantVPS - Best Candlestick Time Frame for Day Trading](https://www.quantvps.com/blog/best-candlestick-time-frame-for-day-trading)
- [Warrior Trading - How to Choose the Right Trading Time Frame](https://www.warriortrading.com/choose-right-time-frame/)
- [1MinScalper - 5-Minute Scalping Strategy](https://1minscalper.com/5-minute-scalping-strategy/)
- [Binance - WebSocket Streams Documentation](https://developers.binance.com/docs/binance-spot-api-docs/web-socket-streams)
- [Binance - Kline Candlestick Streams](https://developers.binance.com/docs/derivatives/usds-margined-futures/websocket-market-streams/Kline-Candlestick-Streams)
- [QuantInsti - RSI Indicator Formula and Calculation](https://blog.quantinsti.com/rsi-indicator/)
- [Macroption - RSI Calculation](https://www.macroption.com/rsi-calculation/)
- [Wikipedia - RSI](https://en.wikipedia.org/wiki/Relative_strength_index)
- [Admiral Markets - MACD Settings for Day Trading & Scalping](https://admiralmarkets.com/education/articles/forex-indicators/macd-indicator-in-depth)
- [Wikipedia - MACD](https://en.wikipedia.org/wiki/MACD)
- [TrendSpider - MACD and RSI Momentum (250 candle warmup)](https://trendspider.com/trading-tools-store/indicators/macd-and-rsi-momentum/)
- [StockCharts - Bollinger Bands](https://chartschool.stockcharts.com/table-of-contents/technical-indicators-and-overlays/technical-overlays/bollinger-bands)
- [Britannica - Bollinger Bands Explained](https://www.britannica.com/money/bollinger-bands-indicator)
- [TrendSpider - Automated Head and Shoulders Pattern Recognition](https://trendspider.com/blog/next-level-intelligence-automated-head-and-shoulders-pattern-recognition/)
- [Medium - Algorithmic Head and Shoulders Detection in Python](https://medium.com/@minkeliu_29243/the-head-and-shoulders-pattern-in-technical-analysis-48cbca1ca9ea)
- [QuantConnect - Head & Shoulders TA Pattern Detection](https://www.quantconnect.com/research/15603/head-amp-shoulders-ta-pattern-detection/)
- [Fidelity - Identifying Chart Patterns](https://www.fidelity.com/bin-public/060_www_fidelity_com/documents/learning-center/Idenitfying-Chart-Patterns.pdf)
- [LiteFinance - Double Top Pattern](https://www.litefinance.org/blog/for-professionals/100-most-efficient-forex-chart-patterns/double-top-pattern/)
- [Wikipedia - Double Top and Double Bottom](https://en.wikipedia.org/wiki/Double_top_and_double_bottom)
- [TechScience - Bitcoin Candlestick Prediction with Deep Neural Networks](https://www.techscience.com/cmc/v68n3/42488/html)
- [FXOpen - 1-Minute Scalping Strategies](https://fxopen.com/blog/en/1-minute-scalping-trading-strategies-with-examples/)
- [MC2 Finance - Best RSI for Scalping](https://www.mc2.fi/blog/best-rsi-for-scalping)
