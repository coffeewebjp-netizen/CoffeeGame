'use strict';

const assert = require('node:assert/strict');
const fs = require('node:fs');
const path = require('node:path');
const vm = require('node:vm');

function makeClassList() {
  const names = new Set();
  return {
    add: (...values) => values.forEach((value) => names.add(value)),
    remove: (...values) => values.forEach((value) => names.delete(value)),
    toggle: (value, force) => {
      if (force === undefined ? !names.has(value) : force) names.add(value);
      else names.delete(value);
    },
    contains: (value) => names.has(value)
  };
}

function makeElement(id) {
  const listeners = new Map();
  const attributes = new Map();
  const styles = new Map();
  return {
    id,
    textContent: '',
    innerHTML: '',
    hidden: false,
    open: false,
    disabled: false,
    title: '',
    value: '',
    paused: true,
    ended: false,
    error: null,
    volume: 1,
    dataset: {},
    classList: makeClassList(),
    style: {
      setProperty(name, value) { styles.set(name, String(value)); },
      getPropertyValue(name) { return styles.get(name) || ''; }
    },
    setAttribute(name, value) { attributes.set(name, String(value)); },
    getAttribute(name) { return attributes.get(name) ?? null; },
    addEventListener(type, listener) {
      if (!listeners.has(type)) listeners.set(type, []);
      listeners.get(type).push(listener);
    },
    dispatchEvent(event) {
      for (const listener of listeners.get(event.type) || []) listener({ target: this, ...event });
      return true;
    },
    play() {
      this.paused = false;
      this.dispatchEvent({ type: 'play' });
      return Promise.resolve();
    },
    pause() {
      this.paused = true;
      this.dispatchEvent({ type: 'pause' });
    },
    focus() {},
    setPointerCapture() {},
    getBoundingClientRect: () => ({ left: 0, top: 0, width: 960, height: 540 })
  };
}

const gradient = { addColorStop() {} };
let spriteSheetDrawCalls = 0;
let locomotionDrawCalls = 0;
let directionalAttackDrawCalls = 0;
let directionalAirDrawCalls = 0;
let slimeAnimationDrawCalls = 0;
const context = new Proxy({}, {
  get(target, property) {
    if (property === 'createLinearGradient' || property === 'createRadialGradient') return () => gradient;
    if (property === 'drawImage') {
      return (source) => {
        if (source?.src?.includes('hero-animation-sheet-v2.png')) spriteSheetDrawCalls += 1;
        if (source?.src?.includes('hero-locomotion-')) locomotionDrawCalls += 1;
        if (source?.src?.includes('hero-attack-directional-v1.png')) directionalAttackDrawCalls += 1;
        if (source?.src?.includes('hero-jump-directional-v1.png') || source?.src?.includes('hero-air-slash-directional-v1.png') || source?.src?.includes('hero-plunge-directional-v1.png')) directionalAirDrawCalls += 1;
        if (source?.src?.includes('slime-animation-v1.png')) slimeAnimationDrawCalls += 1;
      };
    }
    if (!(property in target)) target[property] = () => {};
    return target[property];
  },
  set(target, property, value) {
    target[property] = value;
    return true;
  }
});

const elements = new Map();
const ids = [
  'game', 'eventLog', 'goalBadge', 'expStat', 'goldStat', 'jellyStat',
  'resultOverlay', 'resultKicker', 'resultTitle', 'resultSummary', 'retryButton',
  'jumpButton', 'attackButton', 'specialButton', 'magicButton', 'specialCostLabel',
  'magicCostLabel', 'respawnEnemyButton', 'resetTuningButton', 'startOverlay',
  'startButton', 'tuningPanel', 'levelStatus',
  'hpStatus', 'expStatus', 'staminaStatus', 'mpStatus', 'enemyHpStatus',
  'controllerCompactStatus', 'controllerBadge', 'controllerName', 'controllerLastInput',
  'controllerJumpMap', 'controllerAttackMap', 'controllerSpecialMap', 'controllerMagicMap', 'controllerConfirmMap',
  'inputBindingPanel', 'rebindStatus', 'resetBindingsButton', 'jumpBindingHint', 'attackBindingHint',
  'specialBindingHint', 'magicBindingHint', 'confirmBindingHint', 'gamepadBindingHint',
  'bgmAudio', 'bgmToggleButton', 'bgmVolume', 'bgmStatus', 'sfxToggleButton', 'sfxVolume',
  'bgmVolumeMobile', 'sfxVolumeMobile'
];

