const assert = require('node:assert/strict');
const { before, after, test } = require('node:test');
const { resolve } = require('node:path');
const { pathToFileURL } = require('node:url');
const playwright = require('playwright');

const engine = process.env.UI_BROWSER || 'webkit';
const url = pathToFileURL(resolve(__dirname, '../../src/VideoBatchProcessor.App/WebUi/index.html')).href;
let browser;
before(async () => {
  browser = await playwright[engine].launch({
    headless: true,
    ...(process.env.UI_CHANNEL ? { channel: process.env.UI_CHANNEL } : {}),
  });
});
after(async () => { await browser?.close(); });

async function loadCalibration(page, width, height, cropped = false) {
  await page.goto(url);
  await page.evaluate(({ width, height, cropped }) => {
    window.sentMessages = [];
    window.invokeCSharpAction = json => window.sentMessages.push(JSON.parse(json));
    const rois = {
      foodLeft: { x: 40, y: Math.round(height * .55), width: 24, height: 24 },
      foodRight: { x: width - 70, y: Math.round(height * .45), width: 24, height: 24 },
      noiseLed: { x: width - 40, y: 20, width: 20, height: 20 },
    };
    const canvas = document.createElement('canvas');
    canvas.width = width; canvas.height = height;
    const context = canvas.getContext('2d');
    context.fillStyle = '#f8fafc'; context.fillRect(0, 0, width, height);
    context.fillStyle = '#111827';
    for (const roi of Object.values(rois)) {
      context.beginPath();
      context.arc(roi.x + roi.width / 2, roi.y + roi.height / 2, roi.width / 2, 0, Math.PI * 2);
      context.fill();
    }
    const imageDataUrl = canvas.toDataURL();
    receiveFromHost({ type: 'sessionsLoaded', sessions: [{ sessionId: 'test', fileName: 'exp_0526_dis_d10r2.mp4', phase: 'DIS', isSourceSession: true, namingScheme: 'LegacySession' }] });
    selectSession(state.sessions[0]);
    receiveFromHost({
      type: 'cameraPreviewLoaded', sessionId: 'test',
      width: cropped ? 1920 : width, height: cropped ? 1080 : height,
      crop: cropped ? { x: 40, y: 50, width, height } : null,
      rotation: cropped ? 'Rotate180' : 'None', mirrorHorizontally: cropped,
      previewWidth: width, previewHeight: height, cropEditorWidth: 1920, cropEditorHeight: 1080,
      fps: 30, totalFrames: 72000, durationSeconds: 2400,
      lightRois: rois, imageDataUrl, cropEditorImageDataUrl: imageDataUrl,
    });
    openCalibrationDialog();
    window.calibrationFixture = { type: 'calibrationFrameLoaded', sessionId: 'test', frameToken: 'frame-100', frameIndex: 100, estimatedTimestampSeconds: 100 / 30, width, height, imageDataUrl, brightness: { foodLeft: 100, foodRight: 120, noiseLed: 80 } };
    receiveFromHost({ ...window.calibrationFixture, requestId: state.calibration.requestId });
  }, { width, height, cropped });
  await page.locator('#calibration-image').evaluate(image => image.decode());
}

async function geometry(page) {
  return page.evaluate(() => {
    const rect = node => node.getBoundingClientRect().toJSON();
    const image = document.getElementById('calibration-image');
    return {
      image: rect(image), overlay: rect(document.getElementById('calibration-roi-overlay')),
      viewport: rect(image.parentElement.parentElement),
      width: image.naturalWidth, height: image.naturalHeight,
      prepared: preparedFrameSize(),
      rois: Object.values(state.lightRois),
      circles: [...document.querySelectorAll('.calibration-roi')].map(rect),
    };
  });
}

