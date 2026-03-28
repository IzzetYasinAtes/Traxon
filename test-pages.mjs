import { chromium } from 'playwright';
import { writeFileSync } from 'fs';

const pages = [
  { num: 1, url: 'http://localhost:5000', name: 'dashboard' },
  { num: 2, url: 'http://localhost:5000/signals', name: 'signals' },
  { num: 3, url: 'http://localhost:5000/trades', name: 'trades' },
  { num: 4, url: 'http://localhost:5000/portfolio', name: 'portfolio' },
  { num: 5, url: 'http://localhost:5170', name: 'admin' },
  { num: 6, url: 'http://localhost:5170/calibration', name: 'calibration' },
  { num: 7, url: 'http://localhost:5170/trades', name: 'admin-trades' },
  { num: 8, url: 'http://localhost:5170/engines', name: 'engines' },
  { num: 9, url: 'http://localhost:5170/config', name: 'config' },
  { num: 10, url: 'http://localhost:5170/logs', name: 'logs' },
];

const browser = await chromium.launch({ headless: true });
const context = await browser.newContext({ viewport: { width: 1280, height: 900 } });

const results = [];

for (const p of pages) {
  const page = await context.newPage();
  const consoleErrors = [];
  page.on('console', msg => { if (msg.type() === 'error') consoleErrors.push(msg.text()); });
  page.on('pageerror', err => consoleErrors.push('PAGE ERROR: ' + err.message));

  let httpStatus = 'UNKNOWN';
  let loadError = null;

  try {
    const resp = await page.goto(p.url, { waitUntil: 'networkidle', timeout: 15000 });
    httpStatus = resp ? resp.status() : 'NO_RESP';
    await page.waitForTimeout(1500);
  } catch(e) {
    httpStatus = 'CONN_ERROR';
    loadError = e.message.split('\n')[0];
  }

  const title = await page.title().catch(() => 'N/A');
  
  // Get visible text for error detection
  const bodySnippet = await page.evaluate(() => {
    const body = document.body;
    if (!body) return '';
    return body.innerText.substring(0, 400);
  }).catch(() => '');

  // Check for error indicators
  const hasErrorOnPage = bodySnippet.toLowerCase().includes('error') || 
                          bodySnippet.toLowerCase().includes('exception') ||
                          bodySnippet.toLowerCase().includes('500') ||
                          bodySnippet.toLowerCase().includes('not found') ||
                          bodySnippet.toLowerCase().includes('404');

  const screenshotPath = `D:/repos/Traxon/workspace/playwright-${p.num}-${p.name}.png`;
  let screenshotSaved = false;
  try {
    await page.screenshot({ path: screenshotPath, fullPage: false });
    screenshotSaved = true;
  } catch(e) {}

  results.push({
    num: p.num,
    url: p.url,
    name: p.name,
    httpStatus,
    title,
    consoleErrors: consoleErrors.slice(0, 3),
    loadError,
    hasErrorOnPage,
    bodySnippet: bodySnippet.substring(0, 200),
    screenshotPath: screenshotSaved ? screenshotPath : 'FAILED',
  });

  console.log(JSON.stringify(results[results.length - 1]));
  await page.close();
}

await browser.close();
writeFileSync('/d/repos/Traxon/workspace/playwright-results.json', JSON.stringify(results, null, 2));
console.log('DONE');
