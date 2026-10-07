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
export async function walkTo(page, x, y, until = () => false, tolerance = 20) {
  const location = (await snapshot(page)).location.name;
  for (let step = 0; step < 120; step++) {
    const state = await snapshot(page);
    if (until(state)) return;
    assert.equal(state.location.name, location, 'A location transition must end the current walking route');
    const dx = x - state.player.positionX, dy = y - state.player.positionY;
    if (Math.abs(dx) <= tolerance && Math.abs(dy) <= tolerance) return;
    const duration = tolerance < 20 && Math.max(Math.abs(dx), Math.abs(dy)) < 32 ? 0 : 80;
    await hold(page, Math.abs(dx) > Math.abs(dy) ? (dx < 0 ? 'a' : 'd') : (dy < 0 ? 'w' : 's'), duration);
  }
  assert.fail(`Original farmer could not walk to ${x},${y}`);
}

// This Playwright build treats a returned Promise as truthy in waitForFunction.
// Poll asynchronous .NET/storage observations from Node and await their results.
export async function waitForAsync(page, predicate, arg, timeout = 30000) {
  const deadline = Date.now() + timeout;
  while (Date.now() < deadline) {
    if (await page.evaluate(predicate, arg)) return;
    await page.waitForTimeout(50);
  }
  throw new Error(`Timed out waiting for asynchronous game state after ${timeout}ms`);
}

export async function walkToBed(page) {
  const start = await snapshot(page), house = start.location;
  assert.ok(house.bed, 'Sleep route requires an original farmhouse bed');
  const sleeping = state => state.menu.type === 'DialogueBox';
  // Original FarmHouse.resetLocalState puts returning players in the entrance.
  // Move into the room before crossing horizontally past the doorway wall.
  if (start.player.positionY > house.bed.y + 64)
    await walkTo(page, start.player.positionX, house.bed.y - 64, sleeping);
  if ((await snapshot(page)).menu.type !== 'DialogueBox')
    await walkTo(page, house.bed.x - 128, (await snapshot(page)).player.positionY, sleeping);
  if ((await snapshot(page)).menu.type !== 'DialogueBox') await walkTo(page, house.bed.x - 128, house.bed.y, sleeping);
  if ((await snapshot(page)).menu.type !== 'DialogueBox') await walkTo(page, house.bed.x, house.bed.y, sleeping);
}
