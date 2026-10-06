import assert from 'node:assert/strict';

export const snapshot = page => page.evaluate(() => window.portStatus.game);
export async function clickControl(page, name) {
  await page.waitForFunction(name => window.portStatus.game?.menu?.allowsInteraction
    && window.portStatus.game.menu.controls.some(control => control.name === name), name, { timeout: 30000 });
  const menu = (await snapshot(page)).menu;
  const control = menu.controls.find(control => control.name === name);
  await page.mouse.move(control.x + control.width / 2, control.y + control.height / 2);
  await page.mouse.down();
  await page.waitForFunction(type => window.portStatus.game.input.leftPressed || window.portStatus.game.menu.type !== type,
    menu.type, { timeout: 10000 });
  await page.mouse.up();
  await page.waitForFunction(() => !window.portStatus.game.input.leftPressed, null, { timeout: 10000 });
}
export async function hold(page, key, milliseconds) {
  await page.keyboard.down(key);
  await page.waitForFunction(key => window.portStatus.game.input.keys.includes(key.toLowerCase()), key, { timeout: 10000 });
  await page.waitForTimeout(milliseconds);
  await page.keyboard.up(key);
  await page.waitForFunction(key => !window.portStatus.game.input.keys.includes(key.toLowerCase()), key, { timeout: 10000 });
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
