import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';
import { clickControl, hold } from './game-controls.mjs';

test('browser save controls consume keyboard input and release held game keys', { timeout: 90000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 60000 });
    const before = await page.evaluate(() => window.portStatus.cursorX);
    await page.keyboard.down('ArrowRight');
    await page.waitForFunction(x => window.portStatus.cursorX > x + 5, before);
    await page.evaluate(() => {
      const panel = document.createElement('div'); panel.id = 'savePanel';
      const field = document.createElement('input'); field.id = 'externalField';
      panel.append(field); document.getElementById('app').append(panel); field.focus();
    });
    await page.waitForTimeout(150);
    const stopped = await page.evaluate(() => window.portStatus.cursorX);
    await page.keyboard.up('ArrowRight');
    await page.keyboard.down('ArrowRight');
    await page.keyboard.type('save');
    await page.waitForTimeout(300);
    assert.equal(await page.evaluate(() => window.portStatus.cursorX), stopped);
    assert.equal(await page.locator('#externalField').inputValue(), 'save');
    await page.keyboard.up('ArrowRight');
    await page.evaluate(() => document.getElementById('savePanel').remove());
    await page.locator('#theCanvas').focus();
    await page.keyboard.down('ArrowRight');
    await page.waitForFunction(x => window.portStatus.cursorX > x + 5, stopped);
    await page.keyboard.up('ArrowRight');
  });
});

test('losing focus releases held movement and pointer input, then focus restores control', { timeout: 90000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 60000 });
    const before = await page.evaluate(() => window.portStatus.cursorX);
    await page.keyboard.down('ArrowRight');
    await page.waitForFunction(x => window.portStatus.cursorX > x + 5, before);
    await page.mouse.move(200, 200);
    await page.mouse.down();
    await page.waitForFunction(() => window.portStatus.pointerPressed === true);
    await page.evaluate(() => window.dispatchEvent(new Event('blur')));
    await page.waitForFunction(() => window.portStatus.lifecycle?.focused === false, null, { timeout: 5000 });
    assert.equal(await page.evaluate(() => window.portStatus.pointerPressed), false);
    const stopped = await page.evaluate(() => window.portStatus.cursorX);
    await page.waitForTimeout(300);
    assert.equal(await page.evaluate(() => window.portStatus.cursorX), stopped);
    await page.evaluate(() => window.dispatchEvent(new Event('focus')));
    await page.waitForTimeout(150);
    assert.equal(await page.evaluate(() => window.portStatus.cursorX), stopped, 'Focus alone cannot revive a held key');
    await page.keyboard.up('ArrowRight');
    await page.mouse.up();
    await page.keyboard.down('ArrowRight');
    await page.waitForFunction(x => window.portStatus.cursorX > x + 5, stopped);
    await page.keyboard.up('ArrowRight');
    await page.locator('#theCanvas').focus();
    await page.keyboard.down('ArrowRight');
    await page.evaluate(() => {
      window.testHidden = true;
      Object.defineProperty(document, 'hidden', { configurable: true, get: () => window.testHidden });
      document.dispatchEvent(new Event('visibilitychange'));
    });
    await page.waitForFunction(() => !window.portStatus.lifecycle.focused && !window.portStatus.lifecycle.visible);
    const hidden = await page.evaluate(() => window.portStatus.cursorX);
    await page.waitForTimeout(150);
    assert.equal(await page.evaluate(() => window.portStatus.cursorX), hidden);
    await page.keyboard.up('ArrowRight');
    await page.evaluate(() => { window.testHidden = false; document.dispatchEvent(new Event('visibilitychange')); });
    await page.waitForFunction(() => window.portStatus.lifecycle.focused && window.portStatus.lifecycle.visible);
  });
});