const html = fs.readFileSync(path.join(__dirname, 'index.html'), 'utf8');
for (const id of ids) {
  assert.match(html, new RegExp(`id=["']${id}["']`), `index.html should contain #${id}`);
}
for (const asset of ['styles.css', 'game.js', 'assets/hero-sprite.png', 'assets/hero-animation-sheet-v2.png', 'assets/hero-locomotion-down-v1.png', 'assets/hero-locomotion-side-v1.png', 'assets/hero-attack-directional-v1.png', 'assets/hero-jump-directional-v1.png', 'assets/hero-air-slash-directional-v1.png', 'assets/hero-plunge-directional-v1.png', 'assets/slime-sprite.png', 'assets/slime-animation-v1.png', 'assets/Rituals_of_the_Jade_Valley.mp3', 'assets/katana-slash1.mp3', 'assets/magic-wind2.mp3']) {
  assert.ok(fs.existsSync(path.join(__dirname, asset)), `${asset} should exist`);
}

for (const id of ids) elements.set(id, makeElement(id));
elements.get('game').getContext = () => context;

const documentListeners = new Map();
global.document = {
  hidden: false,
  getElementById: (id) => elements.get(id) || null,
  querySelector: () => null,
  querySelectorAll: () => [],
  addEventListener(type, listener) {
    if (!documentListeners.has(type)) documentListeners.set(type, []);
    documentListeners.get(type).push(listener);
  },
  dispatchEvent(event) {
    for (const listener of documentListeners.get(event.type) || []) listener(event);
    return true;
  }
};

const windowListeners = new Map();
let audioContextCreateCount = 0;
let audioContextResumeCount = 0;
let oscillatorStartCount = 0;
let noiseStartCount = 0;

function makeAudioParam(initialValue = 0) {
  return {
    value: initialValue,
    cancelScheduledValues() {},
    setValueAtTime(value) { this.value = value; },
    linearRampToValueAtTime(value) { this.value = value; },
    exponentialRampToValueAtTime(value) { this.value = value; }
  };
}

function makeAudioNode() {
  return {
    connect(target) { return target; },
    disconnect() {},
    onended: null
  };
}

class MockAudioContext {
  constructor() {
    audioContextCreateCount += 1;
    this.currentTime = 0;
    this.sampleRate = 44100;
    this.state = 'suspended';
    this.destination = makeAudioNode();
    this.onstatechange = null;
  }

  resume() {
    audioContextResumeCount += 1;
    this.state = 'running';
    this.onstatechange?.();
    return Promise.resolve();
  }

  suspend() {
    this.state = 'suspended';
    this.onstatechange?.();
    return Promise.resolve();
  }

  createGain() {
    return { ...makeAudioNode(), gain: makeAudioParam(1) };
  }

  createOscillator() {
    const node = {
      ...makeAudioNode(),
      type: 'sine',
      frequency: makeAudioParam(440),
      start() { oscillatorStartCount += 1; },
      stop() { this.onended?.(); }
    };
    return node;
  }

  createBiquadFilter() {
    return { ...makeAudioNode(), type: 'bandpass', frequency: makeAudioParam(1200), Q: makeAudioParam(1) };
  }

  createDynamicsCompressor() {
    return {
      ...makeAudioNode(),
      threshold: makeAudioParam(-24),
      knee: makeAudioParam(30),
      ratio: makeAudioParam(12),
      attack: makeAudioParam(0.003),
      release: makeAudioParam(0.25)
    };
  }

  createBuffer(_channels, length) {
    const channel = new Float32Array(length);
    return { getChannelData: () => channel };
  }

  createBufferSource() {
    const node = {
      ...makeAudioNode(),
      buffer: null,
      start() { noiseStartCount += 1; },
      stop() { this.onended?.(); }
    };
    return node;
  }
}

