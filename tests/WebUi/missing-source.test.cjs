const assert = require('node:assert/strict');
const { before, after, test } = require('node:test');
const { resolve } = require('node:path');
const { pathToFileURL } = require('node:url');
const playwright = require('playwright');
const engine = process.env.UI_BROWSER || 'webkit';
let browser;
before(async () => { browser = await playwright[engine].launch({
  headless: true,
  ...(process.env.UI_CHANNEL ? { channel: process.env.UI_CHANNEL } : {}),
}); });
after(async () => { await browser?.close(); });

async function prepare(page) {
  await page.goto(pathToFileURL(resolve(__dirname, '../../src/VideoBatchProcessor.App/WebUi/index.html')).href);
  await page.evaluate(() => {
    window.sentMessages = [];
    window.invokeCSharpAction = json => window.sentMessages.push(JSON.parse(json));
    pendingBatchRequest = { type: 'processBatch', sessionId: 'a', sessionIds: ['a', 'b'], existingOutputPolicy: 'archive', exportMode: 'all' };
    receiveFromHost({ type: 'missingBehavioralSourceDecisionRequired', sessions: [{ sessionId: 'b', video: 'exp_0526_dis_d1r1.mp4' }] });
  });
}

for (const choice of ['Cancel', 'Escape', 'Continue without table', 'Choose MAT/CSV']) {
  test(`${engine}: missing table requires an explicit choice: ${choice}`, async () => {
    const page = await browser.newPage({ viewport: { width: 1200, height: 800 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    try {
      await prepare(page);
      assert.equal(await page.locator('#missing-source-modal').isVisible(), true);
      assert.deepEqual(await page.evaluate(() => sentMessages), []);
      if (choice === 'Escape') await page.keyboard.press('Escape');
      else await page.locator('#missing-source-modal').getByRole('button', { name: choice, exact: true }).click();
      const messages = await page.evaluate(() => sentMessages);
      if (choice === 'Continue without table') {
        assert.equal(messages.length, 1);
        assert.deepEqual(messages[0].videoOnlySessionIds, ['b']);
        assert.equal(messages[0].existingOutputPolicy, 'archive');
        await page.evaluate(() => confirmVideoOnly());
        assert.equal(await page.evaluate(() => sentMessages.length), 1);
      } else if (choice === 'Choose MAT/CSV') {
        assert.deepEqual(messages, [{ type: 'attachBehavioralSource', sessionId: 'b' }]);
      } else {
        assert.deepEqual(messages, []);
        await page.evaluate(() => confirmVideoOnly());
        assert.deepEqual(await page.evaluate(() => sentMessages), []);
      }
      if (choice !== 'Choose MAT/CSV') assert.equal(await page.locator('#missing-source-modal').isVisible(), false);
      assert.deepEqual(errors, []);
    } finally { await page.close(); }
  });
}

test(`${engine}: missing table modal is hidden at startup`, async () => {
  const page = await browser.newPage();
  try {
    await page.goto(pathToFileURL(resolve(__dirname, '../../src/VideoBatchProcessor.App/WebUi/index.html')).href);
    assert.equal(await page.locator('#missing-source-modal').isVisible(), false);
  } finally { await page.close(); }
});

test(`${engine}: automatic and manual table loading show a check; unreadable tables do not`, async () => {
  const page = await browser.newPage();
  try {
    await page.goto(pathToFileURL(resolve(__dirname, '../../src/VideoBatchProcessor.App/WebUi/index.html')).href);
    await page.evaluate(() => {
      state.sessions = [{ sessionId: 'a', fileName: 'exp_0526_cs_d1r1.mp4', namingScheme: 'LegacySession', hasBehavioralSource: true, behavioralSource: 'MAT: exp_0526_cs_d1r1.mat' }];
      state.selectedSessionId = 'a';
      renderSessions();
    });
    assert.match(await page.locator('#behavioral-source-label').textContent(), /^✓ Loaded: MAT:/);
    assert.equal(await page.locator('#behavioral-source-label').evaluate(el => el.classList.contains('complete')), true);
    assert.equal(await page.locator('#missing-source-modal').isVisible(), false);
    await page.evaluate(() => receiveFromHost({ type: 'behavioralSourceRefreshed', session: { ...state.sessions[0], hasBehavioralSource: false, behavioralSourceError: 'Invalid MAT data' } }));
    assert.match(await page.locator('#behavioral-source-label').textContent(), /Cannot read table/);
    assert.equal(await page.locator('#behavioral-source-label').evaluate(el => el.classList.contains('complete')), false);
    await page.evaluate(() => receiveFromHost({ type: 'behavioralSourceAttached', session: { ...state.sessions[0], hasBehavioralSource: true, behavioralSourceError: null } }));
    assert.match(await page.locator('#behavioral-source-label').textContent(), /^✓ Loaded:/);
  } finally { await page.close(); }
});

test(`${engine}: Process sessions continues after consent and keeps failure details visible`, async () => {
  const page = await browser.newPage();
  try {
    await page.goto(pathToFileURL(resolve(__dirname, '../../src/VideoBatchProcessor.App/WebUi/index.html')).href);
    await page.evaluate(() => {
      state.sessions = [{ sessionId: 'a', fileName: 'exp_0526_cs_d1r1.mp4' }];
      state.selectedSessionId = 'a';
      state.preview = {};
      state.calibration.saved = {};
      document.getElementById('initials').value = 'abs';
      document.getElementById('treatment').value = 'stx';
      window.sentMessages = [];
      window.invokeCSharpAction = json => {
        const request = JSON.parse(json);
        sentMessages.push(request);
        receiveFromHost(request.videoOnlySessionIds
          ? { type: 'batchProcessingStarted', message: 'Analyzing lights' }
          : { type: 'missingBehavioralSourceDecisionRequired', sessions: [{ sessionId: 'a', video: 'exp_0526_cs_d1r1.mp4' }] });
      };
      startBatchProcessing();
    });
    await page.getByRole('button', { name: 'Continue without table', exact: true }).click();
    assert.equal(await page.evaluate(() => state.batchRunning), true);
    assert.equal(await page.evaluate(() => sentMessages.length), 2);
    assert.deepEqual(await page.evaluate(() => sentMessages[1].videoOnlySessionIds), ['a']);
    await page.evaluate(() => {
      receiveFromHost({ type: 'batchProcessingCompleted', exportedSessions: 0, blockedSessions: 0, failedSessions: 1, exportedClips: 0,
        sessions: [{ video: 'test.mp4', status: 'Failed', message: 'FFmpeg could not create the clip' }] });
      receiveFromHost({ type: 'batchProcessingProgress', message: 'Late progress', percent: 50 });
    });
    assert.equal(await page.locator('#batch-status').isVisible(), true);
    assert.match(await page.locator('#batch-status').textContent(), /FFmpeg could not create the clip/);
    assert.equal(await page.locator('#process-batch').isEnabled(), true);
  } finally { await page.close(); }
});
