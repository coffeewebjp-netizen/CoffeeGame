'use strict';

const canvas = document.getElementById('game');
const ctx = canvas.getContext('2d');
const eventLogEl = document.getElementById('eventLog');
const goalBadgeEl = document.getElementById('goalBadge');
const expStatEl = document.getElementById('expStat');
const goldStatEl = document.getElementById('goldStat');
const jellyStatEl = document.getElementById('jellyStat');
const resultOverlayEl = document.getElementById('resultOverlay');
const resultKickerEl = document.getElementById('resultKicker');
const resultTitleEl = document.getElementById('resultTitle');
const resultSummaryEl = document.getElementById('resultSummary');
const retryButton = document.getElementById('retryButton');
const attackButton = document.getElementById('attackButton');
const jumpButton = document.getElementById('jumpButton');
const specialButton = document.getElementById('specialButton');
const magicButton = document.getElementById('magicButton');
const specialCostLabelEl = document.getElementById('specialCostLabel');
const magicCostLabelEl = document.getElementById('magicCostLabel');
const respawnEnemyButton = document.getElementById('respawnEnemyButton');
const resetTuningButton = document.getElementById('resetTuningButton');
const startOverlayEl = document.getElementById('startOverlay');
const startButton = document.getElementById('startButton');
const tuningPanelEl = document.getElementById('tuningPanel');
const levelStatusEl = document.getElementById('levelStatus');
const hpStatusEl = document.getElementById('hpStatus');
const expStatusEl = document.getElementById('expStatus');
const staminaStatusEl = document.getElementById('staminaStatus');
const mpStatusEl = document.getElementById('mpStatus');
const enemyHpStatusEl = document.getElementById('enemyHpStatus');
const controllerCompactStatusEl = document.getElementById('controllerCompactStatus');
const controllerBadgeEl = document.getElementById('controllerBadge');
const controllerNameEl = document.getElementById('controllerName');
const controllerLastInputEl = document.getElementById('controllerLastInput');
const controllerAttackMapEl = document.getElementById('controllerAttackMap');
const controllerJumpMapEl = document.getElementById('controllerJumpMap');
const controllerSpecialMapEl = document.getElementById('controllerSpecialMap');
const controllerMagicMapEl = document.getElementById('controllerMagicMap');
const controllerConfirmMapEl = document.getElementById('controllerConfirmMap');
const inputBindingPanelEl = document.getElementById('inputBindingPanel');
const rebindStatusEl = document.getElementById('rebindStatus');
const resetBindingsButton = document.getElementById('resetBindingsButton');
const attackBindingHintEl = document.getElementById('attackBindingHint');
const jumpBindingHintEl = document.getElementById('jumpBindingHint');
const specialBindingHintEl = document.getElementById('specialBindingHint');
const magicBindingHintEl = document.getElementById('magicBindingHint');
const confirmBindingHintEl = document.getElementById('confirmBindingHint');
const gamepadBindingHintEl = document.getElementById('gamepadBindingHint');
const bgmAudioEl = document.getElementById('bgmAudio');
const bgmToggleButton = document.getElementById('bgmToggleButton');
const bgmVolumeInput = document.getElementById('bgmVolume');
const bgmStatusEl = document.getElementById('bgmStatus');
const sfxToggleButton = document.getElementById('sfxToggleButton');
const sfxVolumeInput = document.getElementById('sfxVolume');
const bgmVolumeMobileInput = document.getElementById('bgmVolumeMobile');
const sfxVolumeMobileInput = document.getElementById('sfxVolumeMobile');

const W = 960;
const H = 540;
const TAU = Math.PI * 2;
const SPRITE_COLUMNS = 4;
const HERO_LOCOMOTION_ROWS = 3;
const HERO_ATTACK_ROWS = 3;
const SLIME_ANIMATION_ROWS = 2;
const SWORD_ANIMATION_DURATION = 0.32;
const AIR_SLASH_ANIMATION_DURATION = 0.38;

const DEFAULT_CONFIG = Object.freeze({
  playerMaxHp: 24,
  playerMaxMp: 12,
  playerWalkSpeed: 155,
  playerRunSpeed: 245,
  runHoldTime: 0.65,
  swordDamage: 3,
  swordRange: 78,
  swordCooldown: 0.34,
  jumpVelocity: 480,
  jumpGravity: 1180,
  airSlashDamage: 4,
  airSlashRange: 94,
  plungeDamage: 8,
  plungeRadius: 118,
  plungeSpeed: 760,
  plungeLandingLag: 0.48,
  staminaMax: 100,
  staminaPerHit: 25,
  specialDamage: 12,
  specialRange: 142,
  specialCost: 100,
  specialChargeTime: 0.8,
  magicDamage: 5,
  magicCost: 4,
  magicChargeTime: 0.65,
  magicSpeed: 440,
  mpRegen: 0.45,
  slimeHp: 12,
  slimeDamage: 2,
  slimeSpeed: 68,
  slimeAttackInterval: 2.4,
  slimeWindup: 0.55,
  firstLevelExp: 3,
  goalKills: 5,
  respawnDelay: 0.65
});

const INPUT_BINDINGS_STORAGE_KEY = 'coffeeGame.inputBindings.v1';
const AUDIO_SETTINGS_STORAGE_KEY = 'coffeeGame.audioSettings.v1';
const SFX_SAMPLE_FILES = Object.freeze({
  katana: 'assets/katana-slash1.mp3',
  magicWind: 'assets/magic-wind2.mp3'
});
const ACTION_NAMES = Object.freeze({
  attack: '剣',
  jump: 'ジャンプ',
  special: '回転斬り',
  magic: '氷魔法',
  confirm: '決定'
});
const GAMEPAD_BUTTON_NAMES = Object.freeze([
  'A', 'B', 'X', 'Y', 'LB', 'RB', 'LT', 'RT',
  'Select', 'Start', 'L3', 'R3', 'D-pad Up', 'D-pad Down',
  'D-pad Left', 'D-pad Right', 'Home'
]);
const DEFAULT_INPUT_BINDINGS = Object.freeze({
  gamepad: Object.freeze({
    attack: Object.freeze([7]),
    jump: Object.freeze([0]),
    special: Object.freeze([2]),
    magic: Object.freeze([3]),
    confirm: Object.freeze([0, 9])
  }),
  keyboard: Object.freeze({
    attack: Object.freeze(['f']),
    jump: Object.freeze([' ']),
    special: Object.freeze(['q']),
    magic: Object.freeze(['e']),
    confirm: Object.freeze(['enter', 'r'])
  })
});

const config = { ...DEFAULT_CONFIG };
const inputBindings = loadInputBindings();
const audioSettings = loadAudioSettings();
const audioState = {
  playPending: false,
  requestId: 0,
  blocked: false,
  error: '',
  backgroundSuspended: false
};
const sfxState = {
  context: null,
  masterGain: null,
  limiter: null,
  noiseBuffer: null,
  sampleBuffers: Object.create(null),
  samplePromises: Object.create(null),
  sampleErrors: Object.create(null),
  unavailable: false,
  resumePending: false,
  resumeGeneration: 0,
  pendingSounds: [],
  playCounts: Object.create(null)
};

bgmAudioEl.volume = audioSettings.volume;
bgmVolumeInput.value = String(audioSettings.volume);
sfxVolumeInput.value = String(audioSettings.sfxVolume);
bgmVolumeMobileInput.value = String(audioSettings.volume);
sfxVolumeMobileInput.value = String(audioSettings.sfxVolume);

const heroImage = new Image();
heroImage.src = 'assets/hero-sprite.png';
const heroAnimationImage = new Image();
heroAnimationImage.src = 'assets/hero-animation-sheet-v2.png';
const heroLocomotionImages = {
  down: new Image(),
  side: new Image(),
  up: new Image()
};
heroLocomotionImages.down.src = 'assets/hero-locomotion-down-v1.png';
heroLocomotionImages.side.src = 'assets/hero-locomotion-side-v1.png';
heroLocomotionImages.up.src = 'assets/hero-locomotion-up-v1.png';
const heroDirectionalAttackImage = new Image();
heroDirectionalAttackImage.src = 'assets/hero-attack-directional-v1.png';
const heroJumpImage = new Image();
heroJumpImage.src = 'assets/hero-jump-directional-v1.png';
const heroAirSlashImage = new Image();
heroAirSlashImage.src = 'assets/hero-air-slash-directional-v1.png';
const heroPlungeImage = new Image();
heroPlungeImage.src = 'assets/hero-plunge-directional-v1.png';
const slimeImage = new Image();
slimeImage.src = 'assets/slime-sprite.png';
const slimeAnimationImage = new Image();
slimeAnimationImage.src = 'assets/slime-animation-v1.png';

const state = {
  mode: 'playing',
  elapsed: 0,
  kills: 0,
  gold: 0,
  materials: { slimeJelly: 0 },
  respawnTimer: 0,
  finishTimer: 0,
  shake: 0,
  hitStop: 0,
  message: 'スライムが現れた。まずは剣を振ってみよう。',
  messageTimer: 4,
  uiTimer: 0,
  counts: {
    sword: 0,
    jumps: 0,
    airSlashes: 0,
    plunges: 0,
    special: 0,
    magic: 0,
    damageTaken: 0,
    levelUps: 0
  }
};

const player = {
  x: W * 0.43,
  y: H * 0.58,
  r: 18,
  hp: DEFAULT_CONFIG.playerMaxHp,
  maxHp: DEFAULT_CONFIG.playerMaxHp,
  mp: DEFAULT_CONFIG.playerMaxMp,
  maxMp: DEFAULT_CONFIG.playerMaxMp,
  stamina: 0,
  maxStamina: DEFAULT_CONFIG.staminaMax,
  level: 1,
  xp: 0,
  nextXp: DEFAULT_CONFIG.firstLevelExp,
  attackBonus: 0,
  facing: Math.PI / 2,
  visualDirection: 'down',
  locomotion: 'idle',
  locomotionTime: 0,
  moveDirectionKey: '',
  moveHoldTime: 0,
  invuln: 0,
  actionCooldown: 0,
  attackAnim: 0,
  specialAnim: 0,
  charge: null,
  airHeight: 0,
  verticalVelocity: 0,
  airState: 'grounded',
  airAnimTime: 0,
  airAttackUsed: false,
  landingLag: 0
};

let enemy = null;
let enemySerial = 0;

const slashes = [];
const specialWaves = [];
const impactWaves = [];
const magicBolts = [];
const particles = [];

const input = {
  keys: new Set(),
  touchDirections: new Set(),
  queued: { jump: 0, attack: 0, special: 0, magic: 0 },
  rebinding: null,
  gamepadCaptureReleaseRequired: false,
  mouse: { x: W / 2, y: H / 2, inside: false },
  gamepadMove: { x: 0, y: 0 },
  previousGamepad: { jump: false, attack: false, special: false, magic: false, retry: false, moving: false },
  downHeld: false,
  controller: {
    connected: false,
    compatible: false,
    legacyActive: false,
    index: null,
    id: '',
    mapping: '',
    lastInput: '入力待ち'
  }
};

function clamp(value, min, max) {
  return Math.max(min, Math.min(max, value));
}

function rand(min, max) {
  return min + Math.random() * (max - min);
}

function cloneDefaultInputBindings() {
  return {
    gamepad: {
      attack: [...DEFAULT_INPUT_BINDINGS.gamepad.attack],
      jump: [...DEFAULT_INPUT_BINDINGS.gamepad.jump],
      special: [...DEFAULT_INPUT_BINDINGS.gamepad.special],
      magic: [...DEFAULT_INPUT_BINDINGS.gamepad.magic],
      confirm: [...DEFAULT_INPUT_BINDINGS.gamepad.confirm]
    },
    keyboard: {
      attack: [...DEFAULT_INPUT_BINDINGS.keyboard.attack],
      jump: [...DEFAULT_INPUT_BINDINGS.keyboard.jump],
      special: [...DEFAULT_INPUT_BINDINGS.keyboard.special],
      magic: [...DEFAULT_INPUT_BINDINGS.keyboard.magic],
      confirm: [...DEFAULT_INPUT_BINDINGS.keyboard.confirm]
    }
  };
}

function sanitizeInputBindings(candidate) {
  const result = cloneDefaultInputBindings();
  if (!candidate || typeof candidate !== 'object') return result;

  for (const device of ['gamepad', 'keyboard']) {
    for (const action of Object.keys(ACTION_NAMES)) {
      const values = candidate[device]?.[action];
      if (!Array.isArray(values)) continue;
      const sanitized = [];
      for (const rawValue of values) {
        const value = device === 'keyboard' && typeof rawValue === 'string'
          ? rawValue.toLowerCase()
          : rawValue;
        const valid = device === 'gamepad'
          ? Number.isInteger(value) && value >= 0 && value <= 31
          : typeof value === 'string' && value.length > 0 && value.length <= 24;
        if (valid && !sanitized.includes(value)) sanitized.push(value);
      }
      if (sanitized.length > 0) result[device][action] = sanitized.slice(0, 3);
    }

    const claimedBattleInputs = new Set();
    for (const action of ['jump', 'attack', 'special', 'magic']) {
      let uniqueValues = result[device][action].filter((value) => !claimedBattleInputs.has(value));
      if (!uniqueValues.length) {
        uniqueValues = DEFAULT_INPUT_BINDINGS[device][action]
          .filter((value) => !claimedBattleInputs.has(value));
      }
      result[device][action] = uniqueValues.slice(0, 3);
      for (const value of result[device][action]) claimedBattleInputs.add(value);
    }
  }
  return result;
}

function loadInputBindings() {
  if (typeof localStorage === 'undefined') return cloneDefaultInputBindings();
  try {
    const stored = localStorage.getItem(INPUT_BINDINGS_STORAGE_KEY);
    if (!stored) return cloneDefaultInputBindings();
    const bindings = sanitizeInputBindings(JSON.parse(stored));

    // 旧初期値は複数ボタンをOR判定していたため、トリガーの押し残しが
    // X/Yの立ち上がりを隠すことがあった。保存済みの旧初期値だけ単独割当に移行する。
    if (JSON.stringify(bindings.gamepad.attack) === JSON.stringify([7, 0])) bindings.gamepad.attack = [7];
    if (JSON.stringify(bindings.gamepad.special) === JSON.stringify([6, 2])) bindings.gamepad.special = [2];
    if (JSON.stringify(bindings.gamepad.magic) === JSON.stringify([5, 3])) bindings.gamepad.magic = [3];
    return bindings;
  } catch {
    return cloneDefaultInputBindings();
  }
}

function saveInputBindings() {
  if (typeof localStorage === 'undefined') return;
  try {
    localStorage.setItem(INPUT_BINDINGS_STORAGE_KEY, JSON.stringify(inputBindings));
  } catch {
    // Private browsing or storage denial should not stop the game.
  }
}

function loadAudioSettings() {
  const defaults = { enabled: true, volume: 0.35, sfxEnabled: true, sfxVolume: 0.55 };
  if (typeof localStorage === 'undefined') return defaults;
  try {
    const stored = JSON.parse(localStorage.getItem(AUDIO_SETTINGS_STORAGE_KEY) || 'null');
    if (!stored || typeof stored !== 'object') return defaults;
    const storedVolume = Number(stored.volume);
    const storedSfxVolume = Number(stored.sfxVolume);
    return {
      enabled: stored.enabled !== false,
      volume: Number.isFinite(storedVolume) ? clamp(storedVolume, 0, 1) : defaults.volume,
      sfxEnabled: stored.sfxEnabled !== false,
      sfxVolume: Number.isFinite(storedSfxVolume) ? clamp(storedSfxVolume, 0, 1) : defaults.sfxVolume
    };
  } catch {
    return defaults;
  }
}