global.window = {
  AudioContext: MockAudioContext,
  addEventListener(type, listener) {
    if (!windowListeners.has(type)) windowListeners.set(type, []);
    windowListeners.get(type).push(listener);
  },
  dispatchEvent(event) {
    for (const listener of windowListeners.get(event.type) || []) listener(event);
    return true;
  },
  setTimeout() {}
};
const localStore = new Map();
localStore.set('coffeeGame.inputBindings.v1', JSON.stringify({
  gamepad: { attack: [7, 0], special: [6, 2], magic: [5, 3], confirm: [0, 9] },
  keyboard: { attack: ['F', ' '], special: ['Q'], magic: ['E'], confirm: ['Enter', 'R'] }
}));
global.localStorage = {
  getItem: (key) => localStore.get(key) ?? null,
  setItem: (key, value) => localStore.set(key, String(value))
};
let fakeGamepads = [];
global.navigator = { getGamepads: () => fakeGamepads };
global.Image = class Image {
  constructor() {
    this.complete = false;
    this.naturalWidth = 0;
    this.naturalHeight = 0;
    this._src = '';
  }

  set src(value) {
    this._src = value;
    if (String(value).includes('hero-animation-sheet-v2.png')) {
      this.complete = true;
      this.naturalWidth = 1772;
      this.naturalHeight = 886;
    } else if (String(value).includes('hero-locomotion-') || String(value).includes('hero-attack-directional-v1.png') || String(value).includes('hero-jump-directional-v1.png') || String(value).includes('hero-air-slash-directional-v1.png') || String(value).includes('hero-plunge-directional-v1.png')) {
      this.complete = true;
      this.naturalWidth = 1448;
      this.naturalHeight = 1086;
    } else if (String(value).includes('slime-animation-v1.png')) {
      this.complete = true;
      this.naturalWidth = 1536;
      this.naturalHeight = 1024;
    }
  }

  get src() { return this._src; }
};

let now = 0;
let scheduledFrame = null;
global.performance = { now: () => now };
global.requestAnimationFrame = (callback) => {
  scheduledFrame = callback;
  return 1;
};

function step(seconds) {
  const frames = Math.ceil(seconds / 0.025);
  for (let index = 0; index < frames; index += 1) {
    now += 25;
    const callback = scheduledFrame;
    assert.equal(typeof callback, 'function', 'animation frame should be scheduled');
    callback(now);
  }
}

const source = fs.readFileSync(path.join(__dirname, 'game.js'), 'utf8');
vm.runInThisContext(source, { filename: 'game.js' });

const game = window.CoffeeGamePrototype;
assert.ok(game, 'debug API should be exposed');
assert.equal(game.getSnapshot().state.mode, 'ready', 'initial page should wait for the player');
assert.equal(elements.get('sfxToggleButton').textContent, '✦ SFX待機', 'SFX should explain that a first gesture is still needed');
assert.deepEqual(game.getInputBindings().gamepad.attack, [7], 'old RT/A default should migrate to RT only');
assert.deepEqual(game.getInputBindings().gamepad.jump, [0], 'A should become the dedicated jump button during combat');
assert.deepEqual(game.getInputBindings().keyboard.attack, ['f'], 'legacy Space attack should migrate to F only');
assert.deepEqual(game.getInputBindings().keyboard.jump, [' '], 'Space should become the keyboard jump control');
assert.deepEqual(game.getInputBindings().gamepad.special, [2], 'old LT/X default should migrate to X only');
assert.deepEqual(game.getInputBindings().gamepad.magic, [3], 'old RB/Y default should migrate to Y only');
assert.deepEqual(game.getInputBindings().keyboard.special, ['q'], 'stored Legacy keys should normalize case');
window.dispatchEvent({
  type: 'pointerdown',
  target: { closest: () => null }
});
assert.equal(audioContextCreateCount, 1, 'the first gesture should create one shared SFX context');
assert.equal(audioContextResumeCount, 1, 'the first gesture should resume suspended Web Audio');
assert.equal(game.getSnapshot().audio.playing, false, 'a setup gesture before combat must not start BGM');
game.resetRun();
assert.equal(game.getSnapshot().audio.sfxReady, true);
assert.equal(game.getSnapshot().audio.sfxPlayCounts.battleStart, 1, 'combat start should play once');

