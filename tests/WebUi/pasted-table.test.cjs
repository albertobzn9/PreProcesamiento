const assert = require('node:assert/strict');
const { before, after, test } = require('node:test');
const { resolve } = require('node:path');
const { pathToFileURL } = require('node:url');
const playwright = require('playwright');
const engine = process.env.UI_BROWSER || 'webkit';
const url = pathToFileURL(resolve(__dirname, '../../src/VideoBatchProcessor.App/WebUi/index.html')).href;
const columns = ['Ensayo', 'Lado', 'Estim', 'Latencia', 'TiempoAbs', 'PalancasIzq', 'PalancasDer', 'Desplaz'];
const row = '1\t0\t1\t5\t105\t0\t1\t2';
let browser;
before(async () => { browser = await playwright[engine].launch({ headless: true, ...(process.env.UI_CHANNEL ? { channel: process.env.UI_CHANNEL } : {}) }); });
after(async () => { await browser?.close(); });

async function prepare(page) {
  await page.goto(url);
  assert.equal(await page.locator('#paste-table-modal').isVisible(), false);
  await page.evaluate(() => {
    window.sentMessages = [];
    window.invokeCSharpAction = json => sentMessages.push(JSON.parse(json));
    state.sessions = [{ sessionId: 'a', fileName: 'exp_0526_cs_d1r1.mp4', isSourceSession: true, namingScheme: 'LegacySession' }];
    state.selectedSessionId = 'a';
    renderSessions();
  });
  await page.locator('#paste-table-button').click();
}
async function reply(page, payload) {
  await page.evaluate(payload => receiveFromHost({ ...payload, sessionId: 'a', requestId: sentMessages.at(-1).requestId }), payload);
}

test(`${engine}: pasted table validates before saving, updates check and closes`, async () => {
  const page = await browser.newPage({ viewport: { width: 1400, height: 900 } });
  try {
    await prepare(page);
    assert.equal(await page.locator('#save-pasted-table').isEnabled(), false);
    await page.locator('#paste-table-text').fill(row);
    await page.locator('#preview-pasted-table').click();
    assert.equal(await page.evaluate(() => sentMessages.at(-1).type), 'previewPastedTable');
    await reply(page, { type: 'pastedTablePreview', columns, rowCount: 1, rows: [row.split('\t')] });
    assert.equal(await page.locator('#paste-table-modal th').count(), 8);
    assert.equal(await page.locator('#paste-table-modal td').count(), 8);
    await page.locator('#save-pasted-table').click();
    assert.deepEqual(await page.evaluate(() => [sentMessages.at(-1).type, sentMessages.at(-1).text, sentMessages.at(-1).decimalComma]), ['savePastedTable', row, false]);
    await reply(page, { type: 'pastedTableSaved', session: { sessionId: 'a', fileName: 'exp_0526_cs_d1r1.mp4', isSourceSession: true, namingScheme: 'LegacySession', hasBehavioralSource: true, behavioralSource: 'Pasted table' } });
    assert.equal(await page.locator('#paste-table-modal').isVisible(), false);
    assert.match(await page.locator('#behavioral-source-label').textContent(), /✓ Loaded: Pasted table/);
  } finally { await page.close(); }
});

test(`${engine}: edits invalidate preview, stale replies are ignored and errors remain visible`, async () => {
  const page = await browser.newPage();
  try {
    await prepare(page);
    await page.locator('#paste-table-text').fill(row);
    await page.locator('#preview-pasted-table').click();
    await page.locator('#paste-table-decimal').selectOption('comma');
    await reply(page, { type: 'pastedTablePreview', columns, rowCount: 1, rows: [row.split('\t')] });
    assert.equal(await page.locator('#save-pasted-table').isEnabled(), false);
    await page.locator('#preview-pasted-table').click();
    assert.equal(await page.evaluate(() => sentMessages.at(-1).decimalComma), true);
    await reply(page, { type: 'pastedTableRejected', message: 'Row 2, column 4 (Latencia): expected a number.' });
    assert.match(await page.locator('#paste-table-status').textContent(), /Row 2, column 4/);
    assert.equal(await page.locator('#save-pasted-table').isEnabled(), false);
    await page.locator('#cancel-pasted-table').click();
    assert.equal(await page.evaluate(() => sentMessages.some(item => item.type === 'savePastedTable')), false);
  } finally { await page.close(); }
});

test(`${engine}: missing source offers paste and Escape returns without accepting video-only`, async () => {
  const page = await browser.newPage();
  try {
    await prepare(page);
    await page.locator('#cancel-pasted-table').click();
    await page.evaluate(() => receiveFromHost({ type: 'missingBehavioralSourceDecisionRequired', sessions: [{ sessionId: 'a', video: 'test.mp4' }] }));
    await page.locator('#missing-source-modal').getByRole('button', { name: 'Paste table', exact: true }).click();
    await page.keyboard.press('Escape');
    assert.equal(await page.locator('#paste-table-modal').isVisible(), false);
    assert.equal(await page.locator('#missing-source-modal').isVisible(), true);
    assert.deepEqual(await page.evaluate(() => sentMessages), []);
  } finally { await page.close(); }
});

test(`${engine}: limited preview still saves every row and fits a narrow window`, async () => {
  const page = await browser.newPage({ viewport: { width: 375, height: 740 } });
  try {
    await prepare(page);
    const rows = Array.from({ length: 150 }, (_, index) => [index + 1, index % 2, 0, 5, (index + 1) * 10, index, index, 2].map(String));
    const text = rows.map(row => row.join('\t')).join('\n');
    await page.locator('#paste-table-text').fill(text);
    await page.locator('#preview-pasted-table').click();
    await reply(page, { type: 'pastedTablePreview', columns, rowCount: 150, rows: rows.slice(0, 100) });
    assert.match(await page.locator('#paste-table-status').textContent(), /150 event rows validated.*first 100/);
    const button = await page.locator('#save-pasted-table').boundingBox();
    assert.ok(button.x >= 0 && button.x + button.width <= 375 && button.y + button.height <= 740);
    await page.locator('#save-pasted-table').click();
    assert.equal(await page.evaluate(() => sentMessages.at(-1).text), text);
  } finally { await page.close(); }
});