function saveAudioSettings() {
  if (typeof localStorage === 'undefined') return;
  try {
    localStorage.setItem(AUDIO_SETTINGS_STORAGE_KEY, JSON.stringify(audioSettings));
  } catch {
    // Storage denial must not stop combat or audio playback.
  }
}

function updateAudioUi() {
  const playing = !bgmAudioEl.paused && !bgmAudioEl.ended;
  bgmToggleButton.setAttribute('aria-pressed', String(playing));
  bgmToggleButton.textContent = playing
    ? '♪ BGM ON'
    : audioState.playPending ? '♪ 読込中…' : audioSettings.enabled ? '♪ BGMを再生' : '♪ BGM OFF';
  bgmVolumeInput.value = String(audioSettings.volume);
  bgmVolumeMobileInput.value = String(audioSettings.volume);

  if (audioState.error) {
    bgmStatusEl.textContent = audioState.error;
  } else if (playing) {
    bgmStatusEl.textContent = 'Rituals of the Jade Valley';
  } else if (audioState.playPending) {
    bgmStatusEl.textContent = 'BGMを読み込み中';
  } else if (audioState.blocked && audioSettings.enabled) {
    bgmStatusEl.textContent = '再生ボタンを押してください';
  } else {
    bgmStatusEl.textContent = audioSettings.enabled ? '戦闘開始で再生' : '停止中';
  }
  bgmToggleButton.title = bgmStatusEl.textContent;

  const sfxSupported = Boolean(window.AudioContext || window.webkitAudioContext) && !sfxState.unavailable;
  const sfxReady = sfxState.context?.state === 'running';
  sfxToggleButton.disabled = !sfxSupported;
  sfxToggleButton.setAttribute('aria-pressed', String(sfxSupported && audioSettings.sfxEnabled));
  sfxToggleButton.textContent = sfxSupported
    ? !audioSettings.sfxEnabled ? '✦ SFX OFF' : sfxReady ? '✦ SFX ON' : '✦ SFX待機'
    : '✦ SFX非対応';
  sfxToggleButton.title = sfxSupported
    ? !audioSettings.sfxEnabled
      ? '効果音を有効にする'
      : sfxReady ? '効果音は有効です' : '一度押して効果音を有効化・試聴'
    : 'このブラウザは効果音生成に対応していません';
  sfxVolumeInput.value = String(audioSettings.sfxVolume);
  sfxVolumeMobileInput.value = String(audioSettings.sfxVolume);
  sfxVolumeInput.disabled = !sfxSupported;
  sfxVolumeMobileInput.disabled = !sfxSupported;
}

async function playBgm() {
  if (!audioSettings.enabled || audioState.playPending || !bgmAudioEl.paused) {
    updateAudioUi();
    return !bgmAudioEl.paused;
  }

  const requestId = ++audioState.requestId;
  audioState.playPending = true;
  audioState.error = '';
  try {
    await bgmAudioEl.play();
    if (requestId !== audioState.requestId) {
      if (!audioSettings.enabled || audioState.backgroundSuspended || document.hidden) bgmAudioEl.pause();
      return false;
    }
    if (!audioSettings.enabled || audioState.backgroundSuspended || document.hidden) {
      bgmAudioEl.pause();
      return false;
    }
    audioState.blocked = false;
    return true;
  } catch (error) {
    if (requestId !== audioState.requestId || !audioSettings.enabled || error?.name === 'AbortError') return false;
    audioState.blocked = error?.name === 'NotAllowedError';
    audioState.error = audioState.blocked ? '' : 'BGMを読み込めません';
    return false;
  } finally {
    if (requestId === audioState.requestId) audioState.playPending = false;
    updateAudioUi();
  }
}

function pauseBgm() {
  audioState.requestId += 1;
  audioState.playPending = false;
  bgmAudioEl.pause();
  updateAudioUi();
}

function activateBgmFromGesture() {
  if (audioSettings.enabled && !audioState.backgroundSuspended && !document.hidden && bgmAudioEl.paused) void playBgm();
}

function toggleBgm() {
  if (!bgmAudioEl.paused || audioState.playPending) {
    audioSettings.enabled = false;
    audioState.error = '';
    audioState.blocked = false;
    pauseBgm();
  } else {
    audioSettings.enabled = true;
    audioState.blocked = false;
    audioState.error = '';
    void playBgm();
  }
  saveAudioSettings();
  updateAudioUi();
}

async function preloadSfxSamples(context) {
  if (typeof fetch !== 'function') return;
  for (const [name, url] of Object.entries(SFX_SAMPLE_FILES)) {
    if (sfxState.sampleBuffers[name] || sfxState.samplePromises[name]) continue;
    sfxState.samplePromises[name] = (async () => {
      try {
        const response = await fetch(url);
        if (!response.ok) throw new Error(`HTTP ${response.status}`);
        const encoded = await response.arrayBuffer();
        const buffer = await context.decodeAudioData(encoded.slice(0));
        if (sfxState.context === context && context.state !== 'closed') {
          sfxState.sampleBuffers[name] = buffer;
          delete sfxState.sampleErrors[name];
        }
      } catch (error) {
        sfxState.sampleErrors[name] = error instanceof Error ? error.message : String(error);
      } finally {
        delete sfxState.samplePromises[name];
      }
    })();
  }
}

function ensureSfxContext() {
  if (sfxState.unavailable) return null;
  if (sfxState.context?.state === 'closed') {
    try { sfxState.masterGain?.disconnect(); } catch {}
    try { sfxState.limiter?.disconnect(); } catch {}
    sfxState.context = null;
    sfxState.masterGain = null;
    sfxState.limiter = null;
    sfxState.noiseBuffer = null;
    sfxState.sampleBuffers = Object.create(null);
    sfxState.samplePromises = Object.create(null);
  }
  if (sfxState.context) return sfxState.context;

  const AudioContextClass = window.AudioContext || window.webkitAudioContext;
  if (!AudioContextClass) {
    sfxState.unavailable = true;
    updateAudioUi();
    return null;
  }

  try {
    const context = new AudioContextClass();
    const masterGain = context.createGain();
    masterGain.gain.setValueAtTime(audioSettings.sfxEnabled ? audioSettings.sfxVolume : 0, context.currentTime);
    const limiter = context.createDynamicsCompressor?.() || null;
    if (limiter) {
      limiter.threshold.setValueAtTime(-12, context.currentTime);
      limiter.knee.setValueAtTime(8, context.currentTime);
      limiter.ratio.setValueAtTime(12, context.currentTime);
      limiter.attack.setValueAtTime(0.003, context.currentTime);
      limiter.release.setValueAtTime(0.18, context.currentTime);
      masterGain.connect(limiter);
      limiter.connect(context.destination);
    } else {
      masterGain.connect(context.destination);
    }
    context.onstatechange = () => updateAudioUi();
    sfxState.context = context;
    sfxState.masterGain = masterGain;
    sfxState.limiter = limiter;
    void preloadSfxSamples(context);
    return context;
  } catch {
    sfxState.unavailable = true;
    updateAudioUi();
    return null;
  }
}

async function activateSfxFromGesture() {
  if (!audioSettings.sfxEnabled) return false;
  const generation = ++sfxState.resumeGeneration;
  sfxState.resumePending = true;
  const context = ensureSfxContext();
  if (!context) {
    sfxState.resumePending = false;
    sfxState.pendingSounds.length = 0;
    return false;
  }
  void preloadSfxSamples(context);
  try {
    if (context.state === 'suspended' || context.state === 'interrupted') await context.resume();
    if (generation !== sfxState.resumeGeneration) {
      if (document.hidden && context.state === 'running') await context.suspend?.();
      return false;
    }
    if (document.hidden) {
      sfxState.resumePending = false;
      sfxState.pendingSounds.length = 0;
      if (context.state === 'running') await context.suspend?.();
      updateAudioUi();
      return false;
    }
    const ready = context.state === 'running';
    sfxState.resumePending = false;
    if (ready) flushPendingSfx();
    else sfxState.pendingSounds.length = 0;
    updateAudioUi();
    return ready;
  } catch {
    sfxState.resumePending = false;
    sfxState.pendingSounds.length = 0;
    updateAudioUi();
    return false;
  }
}

function activateAudioFromGesture() {
  if (state.mode === 'playing' || state.mode === 'finishing') activateBgmFromGesture();
  void activateSfxFromGesture();
}

function getSfxNoiseBuffer(context) {
  if (sfxState.noiseBuffer) return sfxState.noiseBuffer;
  const length = Math.max(1, Math.floor(context.sampleRate * 0.5));
  const buffer = context.createBuffer(1, length, context.sampleRate);
  const data = buffer.getChannelData(0);
  for (let index = 0; index < data.length; index += 1) data[index] = Math.random() * 2 - 1;
  sfxState.noiseBuffer = buffer;
  return buffer;
}

function flushPendingSfx() {
  if (sfxState.context?.state !== 'running') return;
  const now = performance.now();
  const pending = sfxState.pendingSounds.splice(0, 1)
    .filter((item) => now - item.queuedAt <= 600);
  sfxState.pendingSounds.length = 0;
  for (const item of pending) playSfx(item.name);
}

function scheduleSfxTone(startHz, endHz, duration, volume, type = 'sine', delay = 0) {
  const context = sfxState.context;
  if (!context || context.state !== 'running' || !sfxState.masterGain) return false;
  let oscillator = null;
  let gain = null;
  try {
    const startAt = context.currentTime + delay;
    const stopAt = startAt + duration;
    oscillator = context.createOscillator();
    gain = context.createGain();
    oscillator.type = type;
    oscillator.frequency.setValueAtTime(Math.max(1, startHz), startAt);
    oscillator.frequency.exponentialRampToValueAtTime(Math.max(1, endHz), stopAt);
    gain.gain.setValueAtTime(0.0001, startAt);
    gain.gain.linearRampToValueAtTime(volume, startAt + Math.min(0.012, duration * 0.2));
    gain.gain.exponentialRampToValueAtTime(0.0001, stopAt);
    oscillator.connect(gain);
    gain.connect(sfxState.masterGain);
    oscillator.onended = () => {
      try { oscillator.disconnect(); } catch {}
      try { gain.disconnect(); } catch {}
    };
    oscillator.start(startAt);
    oscillator.stop(stopAt + 0.02);
    return true;
  } catch {
    try { oscillator?.disconnect(); } catch {}
    try { gain?.disconnect(); } catch {}
    return false;
  }
}

function scheduleSfxNoise(duration, volume, filterType = 'bandpass', startHz = 1200, endHz = 500, delay = 0) {
  const context = sfxState.context;
  if (!context || context.state !== 'running' || !sfxState.masterGain) return false;
  let source = null;
  let filter = null;
  let gain = null;
  try {
    const startAt = context.currentTime + delay;
    const stopAt = startAt + duration;
    source = context.createBufferSource();
    filter = context.createBiquadFilter();
    gain = context.createGain();
    source.buffer = getSfxNoiseBuffer(context);
    filter.type = filterType;
    filter.frequency.setValueAtTime(Math.max(1, startHz), startAt);
    filter.frequency.exponentialRampToValueAtTime(Math.max(1, endHz), stopAt);
    filter.Q.value = filterType === 'bandpass' ? 1.2 : 0.7;
    gain.gain.setValueAtTime(0.0001, startAt);
    gain.gain.linearRampToValueAtTime(volume, startAt + Math.min(0.008, duration * 0.15));
    gain.gain.exponentialRampToValueAtTime(0.0001, stopAt);
    source.connect(filter);
    filter.connect(gain);
    gain.connect(sfxState.masterGain);
    source.onended = () => {
      try { source.disconnect(); } catch {}
      try { filter.disconnect(); } catch {}
      try { gain.disconnect(); } catch {}
    };
    source.start(startAt);
    source.stop(stopAt + 0.02);
    return true;
  } catch {
    try { source?.disconnect(); } catch {}
    try { filter?.disconnect(); } catch {}
    try { gain?.disconnect(); } catch {}
    return false;
  }
}

function playSfxSample(name, { volume = 0.6, playbackRate = 1, delay = 0, offset = 0 } = {}) {
  const context = sfxState.context;
  const buffer = sfxState.sampleBuffers[name];
  if (!context || context.state !== 'running' || !sfxState.masterGain || !buffer) return false;
  let source = null;
  let gain = null;
  try {
    source = context.createBufferSource();
    gain = context.createGain();
    source.buffer = buffer;
    source.playbackRate.value = playbackRate;
    gain.gain.setValueAtTime(volume, context.currentTime + delay);
    source.connect(gain);
    gain.connect(sfxState.masterGain);
    source.onended = () => {
      try { source.disconnect(); } catch {}
      try { gain.disconnect(); } catch {}
    };
    source.start(context.currentTime + delay, offset);
    return true;
  } catch {
    try { source?.disconnect(); } catch {}
    try { gain?.disconnect(); } catch {}
    return false;
  }
}

function scheduleSwordWhoosh(delay = 0, intensity = 1, wide = false) {
  scheduleSfxNoise(
    wide ? 0.24 : 0.17,
    0.26 * intensity,
    'bandpass',
    wide ? 3800 : 3200,
    wide ? 430 : 680,
    delay
  );
  scheduleSfxNoise(0.085, 0.1 * intensity, 'highpass', 6800, 2100, delay + 0.012);
  scheduleSfxTone(wide ? 540 : 410, wide ? 82 : 118, wide ? 0.2 : 0.145, 0.065 * intensity, 'triangle', delay);
}

function scheduleBladeRing(delay = 0, intensity = 1, pitchScale = 1) {
  scheduleSfxNoise(0.055, 0.15 * intensity, 'highpass', 7200, 2800, delay);
  scheduleSfxTone(1280 * pitchScale, 1120 * pitchScale, 0.18, 0.12 * intensity, 'sine', delay);
  scheduleSfxTone(2030 * pitchScale, 1760 * pitchScale, 0.14, 0.075 * intensity, 'triangle', delay + 0.004);
  scheduleSfxTone(2860 * pitchScale, 2420 * pitchScale, 0.1, 0.05 * intensity, 'sine', delay + 0.008);
}

function scheduleIceChime(rootHz, delay = 0, intensity = 1) {
  scheduleSfxTone(rootHz, rootHz * 0.96, 0.3, 0.09 * intensity, 'sine', delay);
  scheduleSfxTone(rootHz * 1.5, rootHz * 1.42, 0.23, 0.065 * intensity, 'triangle', delay + 0.012);
  scheduleSfxTone(rootHz * 2.08, rootHz * 1.92, 0.16, 0.04 * intensity, 'sine', delay + 0.024);
}

function scheduleIceShatter(delay = 0, intensity = 1) {
  scheduleSfxNoise(0.07, 0.21 * intensity, 'highpass', 7600, 2600, delay);
  scheduleSfxNoise(0.055, 0.16 * intensity, 'bandpass', 6100, 1900, delay + 0.045);
  scheduleSfxNoise(0.05, 0.12 * intensity, 'highpass', 8200, 3100, delay + 0.088);
  scheduleSfxTone(2450, 1280, 0.11, 0.1 * intensity, 'triangle', delay);
  scheduleSfxTone(1860, 720, 0.15, 0.085 * intensity, 'sine', delay + 0.038);
  scheduleSfxTone(3120, 1540, 0.09, 0.055 * intensity, 'sine', delay + 0.072);
}