for (let kill = 1; kill <= 5; kill += 1) {
  assert.equal(game.defeatCurrentSlime(), true, `slime ${kill} should be defeated once`);
  assert.equal(game.defeatCurrentSlime(), false, `slime ${kill} must not reward twice`);

  const snapshot = game.getSnapshot();
  assert.equal(snapshot.state.kills, kill);
  assert.equal(snapshot.state.gold, kill);
  assert.equal(snapshot.state.materials.slimeJelly, kill);
  assert.equal(snapshot.audio.sfxPlayCounts.reward, kill, 'one slime should grant one reward sound');

  if (kill === 3) {
    assert.equal(snapshot.player.level, 2, 'third slime should reach level 2');
    assert.equal(snapshot.player.xp, 0);
    assert.equal(snapshot.audio.sfxPlayCounts.levelUp, 1, 'the level-up sound should occur once at Lv 2');
  }

  if (kill < 5) game.resetCurrentEnemy();
}

let snapshot = game.getSnapshot();
assert.equal(snapshot.state.mode, 'finishing');
assert.equal(snapshot.player.level, 2);
assert.equal(snapshot.player.xp, 2);
step(1);
snapshot = game.getSnapshot();
assert.equal(snapshot.state.mode, 'cleared');
assert.equal(elements.get('resultOverlay').hidden, false);

game.resetRun();
snapshot = game.getSnapshot();
assert.equal(snapshot.audio.playing, true, 'starting combat should request BGM playback');
assert.equal(snapshot.player.locomotion, 'idle');
assert.equal(snapshot.player.visualDirection, 'down');
fakeGamepads = [{
  id: 'Steam Input Virtual Gamepad', index: 0, connected: true, mapping: 'standard',
  axes: [0.8, 0], buttons: Array.from({ length: 16 }, () => ({ pressed: false, value: 0 }))
}];
step(0.3);
snapshot = game.getSnapshot();
assert.equal(snapshot.player.locomotion, 'walk', 'movement should begin as a walk');
assert.equal(snapshot.player.visualDirection, 'side');
step(0.5);
snapshot = game.getSnapshot();
assert.equal(snapshot.player.locomotion, 'run', 'holding one direction should transition to running');
fakeGamepads[0].axes = [0, -0.8];
step(0.05);
snapshot = game.getSnapshot();
assert.equal(snapshot.player.locomotion, 'walk', 'changing direction should reset running to a walk');
assert.equal(snapshot.player.visualDirection, 'up');
fakeGamepads = [];
step(0.05);
assert.equal(game.getSnapshot().player.locomotion, 'idle');

game.queueAction('jump');
step(0.125);
snapshot = game.getSnapshot();
assert.ok(snapshot.player.airHeight > 0, 'jump should create real airborne height');
assert.equal(snapshot.player.airState, 'jump');
assert.equal(snapshot.state.counts.jumps, 1);
game.queueAction('attack');
step(0.025);
snapshot = game.getSnapshot();
assert.equal(snapshot.player.airState, 'airSlash', 'sword input in the air should become a broad aerial slash');
assert.equal(snapshot.state.counts.airSlashes, 1);
window.dispatchEvent({
  type: 'keydown', key: 'arrowdown', isComposing: false, repeat: false,
  target: { matches: () => false, closest: () => null }, preventDefault() {}
});
step(0.075);
snapshot = game.getSnapshot();
assert.equal(snapshot.player.airState, 'plunge', 'fresh down input in the air should start the plunge');
window.dispatchEvent({ type: 'keyup', key: 'arrowdown' });
step(0.22);
snapshot = game.getSnapshot();
assert.equal(snapshot.player.airState, 'landingLag', 'plunge should end in a brief landing lock');
assert.equal(snapshot.state.counts.plunges, 1);
assert.ok(snapshot.effects.impactWaves > 0, 'plunge landing should create a damaging shockwave');
assert.ok(!snapshot.enemy || snapshot.enemy.hp < snapshot.enemy.maxHp, 'nearby slime should take plunge shockwave damage');
step(0.55);
assert.equal(game.getSnapshot().player.airState, 'grounded', 'landing lock should release automatically');