function assertGeometry(g) {
  const near = (actual, expected) => assert.ok(Math.abs(actual - expected) < .15, `${actual} != ${expected}`);
  assert.ok(g.image.width > 0 && g.image.height > 0);
  for (const key of ['x', 'y', 'width', 'height']) near(g.overlay[key], g.image[key]);
  near(g.image.width / g.image.height, g.width / g.height);
  assert.ok(g.image.left >= g.viewport.left && g.image.top >= g.viewport.top);
  assert.ok(g.image.right <= g.viewport.right && g.image.bottom <= g.viewport.bottom);
  assert.equal(g.circles.length, 3);
  g.rois.forEach((roi, i) => {
    const circle = g.circles[i];
    near(circle.x, g.image.x + roi.x / g.prepared.width * g.image.width);
    near(circle.y, g.image.y + roi.y / g.prepared.height * g.image.height);
    near(circle.width, roi.width / g.prepared.width * g.image.width);
    near(circle.height, roi.height / g.prepared.height * g.image.height);
  });
}

async function waitForFit(page) {
  // Wait for image loading and ResizeObserver layout without a fixed sleep.
  for (let attempt = 0; attempt < 20; attempt++) {
    await page.evaluate(() => new Promise(requestAnimationFrame));
    try { assertGeometry(await geometry(page)); return; }
    catch (error) { if (attempt === 19) throw error; }
  }
}

for (const [name, width, height, cropped] of [
  ['full HD', 1920, 1080, false],
  ['portrait', 720, 1280, false],
  ['cropped, rotated and mirrored', 1600, 420, true],
  ['square crop', 800, 800, true],
]) {
  test(`${engine}: calibration stays aligned for ${name}, resizing and reopening`, async () => {
    const page = await browser.newPage({ viewport: { width: 1408, height: 736 } });
    const errors = [];
    page.on('pageerror', error => errors.push(error.message));
    try {
      await loadCalibration(page, width, height, cropped);
      await waitForFit(page);
      const savedRois = await page.evaluate(() => JSON.stringify(state.lightRois));
      for (const viewport of [{ width: 1000, height: 620 }, { width: 1920, height: 1080 }, { width: 1408, height: 736 }]) {
        await page.setViewportSize(viewport);
        await waitForFit(page);
      }
      await page.evaluate(() => {
        recordCalibrationReference('off');
        receiveFromHost({ ...window.calibrationFixture, requestId: state.calibration.requestId, frameToken: 'frame-200', frameIndex: 200 });
        recordCalibrationReference('active');
        closeCalibrationDialog();
        openCalibrationDialog();
      });
      await waitForFit(page);
      assert.equal(await page.evaluate(() => JSON.stringify(state.lightRois)), savedRois);
      assert.deepEqual(await page.evaluate(() => [state.calibration.offReference.frameIndex, state.calibration.activeReference.frameIndex]), [100, 200]);
      await page.getByRole('button', { name: 'Save calibration', exact: true }).click();
      assert.equal(await page.evaluate(() => window.sentMessages.at(-1).type), 'saveLightCalibration');
      assert.deepEqual(errors, []);
    } finally { await page.close(); }
  });
}

test(`${engine}: batch progress shows its message only once`, async () => {
  const page = await browser.newPage({ viewport: { width: 1408, height: 736 } });
  try {
    await page.goto(url);
    await page.evaluate(() => {
      state.batchRunning = true;
      updateBatchUi('Matching sessions: Analyzing video lights: 0/1 · exp_0526_dis_d10r2.mp4', 16);
    });
    assert.equal(await page.locator('#batch-status').isVisible(), false);
    assert.equal(await page.locator('#batch-progress-label').isVisible(), true);
    assert.equal(await page.locator('#batch-progress-label').textContent(), 'Matching sessions: Analyzing video lights: 0/1 · exp_0526_dis_d10r2.mp4');
    assert.equal(await page.locator('#batch-progress-percent').textContent(), '16%');
    assert.equal(await page.getByText('Matching sessions: Analyzing video lights: 0/1 · exp_0526_dis_d10r2.mp4', { exact: true }).count(), 1);

    await page.evaluate(() => {
      state.batchRunning = false;
      updateBatchUi('Batch not started.');
    });
    assert.equal(await page.locator('#batch-status').isVisible(), true);
    assert.equal(await page.locator('#batch-progress').isVisible(), false);
  } finally { await page.close(); }
});