function playSfx(name) {
  const context = sfxState.context;
  if (!audioSettings.sfxEnabled || !context) return false;
  if (context.state !== 'running') {
    if (sfxState.resumePending && name === 'battleStart') {
      sfxState.pendingSounds.splice(0, sfxState.pendingSounds.length, {
        name,
        queuedAt: performance.now()
      });
      return true;
    }
    return false;
  }
  sfxState.playCounts[name] = (sfxState.playCounts[name] || 0) + 1;

  if (name === 'swordSwing') {
    // 空振りは刃の金属音を足さず、空気を裂く音だけにする。
    scheduleSwordWhoosh(0, 1, false);
  } else if (name === 'swordHit') {
    // 命中時は実録の刀音を主役にし、低い衝撃だけを薄く補う。
    if (!playSfxSample('katana', { volume: 0.74, playbackRate: 1.02 })) scheduleBladeRing(0, 1, 1);
    scheduleSfxNoise(0.075, 0.16, 'bandpass', 4300, 1150, 0.012);
    scheduleSfxTone(165, 72, 0.1, 0.13, 'sine', 0.008);
  } else if (name === 'heavyHit') {
    scheduleBladeRing(0, 1.08, 0.82);
    scheduleSfxNoise(0.18, 0.31, 'lowpass', 980, 180);
    scheduleSfxTone(118, 42, 0.24, 0.24, 'sawtooth');
  } else if (name === 'specialCharge') {
    scheduleSfxTone(135, 390, 0.38, 0.095, 'sawtooth');
    scheduleSfxTone(360, 640, 0.2, 0.07, 'triangle', 0.06);
    scheduleBladeRing(0.14, 0.38, 0.72);
  } else if (name === 'spin') {
    // 三つの風切りを時間差で重ね、刀が一周する感覚を作る。
    scheduleSwordWhoosh(0, 1.2, true);
    scheduleSwordWhoosh(0.085, 1.02, true);
    scheduleSwordWhoosh(0.17, 0.86, true);
    scheduleBladeRing(0.025, 0.64, 0.76);
    scheduleSfxTone(205, 66, 0.42, 0.16, 'triangle');
    playSfxSample('katana', { volume: 0.58, playbackRate: 0.9 });
    playSfxSample('katana', { volume: 0.46, playbackRate: 1.08, delay: 0.14 });
  } else if (name === 'iceCharge') {
    scheduleSfxNoise(0.34, 0.09, 'highpass', 1700, 5200);
    scheduleIceChime(740, 0, 0.82);
    scheduleIceChime(980, 0.12, 0.64);
    scheduleSfxTone(310, 920, 0.4, 0.055, 'sine');
  } else if (name === 'iceCast') {
    if (!playSfxSample('magicWind', { volume: 0.6, playbackRate: 1.04 })) {
      scheduleSfxNoise(0.3, 0.22, 'highpass', 6200, 1050);
      scheduleSfxNoise(0.17, 0.13, 'bandpass', 3900, 720, 0.035);
    }
    scheduleIceChime(1180, 0, 0.75);
    scheduleSfxTone(1680, 410, 0.32, 0.07, 'triangle');
  } else if (name === 'iceHit') {
    scheduleIceShatter(0, 1.08);
    scheduleSfxTone(190, 74, 0.16, 0.1, 'sine', 0.018);
  } else if (name === 'slimeCharge') {
    scheduleSfxTone(135, 72, 0.2, 0.14, 'triangle');
  } else if (name === 'slimeLunge') {
    scheduleSfxTone(82, 230, 0.18, 0.13, 'sine');
    scheduleSfxNoise(0.12, 0.1, 'lowpass', 520, 260);
  } else if (name === 'playerHit') {
    scheduleSfxNoise(0.15, 0.34, 'lowpass', 900, 180);
    scheduleSfxTone(175, 54, 0.18, 0.24, 'sawtooth');
  } else if (name === 'reward') {
    scheduleSfxTone(880, 990, 0.1, 0.12, 'sine');
    scheduleSfxTone(1175, 1320, 0.13, 0.12, 'sine', 0.085);
  } else if (name === 'levelUp') {
    scheduleSfxTone(523, 660, 0.18, 0.13, 'triangle');
    scheduleSfxTone(659, 830, 0.18, 0.13, 'triangle', 0.11);
    scheduleSfxTone(784, 1047, 0.28, 0.15, 'triangle', 0.22);
  } else if (name === 'battleStart') {
    scheduleSfxTone(330, 440, 0.12, 0.09, 'triangle');
    scheduleSfxTone(440, 660, 0.18, 0.1, 'triangle', 0.1);
  } else if (name === 'victory') {
    scheduleSfxTone(523, 784, 0.3, 0.13, 'triangle');
    scheduleSfxTone(659, 988, 0.32, 0.12, 'triangle', 0.12);
    scheduleSfxTone(784, 1175, 0.4, 0.14, 'triangle', 0.24);
  } else if (name === 'defeat') {
    scheduleSfxTone(220, 110, 0.42, 0.16, 'triangle');
    scheduleSfxTone(165, 73, 0.5, 0.12, 'sine', 0.12);
  } else if (name === 'uiConfirm') {
    scheduleSfxTone(520, 720, 0.08, 0.08, 'sine');
  } else if (name === 'jump') {
    scheduleSfxNoise(0.1, 0.08, 'highpass', 2400, 900);
    scheduleSfxTone(180, 390, 0.13, 0.08, 'triangle');
  } else if (name === 'plungeStart') {
    scheduleSwordWhoosh(0, 0.72, false);
    scheduleSfxTone(230, 82, 0.2, 0.11, 'triangle');
  } else if (name === 'plungeImpact') {
    playSfxSample('katana', { volume: 0.62, playbackRate: 0.82 });
    scheduleSfxNoise(0.28, 0.34, 'lowpass', 760, 90);
    scheduleSfxTone(105, 34, 0.34, 0.28, 'sawtooth');
  } else if (name === 'land') {
    scheduleSfxNoise(0.09, 0.11, 'lowpass', 520, 120);
  }
  return true;
}

function setSfxEnabled(enabled) {
  audioSettings.sfxEnabled = Boolean(enabled);
  if (sfxState.context && sfxState.masterGain) {
    try {
      const now = sfxState.context.currentTime;
      const target = audioSettings.sfxEnabled ? audioSettings.sfxVolume : 0;
      sfxState.masterGain.gain.cancelScheduledValues?.(now);
      sfxState.masterGain.gain.setValueAtTime(sfxState.masterGain.gain.value, now);
      sfxState.masterGain.gain.linearRampToValueAtTime(target, now + 0.025);
    } catch {
      // Muting must never interrupt the game loop.
    }
  }
  if (!audioSettings.sfxEnabled) sfxState.pendingSounds.length = 0;
  saveAudioSettings();
  updateAudioUi();
  if (audioSettings.sfxEnabled) {
    void activateSfxFromGesture().then((ready) => {
      if (ready) playSfx('uiConfirm');
    });
  }
}

function toggleSfx() {
  if (audioSettings.sfxEnabled && sfxState.context?.state !== 'running') {
    void activateSfxFromGesture().then((ready) => {
      if (ready) playSfx('uiConfirm');
    });
    return;
  }
  setSfxEnabled(!audioSettings.sfxEnabled);
}

function getGamepadButtonName(index) {
  return GAMEPAD_BUTTON_NAMES[index] || `Button ${index}`;
}

function getKeyboardKeyName(key) {
  const names = {
    ' ': 'Space',
    arrowup: '↑',
    arrowdown: '↓',
    arrowleft: '←',
    arrowright: '→',
    enter: 'Enter',
    escape: 'Esc'
  };
  return names[key] || (key.length === 1 ? key.toUpperCase() : key);
}

function formatBinding(device, action) {
  const values = inputBindings[device][action];
  if (!values.length) return '未割当';
  return values.map((value) => device === 'gamepad' ? getGamepadButtonName(value) : getKeyboardKeyName(value)).join(' / ');
}

function keyboardActionMatches(action, key) {
  return inputBindings.keyboard[action].includes(key);
}

function gamepadActionPressed(gamepad, action) {
  return inputBindings.gamepad[action].some((index) => isGamepadButtonPressed(gamepad, index));
}

function restoreModeAfterRebinding() {
  if (!['input-paused', 'paused'].includes(state.mode)) return;
  state.mode = tuningPanelEl.open ? 'paused' : 'playing';
}

function cancelRebinding(message = '配置変更をキャンセルしました。') {
  input.rebinding = null;
  restoreModeAfterRebinding();
  updateBindingUi(message);
  updateUi();
}

function applyInputBinding(device, action, value) {
  if (!ACTION_NAMES[action] || !['gamepad', 'keyboard'].includes(device)) return false;
  if (device === 'gamepad' && (!Number.isInteger(value) || value < 0 || value > 31)) return false;
  if (device === 'keyboard') {
    if (typeof value !== 'string' || value.length < 1 || value.length > 24) return false;
    value = value.toLowerCase();
  }
  const previousPrimary = inputBindings[device][action][0];
  const conflictGroup = action === 'confirm' ? ['confirm'] : ['jump', 'attack', 'special', 'magic'];

  for (const otherAction of conflictGroup) {
    if (otherAction === action) continue;
    const withoutNewValue = inputBindings[device][otherAction].filter((existing) => existing !== value);
    if (!withoutNewValue.length && previousPrimary !== undefined && previousPrimary !== value) {
      withoutNewValue.push(previousPrimary);
    }
    inputBindings[device][otherAction] = withoutNewValue;
  }

  inputBindings[device][action] = [value];
  if (device === 'gamepad') input.gamepadCaptureReleaseRequired = true;
  saveInputBindings();
  input.rebinding = null;
  restoreModeAfterRebinding();
  const displayValue = device === 'gamepad' ? getGamepadButtonName(value) : getKeyboardKeyName(value);
  const message = `${ACTION_NAMES[action]}を ${displayValue} に変更しました。`;
  announce(message, 2.2);
  updateBindingUi(message);
  updateUi();
  return true;
}

function beginRebinding(device, action) {
  if (!ACTION_NAMES[action] || !['gamepad', 'keyboard'].includes(device)) return;
  if (input.rebinding?.device === device && input.rebinding?.action === action) {
    cancelRebinding();
    return;
  }
  if (state.mode === 'playing') state.mode = 'input-paused';
  input.rebinding = {
    device,
    action,
    armed: device === 'keyboard'
  };
  rebindStatusEl.textContent = device === 'gamepad'
    ? `${ACTION_NAMES[action]}：いったん全ボタンを離してから、割り当てたいボタンを押してください。`
    : `${ACTION_NAMES[action]}：割り当てたいキーを押してください。Escでキャンセル。`;
  updateBindingUi();
  updateUi();
}

function resetInputBindings() {
  const defaults = cloneDefaultInputBindings();
  inputBindings.gamepad = defaults.gamepad;
  inputBindings.keyboard = defaults.keyboard;
  saveInputBindings();
  input.rebinding = null;
  input.gamepadCaptureReleaseRequired = false;
  restoreModeAfterRebinding();
  const message = '配置を初期状態へ戻しました。';
  announce(message, 2.1);
  updateBindingUi(message);
  updateUi();
}

function bindingsMatchDefaults() {
  return ['gamepad', 'keyboard'].every((device) =>
    Object.keys(ACTION_NAMES).every((action) =>
      JSON.stringify(inputBindings[device][action]) === JSON.stringify(DEFAULT_INPUT_BINDINGS[device][action]))
  );
}

function updateBindingUi(statusMessage = '') {
  for (const button of document.querySelectorAll('[data-bind-device][data-bind-action]')) {
    const { bindDevice, bindAction } = button.dataset;
    const waiting = input.rebinding?.device === bindDevice && input.rebinding?.action === bindAction;
    button.textContent = waiting ? '入力待ち…' : formatBinding(bindDevice, bindAction);
    button.classList.toggle('is-waiting', waiting);
  }

  attackBindingHintEl.textContent = `剣 ${formatBinding('keyboard', 'attack')} / 左クリック`;
  jumpBindingHintEl.textContent = `ジャンプ ${formatBinding('keyboard', 'jump')}`;
  specialBindingHintEl.textContent = `回転斬り ${formatBinding('keyboard', 'special')} / 右クリック`;
  magicBindingHintEl.textContent = `氷魔法 ${formatBinding('keyboard', 'magic')}`;
  confirmBindingHintEl.textContent = `開始 / リトライ ${formatBinding('keyboard', 'confirm')}`;
  gamepadBindingHintEl.textContent = `PAD 左Stick・${formatBinding('gamepad', 'jump')}ジャンプ・${formatBinding('gamepad', 'attack')}剣・${formatBinding('gamepad', 'special')}回転斬り・${formatBinding('gamepad', 'magic')}氷魔法`;
  controllerAttackMapEl.textContent = formatBinding('gamepad', 'attack');
  controllerJumpMapEl.textContent = formatBinding('gamepad', 'jump');
  controllerSpecialMapEl.textContent = formatBinding('gamepad', 'special');
  controllerMagicMapEl.textContent = formatBinding('gamepad', 'magic');
  controllerConfirmMapEl.textContent = formatBinding('gamepad', 'confirm');

  if (!input.rebinding) {
    rebindStatusEl.textContent = statusMessage || (bindingsMatchDefaults()
      ? '現在は初期配置です。'
      : '変更した配置をこのブラウザに保存済みです。');
  }
}

function markLegacyInput(label) {
  input.controller.legacyActive = true;
  input.controller.lastInput = label;
}

function angleDelta(a, b) {
  let delta = a - b;
  while (delta > Math.PI) delta -= TAU;
  while (delta < -Math.PI) delta += TAU;
  return delta;
}

function announce(message, seconds = 2.8) {
  state.message = message;
  state.messageTimer = seconds;
  eventLogEl.textContent = message;
}

function addTextParticle(x, y, text, color = '#ffffff', size = 14, life = 0.7) {
  particles.push({
    x,
    y,
    text,
    color,
    size,
    life,
    maxLife: life,
    vx: rand(-13, 13),
    vy: rand(-54, -34),
    dot: false
  });
}

function addSparks(x, y, color, count = 8, speed = 110) {
  for (let i = 0; i < count; i += 1) {
    const angle = rand(0, TAU);
    const velocity = rand(speed * 0.35, speed);
    particles.push({
      x,
      y,
      text: '',
      color,
      size: rand(2, 4),
      life: rand(0.2, 0.42),
      maxLife: 0.42,
      vx: Math.cos(angle) * velocity,
      vy: Math.sin(angle) * velocity,
      dot: true
    });
  }
}

function getDistanceToEnemy() {
  if (!enemy) return Infinity;
  return Math.hypot(enemy.x - player.x, enemy.y - player.y);
}

function faceEnemy(maxDistance = Infinity) {
  if (!enemy || getDistanceToEnemy() > maxDistance) return false;
  setPlayerFacing(Math.atan2(enemy.y - player.y, enemy.x - player.x));
  return true;
}

function getVisualDirection(angle) {
  const x = Math.cos(angle);
  const y = Math.sin(angle);
  if (Math.abs(y) > Math.abs(x) * 0.72) return y < 0 ? 'up' : 'down';
  return 'side';
}

function setPlayerFacing(angle) {
  player.facing = angle;
  player.visualDirection = getVisualDirection(angle);
}

function getMovementDirectionKey(movement) {
  const sector = Math.round(Math.atan2(movement.y, movement.x) / (Math.PI / 4));
  return String((sector + 8) % 8);
}

function spawnSlime(isFirst = false) {
  if (state.mode !== 'playing' || state.kills >= config.goalKills) return;

  const angle = isFirst ? 0 : rand(0, TAU);
  const distance = isFirst ? 92 : rand(125, 185);
  const candidateAngles = [angle, angle + Math.PI, angle + Math.PI / 2, angle - Math.PI / 2];
  let spawnPoint = null;
  for (const candidateAngle of candidateAngles) {
    const candidateX = clamp(player.x + Math.cos(candidateAngle) * distance, 66, W - 66);
    const candidateY = clamp(player.y + Math.sin(candidateAngle) * distance, 98, H - 58);
    const separation = Math.hypot(candidateX - player.x, candidateY - player.y);
    if (!spawnPoint || separation > spawnPoint.separation) {
      spawnPoint = { x: candidateX, y: candidateY, separation };
    }
  }
  const { x, y } = spawnPoint;

  enemySerial += 1;
  enemy = {
    id: `slime-${enemySerial}`,
    x,
    y,
    r: 28,
    hp: config.slimeHp,
    maxHp: config.slimeHp,
    speed: config.slimeSpeed,
    damage: config.slimeDamage,
    attackTimer: 0.65,
    windup: 0,
    lunge: 0,
    lungeX: 0,
    lungeY: 0,
    lungeHit: false,
    flash: 0,
    rewardApplied: false,
    phase: rand(0, TAU)
  };
}