game.resetRun();
game.queueAction('magic');
step(0.025);
snapshot = game.getSnapshot();
assert.equal(snapshot.state.counts.magic, 0, 'ice magic should not fire before charging finishes');
assert.equal(snapshot.player.charge.action, 'magic');
assert.ok(snapshot.player.mp < snapshot.player.maxMp);
assert.equal(snapshot.effects.magicBolts, 0);
assert.equal(snapshot.audio.sfxPlayCounts.iceCharge, 1, 'ice charge should sound once when charging begins');
step(0.7);
snapshot = game.getSnapshot();
assert.equal(snapshot.state.counts.magic, 1);
assert.equal(snapshot.audio.sfxPlayCounts.iceCast, 1, 'ice cast should sound once after charging');
assert.equal(snapshot.player.charge, null);
assert.ok(snapshot.effects.magicBolts === 1 || snapshot.enemy.hp < snapshot.enemy.maxHp, 'released ice bolt should exist or have hit');

game.resetRun();
game.queueAction('special');
step(0.025);
snapshot = game.getSnapshot();
assert.equal(snapshot.state.counts.special, 0);
assert.match(snapshot.state.message, /ST 100/);

game.resetRun();
game.setConfig('specialCost', 0);
game.queueAction('special');
step(0.025);
snapshot = game.getSnapshot();
assert.equal(snapshot.state.counts.special, 0, 'spin attack should wait for its charge');
assert.equal(snapshot.player.charge.action, 'special');
assert.ok(snapshot.audio.sfxPlayCounts.specialCharge >= 1, 'special charge should have a start sound');
assert.equal(snapshot.enemy.hp, snapshot.enemy.maxHp, 'charging must not deal damage early');
step(0.82);
snapshot = game.getSnapshot();
assert.equal(snapshot.state.counts.special, 1, 'spin attack should release after charging');
assert.ok(snapshot.audio.sfxPlayCounts.spin >= 1, 'spin release should have a sound');
assert.equal(snapshot.state.kills, 1, 'the 360-degree spin should hit the nearby slime');

game.resetRun();
game.queueAction('special');
step(0.025);
assert.equal(game.getSnapshot().player.charge.action, 'special');
game.resetRun();
step(0.9);
snapshot = game.getSnapshot();
assert.equal(snapshot.state.counts.special, 0, 'reset must cancel delayed spin release');
assert.equal(snapshot.player.charge, null);

const focusedActionButton = {
  matches(selector) {
    if (selector === '[data-action], [data-move]') return true;
    return selector.includes('button');
  },
  closest() { return null; }
};
let focusedKeyPrevented = false;
window.dispatchEvent({
  type: 'keydown',
  key: 'q',
  isComposing: false,
  repeat: false,
  target: focusedActionButton,
  preventDefault() { focusedKeyPrevented = true; }
});
step(0.025);
assert.equal(game.getSnapshot().player.charge.action, 'special', 'Legacy Q should work while an action button has focus');
assert.equal(focusedKeyPrevented, true, 'handled Legacy action should suppress focused-button defaults');
game.resetRun();
game.setConfig('specialCost', 100);

game.resetRun();
game.queueAction('attack');
step(0.025);
snapshot = game.getSnapshot();
assert.equal(snapshot.state.counts.sword, 1);
assert.equal(snapshot.player.stamina, 25);
assert.equal(snapshot.enemy.hp, snapshot.enemy.maxHp - 3);
assert.ok(snapshot.audio.sfxPlayCounts.swordSwing >= 1, 'sword action should play a swing sound');
assert.ok(snapshot.audio.sfxPlayCounts.swordHit >= 1, 'a landed sword should add a hit sound');

