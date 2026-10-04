const assert = require('node:assert/strict');
const { before, after, test } = require('node:test');
const { resolve } = require('node:path');
const { pathToFileURL } = require('node:url');
const playwright = require('playwright');
const url = pathToFileURL(resolve(__dirname, '../../src/VideoBatchProcessor.App/WebUi/index.html')).href;
const engine = process.env.UI_BROWSER || 'webkit';
let browser;
before(async () => { browser = await playwright[engine].launch({ headless: true, ...(process.env.UI_CHANNEL ? { channel: process.env.UI_CHANNEL } : {}) }); });
after(async () => { await browser?.close(); });
const width = page => page.locator('#video-setup-panel').evaluate(el => el.getBoundingClientRect().width);
const settle = page => page.evaluate(() => new Promise(resolve => requestAnimationFrame(() => requestAnimationFrame(resolve))));

test(`${engine}: panel drag persists and does not change video settings`, async () => {
  const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
  try {
    await page.goto(url);
    await page.evaluate(() => {
      window.sentMessages = [];
      window.invokeCSharpAction = json => sentMessages.push(JSON.parse(json));
      state.lightRois = { foodLeft: { x: 10, y: 20, width: 30, height: 30 } };
      state.camera = { rotation: 'Rotate180', mirrorHorizontally: true };
      state.calibration.saved = { thresholds: { foodLeft: 80 } };
    });
    const setup = await page.evaluate(() => JSON.stringify([state.camera, state.lightRois, state.calibration.saved]));
    assert.equal(await width(page), 400);
    const box = await page.locator('#video-setup-divider').boundingBox();
    await page.mouse.move(box.x + 4, box.y + box.height / 2);
    await page.mouse.down();
    await page.mouse.move(box.x + 4 - 120, box.y + box.height / 2, { steps: 8 });
    await page.mouse.up();
    assert.equal(await width(page), 520);
    assert.equal(await page.evaluate(() => JSON.stringify([state.camera, state.lightRois, state.calibration.saved])), setup);
    assert.deepEqual(await page.evaluate(() => sentMessages), []);
    await page.reload();
    assert.equal(await width(page), 520);
    await page.locator('#video-setup-divider').dblclick();
    assert.equal(await width(page), 400);
    await page.reload();
    assert.equal(await width(page), 400);
  } finally { await page.close(); }
});

test(`${engine}: panel respects bounds, keyboard and small windows`, async () => {
  const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
  try {
    await page.goto(url);
    const divider = page.locator('#video-setup-divider');
    await divider.focus();
    await page.keyboard.press('End');
    assert.equal(await width(page), 640);
    await page.keyboard.press('ArrowLeft');
    assert.equal(await width(page), 640);
    await page.keyboard.press('Home');
    assert.equal(await width(page), 320);
    await page.keyboard.press('ArrowRight');
    assert.equal(await width(page), 320);
    await page.keyboard.press('End');
    await page.setViewportSize({ width: 800, height: 700 });
    await settle(page);
    assert.equal(await width(page), 392);
    assert.equal(await page.locator('.center').evaluate(el => el.getBoundingClientRect().width), 400);
    await page.setViewportSize({ width: 375, height: 700 });
    await settle(page);
    assert.equal(await divider.isVisible(), false);
    assert.equal(await width(page), 375);
    assert.equal(await page.evaluate(() => document.documentElement.scrollWidth), 375);
    await page.setViewportSize({ width: 1400, height: 900 });
    await settle(page);
    assert.equal(await width(page), 640);
  } finally { await page.close(); }
});

test(`${engine}: interrupted drag restores width and storage failure is harmless`, async () => {
  const page = await browser.newPage({ viewport: { width: 1200, height: 800 } });
  const errors = [];
  page.on('pageerror', error => errors.push(error.message));
  try {
    await page.addInitScript(() => {
      Storage.prototype.getItem = () => { throw new Error('Storage unavailable'); };
      Storage.prototype.setItem = () => { throw new Error('Storage unavailable'); };
    });
    await page.goto(url);
    const divider = page.locator('#video-setup-divider');
    const box = await divider.boundingBox();
    await page.mouse.move(box.x + 4, box.y + 100);
    await page.mouse.down();
    await page.mouse.move(box.x - 100, box.y + 100);
    await page.keyboard.press('Escape');
    await page.mouse.up();
    assert.equal(await width(page), 400);
    assert.equal(await page.locator('body').evaluate(el => el.classList.contains('resizing-setup')), false);
    await divider.focus();
    await page.keyboard.press('ArrowLeft');
    assert.equal(await width(page), 410);
    assert.deepEqual(errors, []);
  } finally { await page.close(); }
});