function resetPlayer() {
  player.x = W * 0.43;
  player.y = H * 0.58;
  player.hp = config.playerMaxHp;
  player.maxHp = config.playerMaxHp;
  player.mp = config.playerMaxMp;
  player.maxMp = config.playerMaxMp;
  player.stamina = 0;
  player.maxStamina = config.staminaMax;
  player.level = 1;
  player.xp = 0;
  player.nextXp = config.firstLevelExp;
  player.attackBonus = 0;
  player.facing = Math.PI / 2;
  player.visualDirection = 'down';
  player.locomotion = 'idle';
  player.locomotionTime = 0;
  player.moveDirectionKey = '';
  player.moveHoldTime = 0;
  player.invuln = 0;
  player.actionCooldown = 0;
  player.attackAnim = 0;
  player.specialAnim = 0;
  player.charge = null;
  player.airHeight = 0;
  player.verticalVelocity = 0;
  player.airState = 'grounded';
  player.airAnimTime = 0;
  player.airAttackUsed = false;
  player.landingLag = 0;
}

function resetRun(startImmediately = true) {
  state.mode = startImmediately ? 'playing' : 'ready';
  state.elapsed = 0;
  state.kills = 0;
  state.gold = 0;
  state.materials.slimeJelly = 0;
  state.respawnTimer = 0;
  state.finishTimer = 0;
  state.shake = 0;
  state.hitStop = 0;
  state.counts.sword = 0;
  state.counts.jumps = 0;
  state.counts.airSlashes = 0;
  state.counts.plunges = 0;
  state.counts.special = 0;
  state.counts.magic = 0;
  state.counts.damageTaken = 0;
  state.counts.levelUps = 0;

  enemy = null;
  slashes.length = 0;
  specialWaves.length = 0;
  impactWaves.length = 0;
  magicBolts.length = 0;
  particles.length = 0;
  input.queued.jump = 0;
  input.queued.attack = 0;
  input.queued.special = 0;
  input.queued.magic = 0;
  input.rebinding = null;
  input.gamepadCaptureReleaseRequired = false;
  input.keys.clear();
  input.touchDirections.clear();
  input.gamepadMove.x = 0;
  input.gamepadMove.y = 0;
  input.previousGamepad.jump = false;
  input.previousGamepad.attack = false;
  input.previousGamepad.special = false;
  input.previousGamepad.magic = false;
  input.previousGamepad.retry = false;
  input.previousGamepad.moving = false;
  input.downHeld = false;
  for (const button of document.querySelectorAll('.dpad-button')) button.classList.remove('is-held');

  resetPlayer();
  resultOverlayEl.hidden = true;
  startOverlayEl.hidden = startImmediately;
  if (startImmediately) tuningPanelEl.open = false;
  announce(
    startImmediately ? 'スライムが現れた。まずは剣を振ってみよう。' : '準備ができたら「戦闘を始める」を押してください。',
    startImmediately ? 4 : 3600
  );
  if (startImmediately) spawnSlime(true);
  if (startImmediately) playSfx('battleStart');
  if (startImmediately) activateBgmFromGesture();
  updateBindingUi();
  updateUi();
  (startImmediately ? canvas : startButton).focus({ preventScroll: true });
}

function resetCurrentEnemy() {
  if (state.mode !== 'playing') return;
  enemy = null;
  magicBolts.length = 0;
  spawnSlime(true);
  announce('現在の調整数値でスライムを再生成した。', 2.2);
}

function gainExperience(amount) {
  player.xp += amount;
  let didLevelUp = false;

  while (player.xp >= player.nextXp) {
    player.xp -= player.nextXp;
    player.level += 1;
    player.nextXp = config.firstLevelExp + (player.level - 1) * 2;
    player.attackBonus += 1;
    player.maxHp += 4;
    player.maxMp += 2;
    player.hp = player.maxHp;
    player.mp = player.maxMp;
    state.counts.levelUps += 1;
    didLevelUp = true;
    addTextParticle(player.x, player.y - 58, `LEVEL ${player.level}`, '#ffe39a', 18, 1.1);
    addSparks(player.x, player.y - 15, '#f6c85f', 20, 150);
    state.shake = Math.max(state.shake, 8);
  }

  return didLevelUp;
}

function grantSlimeRewards(sourceEnemy) {
  if (!sourceEnemy || sourceEnemy.rewardApplied) return false;
  sourceEnemy.rewardApplied = true;

  state.kills += 1;
  state.gold += 1;
  state.materials.slimeJelly += 1;
  const leveled = gainExperience(1);
  playSfx('reward');
  if (leveled) playSfx('levelUp');

  addTextParticle(sourceEnemy.x - 34, sourceEnemy.y - 30, 'EXP +1', '#8deeff', 13, 0.9);
  addTextParticle(sourceEnemy.x, sourceEnemy.y - 48, '1 G', '#f7ce68', 13, 0.9);
  addTextParticle(sourceEnemy.x + 42, sourceEnemy.y - 30, 'ゼリー +1', '#93e9d0', 13, 0.9);

  if (leveled) {
    announce(`Lv ${player.level}。HP・MPが回復し、剣の威力が1上がった。`, 3.5);
  } else {
    announce('EXP +1 / Gold +1 / スライムゼリー +1', 2.7);
  }

  return true;
}

function damageEnemy(amount, knockback = 14, source = 'sword') {
  if (!enemy || enemy.rewardApplied || state.mode !== 'playing') return false;

  enemy.hp -= amount;
  const hitSound = source === 'magic' ? 'iceHit' : source === 'special' ? 'heavyHit' : source === 'plunge' ? null : 'swordHit';
  if (hitSound) playSfx(hitSound);
  enemy.flash = 0.12;
  const dx = enemy.x - player.x;
  const dy = enemy.y - player.y;
  const distance = Math.hypot(dx, dy) || 1;
  enemy.x += (dx / distance) * knockback;
  enemy.y += (dy / distance) * knockback;

  const color = source === 'magic' ? '#9df5ff' : source === 'special' ? '#ffe38b' : source === 'plunge' ? '#f3d7a4' : '#ffffff';
  addTextParticle(enemy.x, enemy.y - 34, `-${amount}`, color, source === 'special' ? 18 : 14, 0.55);
  addSparks(enemy.x, enemy.y - 5, color, source === 'special' ? 18 : 8, source === 'special' ? 180 : 110);
  state.shake = Math.max(state.shake, source === 'special' ? 12 : 5);
  state.hitStop = Math.max(state.hitStop, source === 'special' ? 0.08 : 0.04);

  if (enemy.hp <= 0) {
    const defeated = enemy;
    grantSlimeRewards(defeated);
    addSparks(defeated.x, defeated.y, '#6fe5df', 24, 190);
    enemy = null;
    magicBolts.length = 0;

    if (state.kills >= config.goalKills) {
      state.mode = 'finishing';
      state.finishTimer = 0.85;
    } else {
      state.respawnTimer = config.respawnDelay;
    }
  }

  return true;
}

function beginJump() {
  if (state.mode !== 'playing' || player.charge || player.airHeight > 0 || player.landingLag > 0) return false;
  player.airHeight = 1;
  player.verticalVelocity = config.jumpVelocity;
  player.airState = 'jump';
  player.airAnimTime = 0;
  player.airAttackUsed = false;
  player.actionCooldown = Math.max(player.actionCooldown, 0.1);
  state.counts.jumps += 1;
  playSfx('jump');
  announce('ジャンプ！ 空中で剣、下入力で急降下突き。', 1.35);
  return true;
}

function beginAirSlash() {
  if (player.airHeight <= 0 || player.airState === 'plunge' || player.airAttackUsed) return false;
  faceEnemy(220);
  player.airState = 'airSlash';
  player.airAnimTime = 0;
  player.airAttackUsed = true;
  player.attackAnim = AIR_SLASH_ANIMATION_DURATION;
  player.actionCooldown = 0.3;
  state.counts.sword += 1;
  state.counts.airSlashes += 1;
  playSfx('swordSwing');
  slashes.push({
    x: player.x,
    y: player.y - player.airHeight,
    facing: player.facing,
    range: config.airSlashRange,
    life: 0.24,
    maxLife: 0.24,
    wide: true
  });

  if (!enemy) return true;
  const dx = enemy.x - player.x;
  const dy = enemy.y - player.y;
  const distance = Math.hypot(dx, dy);
  const direction = Math.atan2(dy, dx);
  const withinArc = Math.abs(angleDelta(direction, player.facing)) <= 1.25;
  if (distance <= config.airSlashRange + enemy.r && withinArc) {
    player.stamina = Math.min(player.maxStamina, player.stamina + config.staminaPerHit);
    damageEnemy(config.airSlashDamage + player.attackBonus, 28, 'sword');
  }
  return true;
}

function beginPlunge() {
  if (player.airHeight <= 0 || player.airState === 'plunge' || player.landingLag > 0) return false;
  player.airState = 'plunge';
  player.airAnimTime = 0;
  player.airAttackUsed = true;
  player.verticalVelocity = -Math.max(config.plungeSpeed, Math.abs(player.verticalVelocity));
  player.actionCooldown = 0.2;
  input.queued.attack = 0;
  playSfx('plungeStart');
  announce('急降下突き！ 着地後は一瞬動けない。', 1.35);
  return true;
}

function landPlayer() {
  const wasPlunging = player.airState === 'plunge';
  player.airHeight = 0;
  player.verticalVelocity = 0;
  player.airAnimTime = 0;
  player.airAttackUsed = false;

  if (!wasPlunging) {
    player.airState = 'grounded';
    playSfx('land');
    return;
  }

  player.airState = 'landingLag';
  player.landingLag = config.plungeLandingLag;
  player.actionCooldown = Math.max(player.actionCooldown, config.plungeLandingLag);
  state.counts.plunges += 1;
  impactWaves.push({
    x: player.x,
    y: player.y,
    radius: 12,
    maxRadius: config.plungeRadius,
    life: 0.44,
    maxLife: 0.44
  });
  playSfx('plungeImpact');
  addSparks(player.x, player.y - 2, '#e4d3ab', 22, 210);
  state.shake = Math.max(state.shake, 13);
  state.hitStop = Math.max(state.hitStop, 0.06);
  if (enemy && getDistanceToEnemy() <= config.plungeRadius + enemy.r) {
    damageEnemy(config.plungeDamage + player.attackBonus, 52, 'plunge');
  }
}

function trySwordAttack() {
  if (state.mode !== 'playing' || player.actionCooldown > 0 || player.charge || player.landingLag > 0) return;
  if (player.airHeight > 0) {
    beginAirSlash();
    return;
  }
  faceEnemy(180);

  player.actionCooldown = config.swordCooldown;
  player.attackAnim = SWORD_ANIMATION_DURATION;
  playSfx('swordSwing');
  state.counts.sword += 1;
  slashes.push({
    x: player.x,
    y: player.y,
    facing: player.facing,
    range: config.swordRange,
    life: 0.18,
    maxLife: 0.18
  });

  if (!enemy) return;
  const dx = enemy.x - player.x;
  const dy = enemy.y - player.y;
  const distance = Math.hypot(dx, dy);
  const direction = Math.atan2(dy, dx);
  const withinArc = Math.abs(angleDelta(direction, player.facing)) <= 0.9;

  if (distance <= config.swordRange + enemy.r && withinArc) {
    player.stamina = Math.min(player.maxStamina, player.stamina + config.staminaPerHit);
    damageEnemy(config.swordDamage + player.attackBonus, 20, 'sword');
  } else {
    announce('剣が届かない。スライムへ近づこう。', 1.25);
  }
}

function beginSpecialCharge() {
  if (state.mode !== 'playing' || player.actionCooldown > 0 || player.charge || player.airHeight > 0 || player.landingLag > 0) return;
  if (!enemy) {
    announce('回転斬りを向ける敵がいない。次の出現を待とう。', 1.7);
    return;
  }
  if (player.stamina < config.specialCost) {
    announce(`回転斬りには ST ${config.specialCost} が必要。剣を当てて溜めよう。`, 2.2);
    return;
  }

  faceEnemy(260);
  player.stamina -= config.specialCost;
  player.charge = {
    action: 'special',
    duration: config.specialChargeTime,
    remaining: config.specialChargeTime,
    facing: player.facing,
    targetId: enemy.id
  };
  playSfx('specialCharge');
  player.actionCooldown = config.specialChargeTime;
  input.queued.jump = 0;
  input.queued.attack = 0;
  input.queued.special = 0;
  input.queued.magic = 0;
  announce(`回転斬りをチャージ中… ${config.specialChargeTime.toFixed(2)}秒`, 1.5);
}

function releaseSpinAttack(charge) {
  if (state.mode !== 'playing') return;

  setPlayerFacing(charge.facing);
  player.actionCooldown = 0.58;
  player.specialAnim = 0.52;
  playSfx('spin');
  state.counts.special += 1;
  specialWaves.push({
    x: player.x,
    y: player.y,
    facing: charge.facing,
    rotation: charge.facing,
    radius: config.specialRange,
    maxRadius: config.specialRange,
    fixedRadius: true,
    life: 0.48,
    maxLife: 0.48
  });
  announce('必殺・回転斬り！', 1.8);

  if (enemy && getDistanceToEnemy() <= config.specialRange + enemy.r) {
    damageEnemy(config.specialDamage + player.attackBonus, 46, 'special');
  }
}

function beginMagicCharge() {
  if (state.mode !== 'playing' || player.actionCooldown > 0 || player.charge || player.airHeight > 0 || player.landingLag > 0) return;
  if (player.mp < config.magicCost) {
    announce(`MPが足りない。必要MPは ${config.magicCost}。`, 2.2);
    return;
  }
  if (!enemy) {
    announce('氷魔法を向ける敵がいない。', 1.6);
    return;
  }

  faceEnemy();
  player.mp -= config.magicCost;
  player.charge = {
    action: 'magic',
    duration: config.magicChargeTime,
    remaining: config.magicChargeTime,
    facing: player.facing,
    targetId: enemy.id
  };
  playSfx('iceCharge');
  player.actionCooldown = config.magicChargeTime;
  input.queued.jump = 0;
  input.queued.attack = 0;
  input.queued.special = 0;
  input.queued.magic = 0;
  announce(`氷魔法をチャージ中… ${config.magicChargeTime.toFixed(2)}秒`, 1.4);
}

function releaseIceBolt(charge) {
  if (state.mode !== 'playing') return;

  player.actionCooldown = 0.42;
  playSfx('iceCast');
  state.counts.magic += 1;
  const target = enemy && enemy.id === charge.targetId ? enemy : null;
  const direction = target
    ? Math.atan2(target.y - player.y, target.x - player.x)
    : charge.facing;
  magicBolts.push({
    x: player.x + Math.cos(direction) * 24,
    y: player.y - 16 + Math.sin(direction) * 10,
    vx: Math.cos(direction) * config.magicSpeed,
    vy: Math.sin(direction) * config.magicSpeed,
    targetId: target?.id || null,
    life: 2.2,
    radius: 10,
    damage: config.magicDamage + Math.floor(player.attackBonus * 0.5)
  });
  addSparks(player.x, player.y - 18, '#9df5ff', 12, 105);
  announce('氷魔法・氷晶弾！', 1.5);
}