game.resetRun();
const slimeChargeSoundsBefore = game.getSnapshot().audio.sfxPlayCounts.slimeCharge || 0;
const slimeLungeSoundsBefore = game.getSnapshot().audio.sfxPlayCounts.slimeLunge || 0;
step(1.35);
snapshot = game.getSnapshot();
assert.ok((snapshot.audio.sfxPlayCounts.slimeCharge || 0) > slimeChargeSoundsBefore, 'slime wind-up should have a warning sound');
assert.ok((snapshot.audio.sfxPlayCounts.slimeLunge || 0) > slimeLungeSoundsBefore, 'slime launch should have a separate sound');

game.resetRun(false);
const padButtons = Array.from({ length: 16 }, () => ({ pressed: false, value: 0 }));
fakeGamepads = [{
  id: 'Steam Input Virtual Gamepad',
  index: 0,
  connected: true,
  mapping: 'standard',
  axes: [0, 0],
  buttons: padButtons
}];
padButtons[0] = { pressed: true, value: 1 };
step(0.025);
snapshot = game.getSnapshot();
assert.equal(snapshot.state.mode, 'playing', 'A should start a ready run');
assert.equal(snapshot.controller.connected, true);
assert.equal(snapshot.controller.compatible, true);
assert.match(snapshot.controller.id, /Steam Input/);

padButtons[0] = { pressed: false, value: 0 };
step(0.025);
padButtons[7] = { pressed: true, value: 1 };
step(0.025);
snapshot = game.getSnapshot();
assert.equal(snapshot.state.counts.sword, 1, 'RT should use the sword');
assert.match(snapshot.controller.lastInput, /剣/);

padButtons[7] = { pressed: false, value: 0 };
fakeGamepads[0].axes[0] = 0.8;
step(0.4);
snapshot = game.getSnapshot();
assert.ok(snapshot.controller.move.x > 0.7, 'left stick should move horizontally');

padButtons[6] = { pressed: true, value: 1 };
step(0.025);
padButtons[2] = { pressed: true, value: 1 };
step(0.025);
snapshot = game.getSnapshot();
assert.equal(snapshot.state.counts.special, 0, 'X should reach the ST guard even while LT is held');
assert.match(snapshot.controller.lastInput, /回転斬り/);
padButtons[6] = { pressed: false, value: 0 };
padButtons[2] = { pressed: false, value: 0 };
step(0.025);

padButtons[3] = { pressed: true, value: 1 };
step(0.025);
snapshot = game.getSnapshot();
assert.equal(snapshot.state.counts.magic, 0, 'Y should begin charging ice magic');
assert.equal(snapshot.player.charge.action, 'magic');
assert.match(snapshot.controller.lastInput, /氷魔法/);

padButtons[3] = { pressed: false, value: 0 };
step(0.7);
snapshot = game.getSnapshot();
assert.equal(snapshot.state.counts.magic, 1, 'Y ice magic should release after charging');
step(0.45);
assert.equal(game.setInputBinding('gamepad', 'attack', 5), true);
assert.deepEqual(game.getInputBindings().gamepad.attack, [5]);
assert.match(localStore.get('coffeeGame.inputBindings.v1'), /"attack":\[5\]/);
step(0.025);
padButtons[5] = { pressed: true, value: 1 };
step(0.05);
snapshot = game.getSnapshot();
assert.equal(snapshot.state.counts.sword, 2, 'a remapped RB should use the sword');
game.resetInputBindings();
assert.deepEqual(game.getInputBindings().gamepad.attack, [7]);
assert.deepEqual(game.getInputBindings().gamepad.jump, [0]);
assert.deepEqual(game.getInputBindings().gamepad.special, [2]);
assert.deepEqual(game.getInputBindings().gamepad.magic, [3]);
assert.equal(elements.get('controllerAttackMap').textContent, 'RT');
assert.equal(elements.get('controllerJumpMap').textContent, 'A');

game.resetRun();
const inputBindingPanel = elements.get('inputBindingPanel');
const tuningPanel = elements.get('tuningPanel');
inputBindingPanel.open = true;
game.beginRebinding('keyboard', 'attack');
assert.equal(game.getSnapshot().state.mode, 'input-paused', 'rebinding should pause live combat');
inputBindingPanel.open = false;
inputBindingPanel.dispatchEvent({ type: 'toggle' });
assert.equal(game.getSnapshot().state.mode, 'playing', 'closing the binding panel should cancel and resume');

