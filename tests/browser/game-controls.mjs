import assert from 'node:assert/strict';

export const snapshot = page => page.evaluate(() => window.portStatus.game);
export async function pointAtWorld(page, x, y) {
  const point = await page.evaluate(({ x, y }) => {
    const viewport = window.portStatus.game.viewport;
    const canvas = document.getElementById('theCanvas'), rect = canvas.getBoundingClientRect();
    return { x: rect.left + (x - viewport.x) * viewport.zoom * rect.width / canvas.width,
      y: rect.top + (y - viewport.y) * viewport.zoom * rect.height / canvas.height };
  }, { x, y });
  await page.mouse.move(point.x, point.y);
}
export async function clickControl(page, name) {
  await page.waitForFunction(name => window.portStatus.game?.menu?.allowsInteraction
    && window.portStatus.game.menu.controls.some(control => control.name === name), name, { timeout: 30000 });
  const menu = (await snapshot(page)).menu;
  const control = menu.controls.find(control => control.name === name);
  const point = await page.evaluate(control => {
    const canvas = document.getElementById('theCanvas'), rect = canvas.getBoundingClientRect();
    return { x: rect.left + (control.x + control.width / 2) * rect.width / canvas.width,
      y: rect.top + (control.y + control.height / 2) * rect.height / canvas.height };
  }, control);
  await page.mouse.move(point.x, point.y);
  await page.mouse.down();
  await page.waitForFunction(type => window.portStatus.game.input.leftPressed || window.portStatus.game.menu.type !== type,
    menu.type, { timeout: 10000 });
  await page.mouse.up();
  await page.waitForFunction(() => !window.portStatus.game.input.leftPressed, null, { timeout: 10000 });
}
export async function hold(page, key, milliseconds) {
  const nativeKey = key === 'Backspace' ? 'back' : /^\d$/.test(key) ? `d${key}` : key.toLowerCase();
  await page.locator('#theCanvas').focus();
  await page.keyboard.down(key);
  await page.waitForFunction(key => window.portStatus.game.input.keys.includes(key), nativeKey, { timeout: 10000 });
  await page.waitForTimeout(milliseconds);
  await page.keyboard.up(key);
  await page.waitForFunction(key => !window.portStatus.game.input.keys.includes(key), nativeKey, { timeout: 10000 });
}
export async function walkTo(page, x, y, until = () => false) {
  const location = (await snapshot(page)).location.name;
  for (let step = 0; step < 120; step++) {
    const state = await snapshot(page);
    if (until(state)) return;
    assert.equal(state.location.name, location, 'A location transition must end the current walking route');
    const dx = x - state.player.positionX, dy = y - state.player.positionY;
    if (Math.abs(dx) <= 20 && Math.abs(dy) <= 20) return;
    await hold(page, Math.abs(dx) > Math.abs(dy) ? (dx < 0 ? 'a' : 'd') : (dy < 0 ? 'w' : 's'), 80);
  }
  assert.fail(`Original farmer could not walk to ${x},${y}`);
}