function damagePlayer(amount, sourceX, sourceY) {
  if (state.mode !== 'playing' || player.invuln > 0) return;

  player.hp = Math.max(0, player.hp - amount);
  playSfx('playerHit');
  player.invuln = 0.68;
  state.counts.damageTaken += amount;
  state.shake = Math.max(state.shake, 10);
  addTextParticle(player.x, player.y - 62, `-${amount}`, '#ff8588', 16, 0.75);
  addSparks(player.x, player.y - 20, '#ff7a7d', 12, 130);

  const dx = player.x - sourceX;
  const dy = player.y - sourceY;
  const distance = Math.hypot(dx, dy) || 1;
  player.x = clamp(player.x + (dx / distance) * 22, 30, W - 30);
  player.y = clamp(player.y + (dy / distance) * 22, 78, H - 34);

  if (player.hp <= 0) {
    player.hp = 0;
    finishRun(false);
  }
}

function finishRun(cleared) {
  player.charge = null;
  player.airHeight = 0;
  player.verticalVelocity = 0;
  player.airState = 'grounded';
  player.landingLag = 0;
  input.queued.jump = 0;
  input.queued.attack = 0;
  input.queued.special = 0;
  input.queued.magic = 0;
  state.mode = cleared ? 'cleared' : 'gameover';
  playSfx(cleared ? 'victory' : 'defeat');
  resultKickerEl.textContent = cleared ? 'TRIAL COMPLETE' : 'TRY AGAIN';
  resultTitleEl.textContent = cleared ? '最初の討伐完了' : '少女は倒れた';
  resultSummaryEl.innerHTML = cleared
    ? `${state.kills}体を ${state.elapsed.toFixed(1)}秒で討伐。<br>Lv ${player.level} / HP ${Math.ceil(player.hp)}/${player.maxHp} / MP ${Math.floor(player.mp)}/${player.maxMp}<br>Gold ${state.gold} / スライムゼリー ${state.materials.slimeJelly}<br>ジャンプ ${state.counts.jumps}回・剣 ${state.counts.sword}回（空中 ${state.counts.airSlashes}）・急降下 ${state.counts.plunges}回<br>回転斬り ${state.counts.special}回・氷魔法 ${state.counts.magic}回`
    : `討伐 ${state.kills}体 / Lv ${player.level} / 被ダメージ ${state.counts.damageTaken}<br>数値を調整して、もう一度試せます。`;
  resultOverlayEl.hidden = false;
  updateUi();
  retryButton.focus({ preventScroll: true });
}

function getMovementVector() {
  let x = 0;
  let y = 0;

  if (input.keys.has('a') || input.keys.has('arrowleft') || input.touchDirections.has('left')) x -= 1;
  if (input.keys.has('d') || input.keys.has('arrowright') || input.touchDirections.has('right')) x += 1;
  if (input.keys.has('w') || input.keys.has('arrowup') || input.touchDirections.has('up')) y -= 1;
  if (input.keys.has('s') || input.keys.has('arrowdown') || input.touchDirections.has('down')) y += 1;

  x += input.gamepadMove.x;
  y += input.gamepadMove.y;

  const length = Math.hypot(x, y);
  if (length > 1) {
    x /= length;
    y /= length;
  }

  return { x, y };
}

function isGamepadButtonPressed(gamepad, index, threshold = 0.35) {
  const button = gamepad.buttons?.[index];
  if (typeof button === 'number') return button >= threshold;
  return Boolean(button && (button.pressed || button.value >= threshold));
}

function clearGamepadState() {
  input.gamepadMove.x = 0;
  input.gamepadMove.y = 0;
  input.previousGamepad.jump = false;
  input.previousGamepad.attack = false;
  input.previousGamepad.special = false;
  input.previousGamepad.magic = false;
  input.previousGamepad.retry = false;
  input.previousGamepad.moving = false;
}

function markControllerInput(label) {
  input.controller.lastInput = label;
}

function pollGamepad() {
  let gamepads = [];
  try {
    gamepads = navigator.getGamepads ? navigator.getGamepads() : [];
  } catch {
    gamepads = [];
  }

  const connectedGamepads = Array.from(gamepads || []).filter((candidate) => candidate && candidate.connected !== false);
  const previousGamepad = connectedGamepads.find((candidate) => candidate.index === input.controller.index);
  const standardGamepad = connectedGamepads.find((candidate) => candidate.mapping === 'standard');
  const gamepad = previousGamepad?.mapping === 'standard'
    ? previousGamepad
    : standardGamepad || previousGamepad || connectedGamepads[0];
  if (!gamepad) {
    if (input.controller.connected) {
      announce('コントローラーが切断されました。', 2.2);
      input.controller.lastInput = '切断';
    }
    input.controller.connected = false;
    input.controller.compatible = false;
    input.controller.index = null;
    input.controller.id = '';
    input.controller.mapping = '';
    clearGamepadState();
    return;
  }

  const newlyConnected = !input.controller.connected
    || input.controller.index !== gamepad.index
    || input.controller.id !== gamepad.id;
  input.controller.connected = true;
  input.controller.compatible = gamepad.mapping === 'standard';
  input.controller.index = gamepad.index;
  input.controller.id = gamepad.id || 'Gamepad';
  input.controller.mapping = gamepad.mapping || 'non-standard';
  if (newlyConnected) {
    clearGamepadState();
    markControllerInput('接続を確認');
    announce('コントローラーを認識しました。', 2.2);
  }

  if (!input.controller.compatible) {
    if (!input.controller.legacyActive) {
      input.controller.lastInput = '非標準mapping：Steam InputのGamepadテンプレートを選択';
    }
    clearGamepadState();
    return;
  }

  if (input.gamepadCaptureReleaseRequired) {
    const anyButtonPressed = Array.from(gamepad.buttons || [])
      .some((_, index) => isGamepadButtonPressed(gamepad, index));
    if (!anyButtonPressed) input.gamepadCaptureReleaseRequired = false;
    clearGamepadState();
    return;
  }


  if (input.rebinding?.device === 'gamepad') {
    const pressedButtons = Array.from(gamepad.buttons || [])
      .map((_, index) => index)
      .filter((index) => isGamepadButtonPressed(gamepad, index));
    if (!input.rebinding.armed) {
      if (!pressedButtons.length) {
        input.rebinding.armed = true;
        rebindStatusEl.textContent = `${ACTION_NAMES[input.rebinding.action]}：割り当てたいボタンを押してください。`;
      }
    } else if (pressedButtons.length) {
      applyInputBinding('gamepad', input.rebinding.action, pressedButtons[0]);
    }
    clearGamepadState();
    return;
  }

  const rawAxisX = Number(gamepad.axes?.[0]) || 0;
  const rawAxisY = Number(gamepad.axes?.[1]) || 0;
  const rawMagnitude = Math.hypot(rawAxisX, rawAxisY);
  const normalizedMagnitude = rawMagnitude > 0.16
    ? clamp((rawMagnitude - 0.16) / (1 - 0.16), 0, 1)
    : 0;
  const axisX = rawMagnitude > 0 ? (rawAxisX / rawMagnitude) * normalizedMagnitude : 0;
  const axisY = rawMagnitude > 0 ? (rawAxisY / rawMagnitude) * normalizedMagnitude : 0;
  const dpadX = (isGamepadButtonPressed(gamepad, 15) ? 1 : 0) - (isGamepadButtonPressed(gamepad, 14) ? 1 : 0);
  const dpadY = (isGamepadButtonPressed(gamepad, 13) ? 1 : 0) - (isGamepadButtonPressed(gamepad, 12) ? 1 : 0);
  const dpadActive = dpadX !== 0 || dpadY !== 0;
  input.gamepadMove.x = dpadActive ? dpadX : axisX;
  input.gamepadMove.y = dpadActive ? dpadY : axisY;

  const moving = Math.hypot(input.gamepadMove.x, input.gamepadMove.y) > 0.2;
  const jumpPressed = gamepadActionPressed(gamepad, 'jump');
  const attackPressed = gamepadActionPressed(gamepad, 'attack');
  const specialPressed = gamepadActionPressed(gamepad, 'special');
  const magicPressed = gamepadActionPressed(gamepad, 'magic');
  const confirmPressed = gamepadActionPressed(gamepad, 'confirm') || isGamepadButtonPressed(gamepad, 9);

  if (moving && !input.previousGamepad.moving) markControllerInput('左パッド / Stick：移動');
  if (['ready', 'cleared', 'gameover'].includes(state.mode)) {
    if (confirmPressed && !input.previousGamepad.retry) {
      markControllerInput(`${formatBinding('gamepad', 'confirm')}：開始・リトライ`);
      resetRun(true);
      if (audioSettings.sfxEnabled && sfxState.context?.state !== 'running') {
        announce('効果音を鳴らすには、画面上の「SFX待機」を一度クリック／タップしてください。', 5);
      }
    }
    input.previousGamepad.attack = attackPressed;
    input.previousGamepad.jump = jumpPressed;
    input.previousGamepad.special = specialPressed;
    input.previousGamepad.magic = magicPressed;
    input.previousGamepad.retry = confirmPressed;
    input.previousGamepad.moving = moving;
    return;
  }
  if (jumpPressed && !input.previousGamepad.jump) {
    markControllerInput(`${formatBinding('gamepad', 'jump')}：ジャンプ`);
    if (state.mode === 'playing') queueAction('jump');
  }
  if (attackPressed && !input.previousGamepad.attack) {
    markControllerInput(`${formatBinding('gamepad', 'attack')}：剣`);
    if (state.mode === 'playing') queueAction('attack');
  }
  if (specialPressed && !input.previousGamepad.special) {
    markControllerInput(`${formatBinding('gamepad', 'special')}：回転斬り`);
    if (state.mode === 'playing') queueAction('special');
  }
  if (magicPressed && !input.previousGamepad.magic) {
    markControllerInput(`${formatBinding('gamepad', 'magic')}：氷魔法`);
    if (state.mode === 'playing') queueAction('magic');
  }

  input.previousGamepad.attack = attackPressed;
  input.previousGamepad.jump = jumpPressed;
  input.previousGamepad.special = specialPressed;
  input.previousGamepad.magic = magicPressed;
  input.previousGamepad.retry = confirmPressed;
  input.previousGamepad.moving = moving;
}

function updatePlayer(dt) {
  player.actionCooldown = Math.max(0, player.actionCooldown - dt);
  player.attackAnim = Math.max(0, player.attackAnim - dt);
  player.specialAnim = Math.max(0, player.specialAnim - dt);
  player.invuln = Math.max(0, player.invuln - dt);
  player.mp = Math.min(player.maxMp, player.mp + config.mpRegen * dt);
  if (player.airHeight > 0 || player.airState !== 'grounded') player.airAnimTime += dt;

  if (player.landingLag > 0) {
    player.landingLag = Math.max(0, player.landingLag - dt);
    input.queued.jump = 0;
    input.queued.attack = 0;
    input.queued.special = 0;
    input.queued.magic = 0;
    player.locomotion = 'idle';
    player.moveHoldTime = 0;
    if (player.landingLag <= 0) player.airState = 'grounded';
    input.downHeld = getMovementVector().y > 0.55;
    return;
  }

  if (player.charge) {
    const charge = player.charge;
    charge.remaining = Math.max(0, charge.remaining - dt);
    input.queued.jump = 0;
    input.queued.attack = 0;
    input.queued.special = 0;
    input.queued.magic = 0;
    if (charge.remaining <= 0) {
      player.charge = null;
      if (charge.action === 'special') releaseSpinAttack(charge);
      if (charge.action === 'magic') releaseIceBolt(charge);
    }
  } else {
    const bufferedActions = [
      ['jump', beginJump],
      ['attack', trySwordAttack],
      ['special', beginSpecialCharge],
      ['magic', beginMagicCharge]
    ];
    for (const [action, handler] of bufferedActions) {
      if (input.queued[action] <= 0) continue;
      input.queued[action] = Math.max(0, input.queued[action] - dt);
      if (player.actionCooldown <= 0) {
        handler();
        input.queued[action] = 0;
        if (player.charge || player.airState === 'plunge') break;
      }
    }
  }

  const movement = getMovementVector();
  const downHeld = movement.y > 0.55;
  if (player.airHeight > 0 && downHeld && !input.downHeld) beginPlunge();
  input.downHeld = downHeld;

  if (player.airHeight > 0) {
    player.verticalVelocity -= config.jumpGravity * dt;
    if (player.airState === 'plunge') {
      player.verticalVelocity = Math.min(player.verticalVelocity, -config.plungeSpeed);
    }
    player.airHeight += player.verticalVelocity * dt;
    if (player.airHeight <= 0 && player.verticalVelocity < 0) {
      landPlayer();
    } else if (player.airState === 'airSlash' && player.attackAnim <= 0) {
      player.airState = 'jump';
      player.airAnimTime = 0;
    }
  }

  const movementLocked = player.airState === 'plunge' || player.landingLag > 0;
  if ((movement.x !== 0 || movement.y !== 0) && !movementLocked) {
    const movementDirectionKey = getMovementDirectionKey(movement);
    if (player.moveDirectionKey === movementDirectionKey) {
      player.moveHoldTime += dt;
    } else {
      player.moveDirectionKey = movementDirectionKey;
      player.moveHoldTime = 0;
    }
    const nextLocomotion = player.moveHoldTime >= config.runHoldTime ? 'run' : 'walk';
    if (player.locomotion !== nextLocomotion) player.locomotionTime = 0;
    else player.locomotionTime += dt;
    player.locomotion = nextLocomotion;
    setPlayerFacing(Math.atan2(movement.y, movement.x));

    const movementScale = player.charge ? 0.45 : player.airHeight > 0 ? 0.72 : 1;
    const movementSpeed = nextLocomotion === 'run' ? config.playerRunSpeed : config.playerWalkSpeed;
    player.x = clamp(player.x + movement.x * movementSpeed * movementScale * dt, 30, W - 30);
    player.y = clamp(player.y + movement.y * movementSpeed * movementScale * dt, 78, H - 34);
  } else if (!movementLocked && input.mouse.inside) {
    if (player.locomotion !== 'idle') player.locomotionTime = 0;
    else player.locomotionTime += dt;
    player.locomotion = 'idle';
    player.moveDirectionKey = '';
    player.moveHoldTime = 0;
    const dx = input.mouse.x - player.x;
    const dy = input.mouse.y - player.y;
    if (Math.hypot(dx, dy) > 20) setPlayerFacing(Math.atan2(dy, dx));
  } else {
    if (player.locomotion !== 'idle') player.locomotionTime = 0;
    else player.locomotionTime += dt;
    player.locomotion = 'idle';
    player.moveDirectionKey = '';
    player.moveHoldTime = 0;
  }
}

function updateEnemy(dt) {
  if (!enemy) return;

  enemy.flash = Math.max(0, enemy.flash - dt);
  enemy.attackTimer = Math.max(0, enemy.attackTimer - dt);
  const dx = player.x - enemy.x;
  const dy = player.y - enemy.y;
  const distance = Math.hypot(dx, dy) || 1;
  const nx = dx / distance;
  const ny = dy / distance;

  if (enemy.windup > 0) {
    enemy.windup -= dt;
    if (enemy.windup <= 0) {
      playSfx('slimeLunge');
      enemy.lunge = 0.24;
      enemy.lungeX = nx;
      enemy.lungeY = ny;
      enemy.lungeHit = false;
    }
    return;
  }

  if (enemy.lunge > 0) {
    enemy.lunge -= dt;
    enemy.x += enemy.lungeX * 325 * dt;
    enemy.y += enemy.lungeY * 325 * dt;
    if (!enemy.lungeHit && player.airHeight < 34 && Math.hypot(player.x - enemy.x, player.y - enemy.y) < player.r + enemy.r * 0.72) {
      enemy.lungeHit = true;
      damagePlayer(enemy.damage, enemy.x, enemy.y);
    }
    return;
  }

  if (distance > 72) {
    enemy.x += nx * enemy.speed * dt;
    enemy.y += ny * enemy.speed * dt;
  }

  if (enemy.attackTimer <= 0 && distance < 160) {
    enemy.windup = config.slimeWindup;
    enemy.attackTimer = config.slimeAttackInterval;
    playSfx('slimeCharge');
  }

  enemy.x = clamp(enemy.x, 34, W - 34);
  enemy.y = clamp(enemy.y, 82, H - 30);
}

