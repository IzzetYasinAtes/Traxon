window.traxonCandlestickChart = {
    render: function (elementId, candleDataJson, height) {
        var el = document.getElementById(elementId);
        if (!el || typeof LightweightCharts === 'undefined') return;

        // Destroy previous chart if exists
        if (window['__traxonChart_' + elementId]) {
            window['__traxonChart_' + elementId].chart.remove();
        }

        var chart = LightweightCharts.createChart(el, {
            layout: {
                background: { color: 'transparent' },
                textColor: '#a0a0a0'
            },
            grid: {
                vertLines: { color: '#2a2a2a' },
                horzLines: { color: '#2a2a2a' }
            },
            timeScale: { timeVisible: true, secondsVisible: false }
        });
        chart.resize(el.parentElement.offsetWidth || 800, el.parentElement.offsetHeight || height);

        var series = chart.addSeries(LightweightCharts.CandlestickSeries, {
            upColor: '#00d4aa',
            downColor: '#ff4757',
            borderUpColor: '#00d4aa',
            borderDownColor: '#ff4757',
            wickUpColor: '#00d4aa',
            wickDownColor: '#ff4757'
        });

        var candleData = JSON.parse(candleDataJson);
        series.setData(candleData);
        window['__traxonChart_' + elementId] = { chart: chart, series: series };

        window.addEventListener('resize', function () {
            if (el.parentElement) {
                chart.resize(el.parentElement.offsetWidth, el.parentElement.offsetHeight);
            }
        });
    }
};