test('an already-connected standard controller maps buttons and sticks through the framework', { timeout: 90000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 60000 });
    await page.waitForFunction(() => window.portStatus.gamepad?.connected, null, { timeout: 5000 });
    await page.evaluate(() => { window.testPad.buttons[0] = { pressed: true, touched: true, value: 1 }; window.testPad.axes[0] = 0.8; });
    await page.waitForFunction(() => window.portStatus.gamepad.a && window.portStatus.gamepad.leftX > 0.5, null, { timeout: 5000 });
    await page.evaluate(() => { window.testPad.buttons[0] = { pressed: false, touched: false, value: 0 }; window.testPad.axes[0] = 0; });
    await page.waitForFunction(() => !window.portStatus.gamepad.a && window.portStatus.gamepad.leftX === 0);
    await page.evaluate(() => {
      window.testPad.connected = false;
      const event = new Event('gamepaddisconnected'); Object.defineProperty(event, 'gamepad', { value: window.testPad });
      window.dispatchEvent(event);
    });
    await page.waitForFunction(() => !window.portStatus.gamepad.connected);
    await page.evaluate(() => {
      window.testPad.connected = true;
      const event = new Event('gamepadconnected'); Object.defineProperty(event, 'gamepad', { value: window.testPad });
      window.dispatchEvent(event);
    });
    await page.waitForFunction(() => window.portStatus.gamepad.connected);
  }, async page => {
    await page.addInitScript(() => {
      window.testPad = { id: 'Standard test controller', index: 0, connected: true, mapping: 'standard', timestamp: 1,
        axes: [0,0,0,0], buttons: Array.from({ length: 17 }, () => ({ pressed: false, touched: false, value: 0 })) };
      Object.defineProperty(navigator, 'getGamepads', { value: () => [window.testPad, null, null, null] });
    });
  });
});

test('original language selection persists, and clipboard paste reaches the original farmer textbox', { timeout: 240000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    await hold(page, 'Escape', 100);
    await clickControl(page, 'languageButton');
    await clickControl(page, 'fr');
    await page.waitForFunction(() => window.portStatus.game.preferences?.language === 'fr' && window.portStatus.storage.phase === 'saved', null, { timeout: 30000 });
    await page.reload({ waitUntil: 'load' });
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    assert.equal(await page.evaluate(() => window.portStatus.game.language), 'fr');
    await hold(page, 'Escape', 100);
    await clickControl(page, 'New');
    await clickControl(page, 'nameBoxCC');
    await page.evaluate(() => navigator.clipboard.writeText('PasteFarm'));
    await page.keyboard.press('Control+v');
    await page.waitForFunction(() => window.portStatus.phase === 'failed' || window.portStatus.game.menu.text?.nameBox === 'PasteFarm', null, { timeout: 15000 });
    const status = await page.evaluate(() => window.portStatus);
    assert.equal(status.phase, 'ready', status.error);
    assert.equal(status.game.menu.text.nameBox, 'PasteFarm');
    for (let i = 0; i < 'PasteFarm'.length; i++) await page.keyboard.press('Backspace');
    await page.waitForFunction(() => window.portStatus.game.menu.text.nameBox === '');
    await page.evaluate(() => navigator.clipboard.writeText('N'));
    await page.keyboard.press('Control+v');
    await page.waitForFunction(() => window.portStatus.game.menu.text.nameBox === 'N');
    await page.keyboard.press('Control+v');
    await page.waitForFunction(() => window.portStatus.game.menu.text.nameBox === 'NN');
  }, async page => { await page.context().grantPermissions(['clipboard-read', 'clipboard-write']); }, '/');
});

test('resizing updates the backing viewport and fullscreen returns cleanly', { timeout: 90000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 60000 });
    await page.setViewportSize({ width: 1280, height: 800 });
    await page.waitForFunction(() => window.portStatus.lifecycle?.width === 1280
      && document.querySelector('#theCanvas').width === 1280, null, { timeout: 5000 });
    await page.locator('#fullscreen').click();
    await page.waitForFunction(() => document.fullscreenElement?.id === 'canvasHolder', null, { timeout: 5000 });
    await page.evaluate(() => document.exitFullscreen());
    await page.waitForFunction(() => !document.fullscreenElement);
    assert.equal(await page.evaluate(() => window.portStatus.phase), 'ready');
  });
});

test('the original title fullscreen button controls actual browser fullscreen', { timeout: 150000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => window.portStatus?.phase === 'ready', null, { timeout: 120000 });
    await hold(page, 'Escape', 100);
    await clickControl(page, 'windowedButton');
    await page.waitForFunction(() => document.fullscreenElement?.id === 'canvasHolder', null, { timeout: 5000 });
    await clickControl(page, 'windowedButton');
    await page.waitForFunction(() => !document.fullscreenElement, null, { timeout: 5000 });
    assert.equal(await page.evaluate(() => window.portStatus.phase), 'ready');
  }, undefined, '/');
});