function updateMagicBolts(dt) {
  for (let i = magicBolts.length - 1; i >= 0; i -= 1) {
    const bolt = magicBolts[i];
    bolt.life -= dt;

    if (enemy && enemy.id === bolt.targetId) {
      const targetAngle = Math.atan2(enemy.y - bolt.y, enemy.x - bolt.x);
      const currentAngle = Math.atan2(bolt.vy, bolt.vx);
      const nextAngle = currentAngle + clamp(angleDelta(targetAngle, currentAngle), -4 * dt, 4 * dt);
      const speed = Math.hypot(bolt.vx, bolt.vy);
      bolt.vx = Math.cos(nextAngle) * speed;
      bolt.vy = Math.sin(nextAngle) * speed;
    }

    bolt.x += bolt.vx * dt;
    bolt.y += bolt.vy * dt;

    if (enemy && enemy.id === bolt.targetId && Math.hypot(enemy.x - bolt.x, enemy.y - bolt.y) < enemy.r + bolt.radius) {
      magicBolts.splice(i, 1);
      damageEnemy(bolt.damage, 25, 'magic');
      if (!enemy) break;
      continue;
    }

    if (bolt.life <= 0 || bolt.x < -30 || bolt.x > W + 30 || bolt.y < -30 || bolt.y > H + 30) {
      magicBolts.splice(i, 1);
    }
  }
}

function updateEffects(dt) {
  state.shake = Math.max(0, state.shake - dt * 24);
  state.messageTimer = Math.max(0, state.messageTimer - dt);

  for (let i = slashes.length - 1; i >= 0; i -= 1) {
    slashes[i].life -= dt;
    if (slashes[i].life <= 0) slashes.splice(i, 1);
  }

  for (let i = specialWaves.length - 1; i >= 0; i -= 1) {
    const wave = specialWaves[i];
    wave.life -= dt;
    if (!wave.fixedRadius) {
      const progress = 1 - wave.life / wave.maxLife;
      wave.radius = wave.maxRadius * Math.sin(progress * Math.PI * 0.5);
    }
    if (wave.life <= 0) specialWaves.splice(i, 1);
  }

  for (let i = impactWaves.length - 1; i >= 0; i -= 1) {
    const wave = impactWaves[i];
    wave.life -= dt;
    const progress = clamp(1 - wave.life / wave.maxLife, 0, 1);
    wave.radius = wave.maxRadius * Math.sin(progress * Math.PI * 0.5);
    if (wave.life <= 0) impactWaves.splice(i, 1);
  }

  for (let i = particles.length - 1; i >= 0; i -= 1) {
    const particle = particles[i];
    particle.life -= dt;
    particle.x += particle.vx * dt;
    particle.y += particle.vy * dt;
    particle.vy += 72 * dt;
    if (particle.life <= 0) particles.splice(i, 1);
  }
}

function update(dt) {
  pollGamepad();

  if (state.hitStop > 0) {
    state.hitStop = Math.max(0, state.hitStop - dt);
    updateEffects(dt * 0.25);
    return;
  }

  if (state.mode === 'playing') {
    state.elapsed += dt;
    updatePlayer(dt);
    updateEnemy(dt);
    updateMagicBolts(dt);

    if (!enemy && state.kills < config.goalKills) {
      state.respawnTimer -= dt;
      if (state.respawnTimer <= 0) spawnSlime();
    }
  } else if (state.mode === 'finishing') {
    state.finishTimer -= dt;
    if (state.finishTimer <= 0) finishRun(true);
  }

  updateEffects(dt);
  state.uiTimer -= dt;
  if (state.uiTimer <= 0) {
    state.uiTimer = 0.08;
    updateUi();
  }
}

function drawBackground() {
  const gradient = ctx.createLinearGradient(0, 0, W, H);
  gradient.addColorStop(0, '#18343a');
  gradient.addColorStop(0.52, '#0f2121');
  gradient.addColorStop(1, '#241a16');
  ctx.fillStyle = gradient;
  ctx.fillRect(0, 0, W, H);

  ctx.fillStyle = '#0c1716aa';
  for (let x = -20; x < W + 80; x += 120) {
    const height = 82 + ((x * 17) % 35);
    ctx.beginPath();
    ctx.moveTo(x, 0);
    ctx.lineTo(x + 46, height);
    ctx.lineTo(x + 95, 0);
    ctx.closePath();
    ctx.fill();
  }

  ctx.strokeStyle = '#6fa09c1f';
  ctx.lineWidth = 1;
  for (let x = -H; x < W + H; x += 58) {
    ctx.beginPath();
    ctx.moveTo(x, 70);
    ctx.lineTo(x + H * 0.55, H);
    ctx.stroke();
  }
  for (let x = -H; x < W + H; x += 58) {
    ctx.beginPath();
    ctx.moveTo(x, H);
    ctx.lineTo(x + H * 0.55, 70);
    ctx.stroke();
  }

  const stones = [
    [110, 155, 54, 20],
    [790, 130, 72, 23],
    [235, 440, 65, 18],
    [720, 430, 88, 23],
    [505, 115, 42, 15]
  ];
  for (const [x, y, rx, ry] of stones) {
    ctx.fillStyle = '#293936';
    ctx.strokeStyle = '#47625d';
    ctx.beginPath();
    ctx.ellipse(x, y, rx, ry, -0.08, 0, TAU);
    ctx.fill();
    ctx.stroke();
  }

  const vignette = ctx.createRadialGradient(W / 2, H / 2, 90, W / 2, H / 2, 570);
  vignette.addColorStop(0, '#0000');
  vignette.addColorStop(1, '#0009');
  ctx.fillStyle = vignette;
  ctx.fillRect(0, 0, W, H);
}

function drawShadow(x, y, radiusX, radiusY, alpha = 0.38) {
  ctx.fillStyle = `rgba(0, 0, 0, ${alpha})`;
  ctx.beginPath();
  ctx.ellipse(x, y, radiusX, radiusY, 0, 0, TAU);
  ctx.fill();
}

function roundedRectPath(x, y, width, height, radius) {
  const r = Math.min(radius, width / 2, height / 2);
  ctx.beginPath();
  ctx.moveTo(x + r, y);
  ctx.lineTo(x + width - r, y);
  ctx.quadraticCurveTo(x + width, y, x + width, y + r);
  ctx.lineTo(x + width, y + height - r);
  ctx.quadraticCurveTo(x + width, y + height, x + width - r, y + height);
  ctx.lineTo(x + r, y + height);
  ctx.quadraticCurveTo(x, y + height, x, y + height - r);
  ctx.lineTo(x, y + r);
  ctx.quadraticCurveTo(x, y, x + r, y);
  ctx.closePath();
}

function drawPlayerFallback() {
  ctx.fillStyle = '#d44d69';
  roundedRectPath(-14, -48, 28, 45, 9);
  ctx.fill();
  ctx.fillStyle = '#66d7f2';
  ctx.beginPath();
  ctx.arc(0, -52, 16, 0, TAU);
  ctx.fill();
  ctx.strokeStyle = '#eafcff';
  ctx.lineWidth = 4;
  ctx.beginPath();
  ctx.moveTo(9, -25);
  ctx.lineTo(34, -8);
  ctx.stroke();
}

function drawPlayer() {
  const blinking = player.invuln > 0 && Math.floor(player.invuln * 24) % 2 === 0;
  const renderY = player.y - player.airHeight;
  const shadowScale = clamp(1 - player.airHeight / 360, 0.55, 1);
  drawShadow(player.x, player.y + 10, 27 * shadowScale, 9 * shadowScale, 0.42 * shadowScale);
  if (blinking) return;

  const attackProgress = player.attackAnim > 0
    ? clamp(1 - player.attackAnim / SWORD_ANIMATION_DURATION, 0, 1)
    : 0;
  const specialProgress = player.specialAnim > 0 ? 1 - player.specialAnim / 0.52 : 0;
  const forward = Math.sin(attackProgress * Math.PI) * 8 + Math.sin(specialProgress * Math.PI) * 12;
  const facingLeft = player.visualDirection === 'side' && Math.cos(player.facing) < 0;
  const spinFlip = player.specialAnim > 0 && Math.cos(specialProgress * TAU * 1.5) < 0 ? -1 : 1;

  ctx.save();
  ctx.translate(
    player.x + Math.cos(player.facing) * forward,
    renderY + Math.sin(player.facing) * forward
  );
  ctx.scale((facingLeft ? -1 : 1) * spinFlip, 1);
  if (player.attackAnim > 0) ctx.rotate(-0.06 * attackProgress);
  if (player.specialAnim > 0) ctx.rotate(Math.sin(specialProgress * TAU * 1.5) * 0.11);

  const usingDirectionalAttack = (player.attackAnim > 0 || player.specialAnim > 0)
    && heroDirectionalAttackImage.complete
    && heroDirectionalAttackImage.naturalWidth > 0;
  const locomotionImage = heroLocomotionImages[player.visualDirection];
  const airImage = player.airState === 'plunge' || player.airState === 'landingLag'
    ? heroPlungeImage
    : player.airState === 'airSlash'
      ? heroAirSlashImage
      : player.airHeight > 0 ? heroJumpImage : null;
  if (airImage?.complete && airImage.naturalWidth > 0) {
    const sourceWidth = airImage.naturalWidth / SPRITE_COLUMNS;
    const sourceHeight = airImage.naturalHeight / HERO_ATTACK_ROWS;
    const row = player.visualDirection === 'down' ? 0 : player.visualDirection === 'side' ? 1 : 2;
    let frame = 0;
    if (player.airState === 'landingLag') {
      frame = 3;
    } else if (player.airState === 'plunge') {
      frame = player.airAnimTime < 0.08 ? 0 : player.airAnimTime < 0.16 ? 1 : 2;
    } else if (player.airState === 'airSlash') {
      const progress = clamp(1 - player.attackAnim / AIR_SLASH_ANIMATION_DURATION, 0, 1);
      frame = Math.min(SPRITE_COLUMNS - 1, Math.floor(progress * SPRITE_COLUMNS));
    } else {
      frame = player.airAnimTime < 0.1 ? 0 : player.verticalVelocity > 80 ? 1 : Math.abs(player.verticalVelocity) <= 80 ? 2 : 3;
    }
    const height = canvas.clientWidth > 0 && canvas.clientWidth < 500 ? 150 : 112;
    ctx.drawImage(
      airImage,
      frame * sourceWidth,
      row * sourceHeight,
      sourceWidth,
      sourceHeight,
      -height / 2,
      -height + 19,
      height,
      height
    );
  } else if (usingDirectionalAttack) {
    const sourceWidth = heroDirectionalAttackImage.naturalWidth / SPRITE_COLUMNS;
    const sourceHeight = heroDirectionalAttackImage.naturalHeight / HERO_ATTACK_ROWS;
    const frame = player.attackAnim > 0
      ? Math.min(SPRITE_COLUMNS - 1, Math.floor(attackProgress * SPRITE_COLUMNS))
      : Math.min(2, 1 + Math.floor(specialProgress * 2));
    const row = player.visualDirection === 'down' ? 0 : player.visualDirection === 'side' ? 1 : 2;
    const height = canvas.clientWidth > 0 && canvas.clientWidth < 500 ? 150 : 112;
    ctx.drawImage(
      heroDirectionalAttackImage,
      frame * sourceWidth,
      row * sourceHeight,
      sourceWidth,
      sourceHeight,
      -height / 2,
      -height + 19,
      height,
      height
    );
  } else if (locomotionImage?.complete && locomotionImage.naturalWidth > 0) {
    const sourceWidth = locomotionImage.naturalWidth / SPRITE_COLUMNS;
    const sourceHeight = locomotionImage.naturalHeight / HERO_LOCOMOTION_ROWS;
    const row = player.locomotion === 'walk' ? 1 : player.locomotion === 'run' ? 2 : 0;
    const frameRate = player.locomotion === 'run' ? 10 : player.locomotion === 'walk' ? 7 : 3.5;
    const frame = Math.floor(player.locomotionTime * frameRate) % SPRITE_COLUMNS;
    const height = canvas.clientWidth > 0 && canvas.clientWidth < 500 ? 150 : 112;
    ctx.drawImage(
      locomotionImage,
      frame * sourceWidth,
      row * sourceHeight,
      sourceWidth,
      sourceHeight,
      -height / 2,
      -height + 19,
      height,
      height
    );
  } else if (heroAnimationImage.complete && heroAnimationImage.naturalWidth > 0) {
    const sourceWidth = heroAnimationImage.naturalWidth / SPRITE_COLUMNS;
    const sourceHeight = heroAnimationImage.naturalHeight / 2;
    const attacking = player.attackAnim > 0;
    const frame = attacking
      ? Math.min(SPRITE_COLUMNS - 1, Math.floor(attackProgress * SPRITE_COLUMNS))
      : Math.floor(state.elapsed * 3.5) % SPRITE_COLUMNS;
    const row = attacking ? 1 : 0;
    const height = canvas.clientWidth > 0 && canvas.clientWidth < 500 ? 150 : 112;
    ctx.drawImage(heroAnimationImage, frame * sourceWidth, row * sourceHeight, sourceWidth, sourceHeight, -height / 2, -height + 19, height, height);
  } else if (heroImage.complete && heroImage.naturalWidth > 0) {
    const height = canvas.clientWidth > 0 && canvas.clientWidth < 500 ? 150 : 112;
    const width = height * (heroImage.naturalWidth / heroImage.naturalHeight);
    ctx.drawImage(heroImage, -width / 2, -height + 19, width, height);
  } else {
    drawPlayerFallback();
  }
  ctx.restore();

  ctx.strokeStyle = '#b9f7f044';
  ctx.lineWidth = 2;
  ctx.beginPath();
  ctx.moveTo(player.x, renderY - 4);
  ctx.lineTo(player.x + Math.cos(player.facing) * 28, renderY - 4 + Math.sin(player.facing) * 28);
  ctx.stroke();
}

function drawChargeEffect() {
  if (!player.charge) return;
  const charge = player.charge;
  const ratio = clamp(1 - charge.remaining / Math.max(0.01, charge.duration), 0, 1);
  const isSpecial = charge.action === 'special';
  const color = isSpecial ? '#ffe083' : '#9df5ff';
  const radius = 38 + Math.sin(state.elapsed * 11) * 2;

  ctx.save();
  ctx.strokeStyle = '#ffffff24';
  ctx.lineWidth = 7;
  ctx.beginPath();
  ctx.arc(player.x, player.y - 9, radius, -Math.PI / 2, -Math.PI / 2 + TAU);
  ctx.stroke();

  ctx.strokeStyle = color;
  ctx.lineWidth = 7;
  ctx.shadowColor = color;
  ctx.shadowBlur = 17;
  ctx.beginPath();
  ctx.arc(player.x, player.y - 9, radius, -Math.PI / 2, -Math.PI / 2 + TAU * ratio);
  ctx.stroke();

  for (let index = 0; index < 4; index += 1) {
    const angle = state.elapsed * (isSpecial ? 5 : -4) + index * TAU / 4;
    ctx.fillStyle = color;
    ctx.beginPath();
    ctx.arc(player.x + Math.cos(angle) * (radius + 7), player.y - 9 + Math.sin(angle) * (radius + 7), 2.5, 0, TAU);
    ctx.fill();
  }

  ctx.shadowBlur = 0;
  ctx.fillStyle = '#f4ffff';
  ctx.font = '700 12px Consolas, "Yu Gothic UI", sans-serif';
  ctx.textAlign = 'center';
  ctx.fillText(`${isSpecial ? '回転斬り' : '氷魔法'} ${Math.round(ratio * 100)}%`, player.x, player.y - 69);
  ctx.restore();
}

