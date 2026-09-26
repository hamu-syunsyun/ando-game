// 効果音（音声ファイルなし。WebAudioで鳴らす）
const Sound = (() => {
  let ctx = null;
  let muted = false;
  try { muted = localStorage.getItem('ando-game:muted') === '1'; } catch (e) { /* 使えなくても動く */ }

  function ac() {
    if (!ctx) {
      const C = window.AudioContext || window.webkitAudioContext;
      if (!C) return null;
      ctx = new C();
    }
    if (ctx.state === 'suspended') ctx.resume();
    return ctx;
  }

  function tone(freq, dur, type = 'square', vol = 0.06, when = 0) {
    if (muted) return;
    const c = ac();
    if (!c) return;
    const t = c.currentTime + when;
    const o = c.createOscillator();
    const g = c.createGain();
    o.type = type;
    o.frequency.setValueAtTime(freq, t);
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g);
    g.connect(c.destination);
    o.start(t);
    o.stop(t + dur + 0.02);
  }

  // 周波数を f1 から f2 へすべらせる
  function sweep(f1, f2, dur, type = 'sawtooth', vol = 0.06, when = 0) {
    if (muted) return;
    const c = ac();
    if (!c) return;
    const t = c.currentTime + when;
    const o = c.createOscillator();
    const g = c.createGain();
    o.type = type;
    o.frequency.setValueAtTime(f1, t);
    o.frequency.exponentialRampToValueAtTime(f2, t + dur);
    g.gain.setValueAtTime(vol, t);
    g.gain.exponentialRampToValueAtTime(0.0001, t + dur);
    o.connect(g);
    g.connect(c.destination);
    o.start(t);
    o.stop(t + dur + 0.02);
  }

  return {
    unlock() { if (!muted) ac(); },
    // ボス戦用
    slash() { sweep(900, 260, 0.12, 'sawtooth', 0.035); },
    hit() { sweep(420, 110, 0.12, 'square', 0.05); tone(1800, 0.04, 'square', 0.025); },
    zap() { sweep(1700, 180, 0.3, 'sawtooth', 0.05); },
    boom() { sweep(200, 40, 0.5, 'sawtooth', 0.08); },
    hurt() { sweep(320, 90, 0.25, 'square', 0.07); },
    burst() { [392, 523, 659, 784, 1047].forEach((f, i) => tone(f, 0.18, 'sawtooth', 0.045, i * 0.06)); sweep(2000, 100, 0.8, 'sawtooth', 0.05, 0.3); },
    ok() { tone(880, 0.08); tone(1320, 0.14, 'square', 0.06, 0.07); },
    ng() { tone(150, 0.28, 'sawtooth', 0.07); },
    tick() { tone(1000, 0.05, 'sine', 0.06); },
    click() { tone(620, 0.03, 'triangle', 0.05); },
    clear() { [523, 659, 784, 1047].forEach((f, i) => tone(f, 0.16, 'square', 0.05, i * 0.07)); },
    count() { tone(660, 0.1, 'square', 0.05); },
    go() { tone(990, 0.25, 'square', 0.06); },
    end() { [784, 659, 523].forEach((f, i) => tone(f, 0.25, 'triangle', 0.08, i * 0.14)); },
    pass() { [523, 659, 784, 1047, 1319].forEach((f, i) => tone(f, 0.2, 'triangle', 0.07, i * 0.1)); },
    fail() { [392, 349, 311, 262].forEach((f, i) => tone(f, 0.3, 'sawtooth', 0.05, i * 0.18)); },
    toggle() {
      muted = !muted;
      try { localStorage.setItem('ando-game:muted', muted ? '1' : '0'); } catch (e) { /* noop */ }
      return muted;
    },
    get muted() { return muted; },
  };
})();