tuningPanel.open = true;
tuningPanel.dispatchEvent({ type: 'toggle' });
assert.equal(game.getSnapshot().state.mode, 'paused', 'opening tuning should pause combat');
inputBindingPanel.open = true;
game.beginRebinding('keyboard', 'magic');
tuningPanel.open = false;
tuningPanel.dispatchEvent({ type: 'toggle' });
assert.equal(game.getSnapshot().state.mode, 'paused', 'closing tuning must not resume during rebinding');
inputBindingPanel.open = false;
inputBindingPanel.dispatchEvent({ type: 'toggle' });
assert.equal(game.getSnapshot().state.mode, 'playing', 'ending the final pause reason should resume combat');

padButtons[5] = { pressed: false, value: 0 };
fakeGamepads[0].mapping = '';
step(0.025);
snapshot = game.getSnapshot();
assert.equal(snapshot.controller.connected, true);
assert.equal(snapshot.controller.compatible, false, 'raw non-standard mapping should require Steam Input setup');
assert.equal(snapshot.controller.move.x, 0);

fakeGamepads = [];
step(0.025);
snapshot = game.getSnapshot();
assert.equal(snapshot.controller.connected, false, 'disconnect should clear the active controller');
game.debugLegacyInput('KEY / Legacy：F');
snapshot = game.getSnapshot();
assert.equal(snapshot.controller.legacyActive, true);
assert.equal(elements.get('controllerBadge').textContent, 'KEY / Legacy');

elements.get('bgmVolumeMobile').value = '0.2';
elements.get('bgmVolumeMobile').dispatchEvent({ type: 'input' });
assert.equal(game.getSnapshot().audio.volume, 0.2, 'mobile BGM volume should update the shared setting');
assert.equal(elements.get('bgmVolume').value, '0.2');
elements.get('sfxVolumeMobile').value = '0.3';
elements.get('sfxVolumeMobile').dispatchEvent({ type: 'input' });
assert.equal(game.getSnapshot().audio.sfxVolume, 0.3, 'mobile SFX volume should update the shared setting');
assert.equal(elements.get('sfxVolume').value, '0.3');

document.hidden = true;
document.dispatchEvent({ type: 'visibilitychange' });
snapshot = game.getSnapshot();
assert.equal(snapshot.audio.playing, false, 'backgrounding should pause BGM');
assert.equal(snapshot.audio.sfxReady, false, 'backgrounding should suspend Web Audio');
document.hidden = false;
document.dispatchEvent({ type: 'visibilitychange' });
window.dispatchEvent({ type: 'pointerdown', target: { closest: () => null } });
assert.equal(game.getSnapshot().audio.sfxReady, true, 'the next trusted gesture should resume SFX');

game.pauseBgm();
assert.equal(game.getSnapshot().audio.playing, false, 'BGM pause should be safe');
const sfxStartsBeforeDisable = oscillatorStartCount + noiseStartCount;
game.setSfxEnabled(false);
assert.equal(game.playSfx('swordSwing'), false, 'disabled SFX must not schedule audio');
assert.equal(oscillatorStartCount + noiseStartCount, sfxStartsBeforeDisable);
assert.equal(game.getSnapshot().audio.sfxEnabled, false);
assert.ok(locomotionDrawCalls > 0, 'directional locomotion sheets should be rendered during gameplay');
assert.ok(directionalAttackDrawCalls > 0, 'directional sword sheet should be rendered during attacks');
assert.ok(directionalAirDrawCalls > 0, 'directional jump, air slash, and plunge sheets should render');
assert.ok(slimeAnimationDrawCalls > 0, 'animated slime sprite sheet should be rendered during gameplay');

console.log('Smoke test passed: sampled combat SFX, directional jump/aerial/plunge combat, sheathed locomotion, BGM, charged skills, Steam Input, and pause-state handling.');