function drawSlimeFallback() {
  ctx.fillStyle = '#55dce3';
  ctx.strokeStyle = '#d9ffff';
  ctx.lineWidth = 2;
  roundedRectPath(-31, -39, 62, 48, 22);
  ctx.fill();
  ctx.stroke();
  ctx.fillStyle = '#f3b746';
  ctx.beginPath();
  ctx.arc(-10, -19, 4, 0, TAU);
  ctx.arc(10, -19, 4, 0, TAU);
  ctx.fill();
}

function drawEnemy() {
  if (!enemy) return;

  const bob = Math.sin(state.elapsed * 5.2 + enemy.phase) * 3;
  const scaleX = 1;
  const scaleY = 1;

  drawShadow(enemy.x, enemy.y + 8, 31 * scaleX, 10 * scaleY, 0.4);
  ctx.save();
  ctx.translate(enemy.x, enemy.y + (enemy.windup > 0 || enemy.lunge > 0 ? 0 : bob));
  const lungeFacingLeft = enemy.lunge > 0 && enemy.lungeX < 0;
  ctx.scale(lungeFacingLeft ? -1 : 1, 1);
  if (enemy.flash > 0) ctx.filter = 'brightness(2.3) saturate(0.15)';

  if (slimeAnimationImage.complete && slimeAnimationImage.naturalWidth > 0) {
    const sourceWidth = slimeAnimationImage.naturalWidth / SPRITE_COLUMNS;
    const sourceHeight = slimeAnimationImage.naturalHeight / SLIME_ANIMATION_ROWS;
    let row = 0;
    let frame = Math.floor((state.elapsed + enemy.phase) * 4.5) % SPRITE_COLUMNS;
    if (enemy.windup > 0) {
      const windupProgress = 1 - enemy.windup / Math.max(0.01, config.slimeWindup);
      row = 1;
      frame = windupProgress < 0.68 ? 0 : 1;
    } else if (enemy.lunge > 0) {
      row = 1;
      frame = enemy.lunge > 0.075 ? 2 : 3;
    }
    const width = canvas.clientWidth > 0 && canvas.clientWidth < 500 ? 118 : 84;
    const height = width * (sourceHeight / sourceWidth);
    ctx.drawImage(slimeAnimationImage, frame * sourceWidth, row * sourceHeight, sourceWidth, sourceHeight, -width / 2, -height + 20, width, height);
  } else if (slimeImage.complete && slimeImage.naturalWidth > 0) {
    const width = canvas.clientWidth > 0 && canvas.clientWidth < 500 ? 104 : 72;
    const height = width * (slimeImage.naturalHeight / slimeImage.naturalWidth);
    ctx.drawImage(slimeImage, -width / 2, -height + 10, width, height);
  } else {
    drawSlimeFallback();
  }
  ctx.restore();

  if (enemy.windup > 0) {
    const ratio = 1 - enemy.windup / Math.max(0.01, config.slimeWindup);
    ctx.strokeStyle = '#ff9a7d';
    ctx.lineWidth = 4;
    ctx.beginPath();
    ctx.arc(enemy.x, enemy.y - 12, 39, -Math.PI / 2, -Math.PI / 2 + TAU * ratio);
    ctx.stroke();
  }

  const barWidth = 86;
  const barY = enemy.y - 65;
  ctx.fillStyle = '#05090bcc';
  ctx.fillRect(enemy.x - barWidth / 2, barY, barWidth, 8);
  ctx.fillStyle = '#67d99a';
  ctx.fillRect(enemy.x - barWidth / 2, barY, barWidth * clamp(enemy.hp / enemy.maxHp, 0, 1), 8);
  ctx.strokeStyle = '#d9ffff55';
  ctx.strokeRect(enemy.x - barWidth / 2, barY, barWidth, 8);
  ctx.fillStyle = '#eaf6f4';
  ctx.font = '12px "Yu Gothic UI", sans-serif';
  ctx.textAlign = 'center';
  ctx.fillText('スライム', enemy.x, barY - 7);
}

function drawSlashes() {
  for (const slash of slashes) {
    const alpha = clamp(slash.life / slash.maxLife, 0, 1);
    ctx.save();
    ctx.translate(slash.x, slash.y);
    ctx.rotate(slash.facing);
    ctx.globalAlpha = alpha;
    ctx.strokeStyle = '#eaffff';
    ctx.lineWidth = 9 * alpha + 2;
    ctx.shadowColor = '#84e9f0';
    ctx.shadowBlur = 18;
    ctx.beginPath();
    ctx.arc(0, 0, slash.range, slash.wide ? -1.35 : -0.9, slash.wide ? 1.35 : 0.9);
    ctx.stroke();
    ctx.restore();
  }
  ctx.globalAlpha = 1;
  ctx.shadowBlur = 0;
}

function drawSpecialWaves() {
  for (const wave of specialWaves) {
    const alpha = clamp(wave.life / wave.maxLife, 0, 1);
    const progress = 1 - wave.life / wave.maxLife;
    const rotation = wave.rotation + progress * TAU * 1.45;
    ctx.save();
    ctx.globalAlpha = alpha;
    ctx.strokeStyle = '#ffd76f';
    ctx.lineWidth = 12 * alpha + 2;
    ctx.shadowColor = '#ffe28c';
    ctx.shadowBlur = 25;
    ctx.beginPath();
    ctx.arc(wave.x, wave.y, wave.radius, rotation - 1.25, rotation + 1.25);
    ctx.stroke();
    ctx.lineWidth = 7 * alpha + 2;
    ctx.beginPath();
    ctx.arc(wave.x, wave.y, wave.radius * 0.72, rotation + Math.PI - 0.95, rotation + Math.PI + 0.95);
    ctx.stroke();
    ctx.restore();
  }
  ctx.globalAlpha = 1;
  ctx.shadowBlur = 0;
}

function drawImpactWaves() {
  for (const wave of impactWaves) {
    const alpha = clamp(wave.life / wave.maxLife, 0, 1);
    ctx.save();
    ctx.translate(wave.x, wave.y);
    ctx.globalAlpha = alpha;
    ctx.strokeStyle = '#f1d5a6';
    ctx.shadowColor = '#ffda88';
    ctx.shadowBlur = 18;
    ctx.lineWidth = 8 * alpha + 2;
    ctx.beginPath();
    ctx.ellipse(0, 0, wave.radius, wave.radius * 0.34, 0, 0, TAU);
    ctx.stroke();
    ctx.shadowBlur = 0;
    ctx.strokeStyle = '#9c8068';
    ctx.lineWidth = 3;
    for (let index = 0; index < 8; index += 1) {
      const angle = index * TAU / 8;
      const inner = wave.radius * 0.25;
      const outer = wave.radius * (0.58 + (index % 2) * 0.18);
      ctx.beginPath();
      ctx.moveTo(Math.cos(angle) * inner, Math.sin(angle) * inner * 0.34);
      ctx.lineTo(Math.cos(angle) * outer, Math.sin(angle) * outer * 0.34);
      ctx.stroke();
    }
    ctx.restore();
  }
}

function drawMagicBolts() {
  for (const bolt of magicBolts) {
    const angle = Math.atan2(bolt.vy, bolt.vx);
    ctx.save();
    ctx.translate(bolt.x, bolt.y);
    ctx.rotate(angle);
    ctx.strokeStyle = '#8befff88';
    ctx.lineWidth = 5;
    ctx.beginPath();
    ctx.moveTo(-28, 0);
    ctx.lineTo(-8, 0);
    ctx.stroke();
    ctx.fillStyle = '#8deeff';
    ctx.strokeStyle = '#eaffff';
    ctx.lineWidth = 2;
    ctx.shadowColor = '#76e9ff';
    ctx.shadowBlur = 20;
    ctx.beginPath();
    ctx.moveTo(15, 0);
    ctx.lineTo(0, -9);
    ctx.lineTo(-12, 0);
    ctx.lineTo(0, 9);
    ctx.closePath();
    ctx.fill();
    ctx.stroke();
    ctx.restore();
  }
  ctx.shadowBlur = 0;
}

function drawParticles() {
  ctx.save();
  for (const particle of particles) {
    const alpha = clamp(particle.life / particle.maxLife, 0, 1);
    ctx.globalAlpha = alpha;
    ctx.fillStyle = particle.color;
    if (particle.dot) {
      ctx.beginPath();
      ctx.arc(particle.x, particle.y, particle.size * alpha, 0, TAU);
      ctx.fill();
    } else {
      ctx.font = `700 ${particle.size}px Consolas, "Yu Gothic UI", sans-serif`;
      ctx.textAlign = 'center';
      ctx.fillText(particle.text, particle.x, particle.y);
    }
  }
  ctx.restore();
  ctx.globalAlpha = 1;
}

function drawBar(x, y, width, height, ratio, color, label, value) {
  ctx.fillStyle = '#05090bd9';
  ctx.fillRect(x, y, width, height);
  ctx.fillStyle = color;
  ctx.fillRect(x, y, width * clamp(ratio, 0, 1), height);
  ctx.strokeStyle = '#d7efed66';
  ctx.strokeRect(x, y, width, height);
  ctx.fillStyle = '#eef7f5';
  ctx.font = '700 11px Consolas, "Yu Gothic UI", sans-serif';
  ctx.textAlign = 'left';
  ctx.fillText(`${label} ${value}`, x, y - 5);
}

function drawHud() {
  ctx.fillStyle = '#071014cc';
  ctx.fillRect(12, 12, 244, 76);
  ctx.strokeStyle = '#54777a66';
  ctx.strokeRect(12, 12, 244, 76);
  drawBar(24, 35, 210, 11, player.hp / player.maxHp, '#e46172', 'HP', `${Math.ceil(player.hp)}/${player.maxHp}`);
  drawBar(24, 67, 210, 7, player.xp / player.nextXp, '#6edee6', 'EXP', `${player.xp}/${player.nextXp}`);

  ctx.fillStyle = '#071014cc';
  ctx.fillRect(W - 256, 12, 244, 76);
  ctx.strokeStyle = '#54777a66';
  ctx.strokeRect(W - 256, 12, 244, 76);
  drawBar(W - 234, 35, 210, 10, player.stamina / player.maxStamina, '#edbf55', 'ST', `${Math.floor(player.stamina)}/${player.maxStamina}`);
  drawBar(W - 234, 67, 210, 7, player.mp / player.maxMp, '#9f84eb', 'MP', `${Math.floor(player.mp)}/${player.maxMp}`);

  ctx.fillStyle = '#f1f8f7';
  ctx.textAlign = 'center';
  ctx.font = '700 16px "Yu Gothic UI", sans-serif';
  ctx.fillText(`Lv ${player.level}　スライム ${state.kills} / ${config.goalKills}`, W / 2, 31);
  ctx.font = '12px Consolas, "Yu Gothic UI", sans-serif';
  ctx.fillStyle = '#d9e6e4';
  ctx.fillText(`Gold ${state.gold}　スライムゼリー ${state.materials.slimeJelly}`, W / 2, 52);
}

function drawMessage() {
  if (state.messageTimer <= 0) return;
  const alpha = clamp(state.messageTimer / 0.3, 0, 1);
  ctx.save();
  ctx.globalAlpha = alpha;
  ctx.fillStyle = '#071014dd';
  ctx.fillRect(165, H - 49, W - 330, 30);
  ctx.strokeStyle = '#66c9cf88';
  ctx.strokeRect(165, H - 49, W - 330, 30);
  ctx.fillStyle = '#f0f7f6';
  ctx.font = '13px "Yu Gothic UI", sans-serif';
  ctx.textAlign = 'center';
  ctx.fillText(state.message, W / 2, H - 29);
  ctx.restore();
}

function draw() {
  ctx.save();
  if (state.shake > 0) ctx.translate(rand(-state.shake, state.shake), rand(-state.shake, state.shake));

  drawBackground();
  drawMagicBolts();
  drawSpecialWaves();
  drawImpactWaves();

  const actors = [];
  if (enemy) actors.push({ y: enemy.y, draw: drawEnemy });
  actors.push({ y: player.y, draw: drawPlayer });
  actors.sort((a, b) => a.y - b.y);
  for (const actor of actors) actor.draw();

  drawChargeEffect();
  drawSlashes();
  drawParticles();
  drawHud();
  drawMessage();
  ctx.restore();
}

function updateUi() {
  goalBadgeEl.textContent = `${state.kills} / ${config.goalKills}`;
  expStatEl.textContent = `${player.xp} / ${player.nextXp}`;
  goldStatEl.textContent = String(state.gold);
  jellyStatEl.textContent = String(state.materials.slimeJelly);
  levelStatusEl.textContent = String(player.level);
  hpStatusEl.textContent = `${Math.ceil(player.hp)} / ${player.maxHp}`;
  expStatusEl.textContent = `${player.xp} / ${player.nextXp}`;
  staminaStatusEl.textContent = `${Math.floor(player.stamina)} / ${player.maxStamina}`;
  mpStatusEl.textContent = `${Math.floor(player.mp)} / ${player.maxMp}`;
  enemyHpStatusEl.textContent = enemy ? `${Math.max(0, Math.ceil(enemy.hp))} / ${enemy.maxHp}` : '出現待ち';
  const controllerConnected = input.controller.connected;
  const controllerCompatible = input.controller.compatible;
  const legacyActive = input.controller.legacyActive;
  const controllerStatus = controllerCompatible
    ? '接続済み'
    : legacyActive
      ? 'KEY / Legacy'
      : controllerConnected ? '要設定' : '未検出';
  controllerCompactStatusEl.textContent = controllerStatus;
  controllerCompactStatusEl.classList.toggle('connected', controllerCompatible);
  controllerCompactStatusEl.classList.toggle('legacy', legacyActive && !controllerCompatible);
  controllerCompactStatusEl.classList.toggle('needs-setup', controllerConnected && !controllerCompatible && !legacyActive);
  controllerBadgeEl.textContent = controllerStatus;
  controllerBadgeEl.classList.toggle('connected', controllerCompatible);
  controllerBadgeEl.classList.toggle('legacy', legacyActive && !controllerCompatible);
  controllerBadgeEl.classList.toggle('needs-setup', controllerConnected && !controllerCompatible && !legacyActive);
  controllerNameEl.textContent = controllerCompatible
    ? `${input.controller.id} / mapping: standard`
    : legacyActive
      ? 'キーボード／マウス入力として操作中です。Steam Input Legacyの場合は正常です。'
      : controllerConnected
        ? `${input.controller.id} / Steam InputでGamepadテンプレートを選択してください。`
        : '操作すると入力方式を判定します。Steam Input Legacyはキーボード入力として表示されます。';
  controllerNameEl.title = controllerConnected ? input.controller.id : 'Keyboard / Steam Input Legacy';
  controllerLastInputEl.textContent = input.controller.lastInput;

  const unavailable = state.mode !== 'playing';
  const charging = Boolean(player.charge);
  const chargeRatio = charging
    ? clamp(1 - player.charge.remaining / Math.max(0.01, player.charge.duration), 0, 1)
    : 0;
  const actionLocked = unavailable || player.actionCooldown > 0 || charging || player.landingLag > 0;
  jumpButton.disabled = actionLocked || player.airHeight > 0;
  attackButton.disabled = actionLocked || player.airState === 'plunge' || (player.airHeight > 0 && player.airAttackUsed);
  specialButton.disabled = actionLocked || player.airHeight > 0;
  magicButton.disabled = actionLocked || player.airHeight > 0;
  jumpButton.title = player.landingLag > 0
    ? `着地硬直 ${player.landingLag.toFixed(2)}秒`
    : player.airHeight > 0 ? '空中です' : 'ジャンプ';
  attackButton.title = player.airHeight > 0 ? '空中なで斬り' : '通常攻撃';
  specialButton.classList.toggle('resource-low', player.stamina < config.specialCost);
  magicButton.classList.toggle('resource-low', player.mp < config.magicCost);
  specialButton.classList.toggle('charging', player.charge?.action === 'special');
  magicButton.classList.toggle('charging', player.charge?.action === 'magic');
  specialButton.style.setProperty('--charge-angle', `${player.charge?.action === 'special' ? chargeRatio * 360 : 0}deg`);
  magicButton.style.setProperty('--charge-angle', `${player.charge?.action === 'magic' ? chargeRatio * 360 : 0}deg`);

  specialCostLabelEl.textContent = player.charge?.action === 'special'
    ? `CHARGE ${Math.round(chargeRatio * 100)}%`
    : player.stamina < config.specialCost
      ? `あと ${Math.ceil(config.specialCost - player.stamina)} ST`
      : `${config.specialChargeTime.toFixed(2)}秒 / ST ${config.specialCost}`;
  magicCostLabelEl.textContent = player.charge?.action === 'magic'
    ? `CHARGE ${Math.round(chargeRatio * 100)}%`
    : player.mp < config.magicCost
      ? `あと ${Math.ceil(config.magicCost - player.mp)} MP`
      : `${config.magicChargeTime.toFixed(2)}秒 / MP ${config.magicCost}`;
  specialButton.title = player.charge?.action === 'special'
    ? `回転斬りをチャージ中 ${Math.round(chargeRatio * 100)}%`
    : player.stamina < config.specialCost ? `あと ${Math.ceil(config.specialCost - player.stamina)} ST` : '回転斬りをチャージ';
  magicButton.title = player.charge?.action === 'magic'
    ? `氷魔法をチャージ中 ${Math.round(chargeRatio * 100)}%`
    : player.mp < config.magicCost ? `あと ${Math.ceil(config.magicCost - player.mp)} MP` : '氷魔法をチャージ';
}

