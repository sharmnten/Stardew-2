import test from 'node:test';
import assert from 'node:assert/strict';
import { withGame } from './driver.mjs';

const snapshot = page => page.evaluate(() => window.portStatus.game);
async function clickControl(page, name) {
  await page.waitForFunction(name => window.portStatus.game?.menu?.allowsInteraction
    && window.portStatus.game.menu.controls.some(control => control.name === name), name, { timeout: 30000 });
  const control = (await snapshot(page)).menu.controls.find(control => control.name === name);
  await page.mouse.move(control.x + control.width / 2, control.y + control.height / 2);
  await page.mouse.down();
  await page.waitForFunction(() => window.portStatus.game.input.leftPressed, null, { timeout: 10000 });
  await page.mouse.up();
  await page.waitForFunction(() => !window.portStatus.game.input.leftPressed, null, { timeout: 10000 });
}
async function hold(page, key, milliseconds) {
  await page.keyboard.down(key);
  await page.waitForFunction(key => window.portStatus.game.input.keys.includes(key.toLowerCase()), key, { timeout: 10000 });
  await page.waitForTimeout(milliseconds);
  await page.keyboard.up(key);
  await page.waitForFunction(key => !window.portStatus.game.input.keys.includes(key.toLowerCase()), key, { timeout: 10000 });
}
async function walkTo(page, x, y, until = () => false) {
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

test('the original game creates a farmer, reaches the farm, uses a tool, opens inventory and sleeps', { timeout: 300000 }, async () => {
  await withGame(async page => {
    await page.waitForFunction(() => ['ready','failed'].includes(window.portStatus?.phase), null, { timeout: 120000 });
    const status = await page.evaluate(() => window.portStatus);
    assert.equal(status.phase, 'ready', status.error);
    assert.equal(status.game?.runtime, 'StardewValley.GameRunner', 'The recovered original lifecycle must run');
    assert.equal(status.game.audioEngine, 'AudioEngineWrapper', 'Original gameplay audio must initialize successfully');
    await hold(page, 'Escape', 100); // Skip the original logo animation through its normal input.
    await page.waitForFunction(() => window.portStatus.game.menu.allowsInteraction, null, { timeout: 30000 });
    await clickControl(page, 'New');
    console.info('Original title click:', (await snapshot(page)).lastTitleClick);
    await page.waitForFunction(() => window.portStatus.game.menu.type === 'CharacterCustomization', null, { timeout: 30000 });
    for (const [name, value] of [['nameBoxCC','Browser'],['farmnameBoxCC','Wasm'],['favThingBoxCC','Original']]) {
      await clickControl(page, name);
      await page.keyboard.type(value, { delay: 40 });
    }
    await clickControl(page, 'skipIntroButton');
    await clickControl(page, 'okButton');
    await page.waitForFunction(() => window.portStatus.game?.player?.customized && window.portStatus.game.location?.name === 'FarmHouse'
      && window.portStatus.game.day === 1 && !window.portStatus.game.overnight && window.portStatus.game.menu.type === null,
      null, { timeout: 60000 });
    const created = await snapshot(page);
    assert.equal(created.player.name, 'Browser');
    assert.equal(created.player.farmName, 'Wasm');
    assert.equal(created.day, 1);
    console.info('Original farmer created in FarmHouse.');
    const door = created.location.exit;
    await walkTo(page, door.x, door.y, state => state.location.name === 'Farm');
    if ((await snapshot(page)).location.name === 'FarmHouse') await hold(page, 's', 400);
    await page.waitForFunction(() => window.portStatus.game.location.name === 'Farm', null, { timeout: 10000 });
    const before = await snapshot(page);
    await hold(page, 'd', 250);
    const moved = await snapshot(page);
    assert.notEqual(moved.player.positionX, before.player.positionX);
    await hold(page, 'c', 100);
    await page.waitForFunction(stamina => window.portStatus.game.player.stamina < stamina, moved.player.stamina, { timeout: 10000 });
    await page.waitForFunction(() => !window.portStatus.game.player.usingTool && window.portStatus.game.player.canMove, null, { timeout: 10000 });
    await hold(page, 'e', 100);
    await page.waitForFunction(() => window.portStatus.game.menu.type === 'GameMenu', null, { timeout: 10000 });
    console.info('Original movement, tool use and inventory verified.');
    await hold(page, 'Escape', 100);
    const entrance = (await snapshot(page)).location.houseEntrance;
    await walkTo(page, entrance.x, entrance.y);
    await hold(page, 'w', 400);
    await hold(page, 'x', 100); // The original building door uses the action button.
    await page.waitForFunction(() => window.portStatus.game.location.name === 'FarmHouse', null, { timeout: 10000 });
    console.info('Original farmhouse door entered.');
    const house = (await snapshot(page)).location;
    await walkTo(page, house.exit.x, house.exit.y - 64); // Step out of the narrow doorway before crossing the room.
    const bed = house.bed;
    await walkTo(page, bed.x - 128, bed.y); // Enter the original bed from its open side.
    await walkTo(page, bed.x, bed.y, state => state.menu.type === 'DialogueBox');
    await page.waitForFunction(() => window.portStatus.game.menu.type === 'DialogueBox', null, { timeout: 10000 });
    await clickControl(page, 'Yes');
    await page.waitForFunction(() => window.portStatus.game.day === 2, null, { timeout: 60000 });
    await page.waitForFunction(() => !window.portStatus.game.overnight && window.portStatus.game.menu.type === null && window.portStatus.game.player.canMove
      && window.portStatus.game.location.name === 'FarmHouse', null, { timeout: 60000 });
    const morning = await snapshot(page);
    assert.equal(morning.player.name, 'Browser');
    assert.ok(morning.save.mainBytes > 1000, 'The original save serializer must write the full farm');
    assert.ok(morning.save.farmerBytes > 100, 'The original save serializer must write SaveGameInfo');
    console.info('Original day-2 save files:', morning.save);
    await page.screenshot({ path: '.port-cache/new-game-day-2.png' });
  }, undefined, '/');
});