function queueAction(action) {
  if (!Object.prototype.hasOwnProperty.call(input.queued, action)) return;
  input.queued[action] = Math.max(input.queued[action], 0.12);
  const button = document.querySelector(`[data-action="${action}"]`);
  if (button) {
    button.classList.add('is-pressed');
    window.setTimeout(() => button.classList.remove('is-pressed'), 90);
  }
}

function initializeTuningControls() {
  const controls = document.querySelectorAll('[data-config]');
  for (const control of controls) {
    const key = control.dataset.config;
    control.value = String(config[key]);
    control.addEventListener('change', () => {
      const min = Number(control.min);
      const max = Number(control.max);
      const parsed = Number(control.value);
      if (!Number.isFinite(parsed)) {
        control.value = String(config[key]);
        return;
      }
      const value = clamp(parsed, Number.isFinite(min) ? min : -Infinity, Number.isFinite(max) ? max : Infinity);
      config[key] = value;
      control.value = String(value);

      if (key === 'goalKills') {
        config.goalKills = Math.max(1, Math.floor(value));
        if (state.mode === 'playing' && state.kills >= config.goalKills) {
          enemy = null;
          magicBolts.length = 0;
          state.mode = 'finishing';
          state.finishTimer = 0.1;
        }
      }
      if (key === 'firstLevelExp' && player.level === 1) {
        player.nextXp = Math.max(player.xp + 1, Math.floor(value));
      }

      const enemySetting = ['slimeHp', 'slimeDamage', 'slimeAttackInterval'].includes(key);
      announce(
        `${control.parentElement.childNodes[0].textContent.trim()}を ${value} に変更。${enemySetting ? '敵の再生成後に反映。' : ''}`,
        enemySetting ? 2.5 : 1.8
      );
      updateUi();
    });
  }
}

function resetTuning() {
  Object.assign(config, DEFAULT_CONFIG);
  for (const control of document.querySelectorAll('[data-config]')) {
    control.value = String(config[control.dataset.config]);
  }
  resetRun();
  announce('調整数値を初期値へ戻した。', 2.1);
}

function bindPointerButton(button, onPress) {
  button.addEventListener('pointerdown', (event) => {
    event.preventDefault();
    button.setPointerCapture?.(event.pointerId);
    onPress(event);
  });
}

window.addEventListener('keydown', (event) => {
  if (event.isComposing) return;
  if (!event.target?.closest?.('.audio-controls')) activateAudioFromGesture();
  const key = event.key.toLowerCase();

  if (input.rebinding && key === 'escape') {
    event.preventDefault();
    cancelRebinding();
    return;
  }

  if (input.rebinding?.device === 'keyboard') {
    event.preventDefault();
    applyInputBinding('keyboard', input.rebinding.action, key);
    return;
  }

  const interactiveTarget = event.target?.matches?.('input, textarea, select, button, summary, a');
  const gameplayTarget = event.target?.matches?.('[data-action], [data-move]');
  if (interactiveTarget && !gameplayTarget) return;
  input.keys.add(key);

  const movementKey = ['w', 'a', 's', 'd', 'arrowup', 'arrowdown', 'arrowleft', 'arrowright'].includes(key);
  let handledAction = false;

  if (!event.repeat) {
    if (['ready', 'cleared', 'gameover'].includes(state.mode) && keyboardActionMatches('confirm', key)) {
      resetRun(true);
      handledAction = true;
    } else if (state.mode === 'playing') {
      if (keyboardActionMatches('jump', key)) {
        queueAction('jump');
        handledAction = true;
      }
      if (keyboardActionMatches('attack', key)) {
        queueAction('attack');
        handledAction = true;
      }
      if (keyboardActionMatches('special', key)) {
        queueAction('special');
        handledAction = true;
      }
      if (keyboardActionMatches('magic', key)) {
        queueAction('magic');
        handledAction = true;
      }
    }
  }

  if (movementKey || handledAction) {
    markLegacyInput(`KEY / Legacy：${getKeyboardKeyName(key)}`);
    event.preventDefault();
  }
});

window.addEventListener('keyup', (event) => {
  input.keys.delete(event.key.toLowerCase());
});

window.addEventListener('blur', () => {
  input.keys.clear();
  input.touchDirections.clear();
  for (const button of document.querySelectorAll('.dpad-button')) button.classList.remove('is-held');
});

canvas.addEventListener('pointermove', (event) => {
  const rect = canvas.getBoundingClientRect();
  input.mouse.x = (event.clientX - rect.left) * (W / rect.width);
  input.mouse.y = (event.clientY - rect.top) * (H / rect.height);
  input.mouse.inside = true;
});

canvas.addEventListener('pointerleave', () => {
  input.mouse.inside = false;
});

canvas.addEventListener('pointerdown', (event) => {
  if (event.pointerType === 'touch') return;
  event.preventDefault();
  markLegacyInput(event.button === 2 ? 'MOUSE / Legacy：回転斬り' : 'MOUSE / Legacy：剣');
  canvas.focus({ preventScroll: true });
  if (event.button === 2) queueAction('special');
  else queueAction('attack');
});

canvas.addEventListener('contextmenu', (event) => event.preventDefault());

for (const button of document.querySelectorAll('[data-action]')) {
  bindPointerButton(button, (event) => {
    if (event.pointerType === 'mouse') markLegacyInput(`MOUSE / Legacy：${ACTION_NAMES[button.dataset.action]}`);
    queueAction(button.dataset.action);
    canvas.focus({ preventScroll: true });
  });
  button.addEventListener('click', (event) => {
    if (event.detail !== 0) return;
    markLegacyInput(`KEY / Legacy：${ACTION_NAMES[button.dataset.action]}`);
    queueAction(button.dataset.action);
    canvas.focus({ preventScroll: true });
  });
}

for (const button of document.querySelectorAll('[data-move]')) {
  const direction = button.dataset.move;
  const release = () => {
    input.touchDirections.delete(direction);
    button.classList.remove('is-held');
  };
  button.addEventListener('pointerdown', (event) => {
    event.preventDefault();
    button.setPointerCapture?.(event.pointerId);
    input.touchDirections.add(direction);
    if (event.pointerType === 'mouse') markLegacyInput('MOUSE / Legacy：移動');
    button.classList.add('is-held');
    canvas.focus({ preventScroll: true });
  });
  button.addEventListener('pointerup', release);
  button.addEventListener('pointercancel', release);
  button.addEventListener('lostpointercapture', release);
  button.addEventListener('keydown', (event) => {
    if (event.key !== 'Enter' && event.key !== ' ') return;
    event.preventDefault();
    markLegacyInput(`KEY / Legacy：${getKeyboardKeyName(event.key.toLowerCase())}`);
    input.touchDirections.add(direction);
    button.classList.add('is-held');
  });
  button.addEventListener('keyup', (event) => {
    if (event.key === 'Enter' || event.key === ' ') release();
  });
}

for (const button of document.querySelectorAll('[data-bind-device][data-bind-action]')) {
  button.addEventListener('click', () => beginRebinding(button.dataset.bindDevice, button.dataset.bindAction));
}

window.addEventListener('pointerdown', (event) => {
  if (!event.target?.closest?.('.audio-controls')) activateAudioFromGesture();
}, { capture: true });

bgmToggleButton.addEventListener('click', toggleBgm);
sfxToggleButton.addEventListener('click', toggleSfx);
function setBgmVolumeFromInput(inputElement) {
  const volume = clamp(Number(inputElement.value), 0, 1);
  audioSettings.volume = volume;
  bgmAudioEl.volume = volume;
  saveAudioSettings();
  updateAudioUi();
}

function setSfxVolumeFromInput(inputElement) {
  const volume = clamp(Number(inputElement.value), 0, 1);
  audioSettings.sfxVolume = volume;
  if (sfxState.context && sfxState.masterGain) {
    try {
      sfxState.masterGain.gain.setValueAtTime(audioSettings.sfxEnabled ? volume : 0, sfxState.context.currentTime);
    } catch {
      // A closed/interrupted context must not stop combat.
    }
  }
  saveAudioSettings();
  updateAudioUi();
}

bgmVolumeInput.addEventListener('input', () => setBgmVolumeFromInput(bgmVolumeInput));
bgmVolumeMobileInput.addEventListener('input', () => setBgmVolumeFromInput(bgmVolumeMobileInput));
sfxVolumeInput.addEventListener('input', () => setSfxVolumeFromInput(sfxVolumeInput));
sfxVolumeMobileInput.addEventListener('input', () => setSfxVolumeFromInput(sfxVolumeMobileInput));
bgmAudioEl.addEventListener('play', updateAudioUi);
bgmAudioEl.addEventListener('pause', updateAudioUi);
bgmAudioEl.addEventListener('error', () => {
  const messages = {
    2: 'BGMの通信に失敗しました',
    3: 'BGMをデコードできません',
    4: 'このブラウザはBGM非対応です'
  };
  audioState.error = messages[bgmAudioEl.error?.code] || 'BGMを読み込めません';
  updateAudioUi();
});

document.addEventListener('visibilitychange', () => {
  if (document.hidden) {
    audioState.backgroundSuspended = true;
    sfxState.resumeGeneration += 1;
    sfxState.resumePending = false;
    sfxState.pendingSounds.length = 0;
    pauseBgm();
    if (sfxState.context && sfxState.context.state !== 'closed') void sfxState.context.suspend?.().catch(() => {});
  } else {
    audioState.backgroundSuspended = false;
    updateAudioUi();
  }
});

window.addEventListener('pagehide', () => {
  audioState.backgroundSuspended = true;
  sfxState.resumeGeneration += 1;
  sfxState.resumePending = false;
  sfxState.pendingSounds.length = 0;
  pauseBgm();
  if (sfxState.context && sfxState.context.state !== 'closed') void sfxState.context.suspend?.().catch(() => {});
});

window.addEventListener('pageshow', () => {
  audioState.backgroundSuspended = false;
  updateAudioUi();
});

inputBindingPanelEl.addEventListener('toggle', () => {
  if (!inputBindingPanelEl.open && input.rebinding) {
    cancelRebinding('パネルを閉じたため、配置変更をキャンセルしました。');
  }
});

retryButton.addEventListener('click', (event) => {
  if (event.detail === 0) markLegacyInput('KEY / Legacy：決定');
  resetRun(true);
});
startButton.addEventListener('click', (event) => {
  if (event.detail === 0) markLegacyInput('KEY / Legacy：決定');
  resetRun(true);
});
respawnEnemyButton.addEventListener('click', resetCurrentEnemy);
resetTuningButton.addEventListener('click', resetTuning);
resetBindingsButton.addEventListener('click', resetInputBindings);
tuningPanelEl.addEventListener('toggle', () => {
  if (tuningPanelEl.open && state.mode === 'playing') {
    state.mode = 'paused';
    announce('調整数値を確認中。戦闘は一時停止しています。', 3600);
    updateUi();
  } else if (!tuningPanelEl.open && state.mode === 'paused' && !input.rebinding) {
    state.mode = 'playing';
    announce('調整を反映して戦闘を再開。', 1.8);
    updateUi();
    canvas.focus({ preventScroll: true });
  }
});

window.CoffeeGamePrototype = Object.freeze({
  getSnapshot() {
    return {
      state: JSON.parse(JSON.stringify(state)),
      player: JSON.parse(JSON.stringify(player)),
      enemy: enemy ? { ...enemy } : null,
      config: { ...config },
      effects: {
        slashes: slashes.length,
        specialWaves: specialWaves.length,
        impactWaves: impactWaves.length,
        magicBolts: magicBolts.length,
        particles: particles.length
      },
      controller: {
        ...input.controller,
        move: { ...input.gamepadMove }
      },
      audio: {
        enabled: audioSettings.enabled,
        volume: audioSettings.volume,
        playing: !bgmAudioEl.paused,
        blocked: audioState.blocked,
        error: audioState.error,
        sfxEnabled: audioSettings.sfxEnabled,
        sfxVolume: audioSettings.sfxVolume,
        sfxReady: sfxState.context?.state === 'running',
        sfxSamplesReady: Object.keys(sfxState.sampleBuffers),
        sfxSampleErrors: { ...sfxState.sampleErrors },
        sfxPlayCounts: { ...sfxState.playCounts }
      }
    };
  },
  resetRun,
  resetCurrentEnemy,
  queueAction,
  setConfig(key, value) {
    if (!Object.prototype.hasOwnProperty.call(config, key) || !Number.isFinite(Number(value))) return false;
    config[key] = Number(value);
    updateUi();
    return true;
  },
  getInputBindings() {
    return JSON.parse(JSON.stringify(inputBindings));
  },
  beginRebinding,
  cancelRebinding,
  setInputBinding(device, action, value) {
    return applyInputBinding(device, action, value);
  },
  resetInputBindings,
  playBgm,
  pauseBgm,
  playSfx,
  activateSfxFromGesture,
  setSfxEnabled,
  debugLegacyInput(label = 'KEY / Legacy：debug') {
    markLegacyInput(label);
    updateUi();
  },
  defeatCurrentSlime() {
    if (!enemy) return false;
    return damageEnemy(enemy.hp, 0, 'debug');
  }
});

let lastFrame = performance.now();
function frame(now) {
  const dt = Math.min(0.033, Math.max(0, (now - lastFrame) / 1000));
  lastFrame = now;
  update(dt);
  draw();
  requestAnimationFrame(frame);
}

initializeTuningControls();
updateAudioUi();
resetRun(false);
requestAnimationFrame(frame);
