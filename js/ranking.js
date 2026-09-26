// ランキング（そのPCのブラウザ内に保存）
const Ranking = (() => {
  const KEY = 'ando-game:ranking:v1';
  const PLAYS = 'ando-game:plays:v1';
  const MAX = 10;

  let mem = {};
  try { mem = JSON.parse(localStorage.getItem(KEY)) || {}; } catch (e) { mem = {}; }
  let plays = 0;
  try { plays = parseInt(localStorage.getItem(PLAYS), 10) || 0; } catch (e) { plays = 0; }

  function save() {
    try { localStorage.setItem(KEY, JSON.stringify(mem)); } catch (e) { /* 保存できなくても遊べる */ }
  }

  return {
    MAX,
    list: (mode) => (mem[mode] || []).slice(),
    best: (mode) => ((mem[mode] || [])[0] || {}).score || 0,
    // 入るなら順位（1始まり）、入らないなら 0
    rankOf(mode, score) {
      if (score <= 0) return 0;
      const l = mem[mode] || [];
      let i = l.findIndex((e) => score > e.score);
      if (i === -1) i = l.length;
      return i < MAX ? i + 1 : 0;
    },
    add(mode, name, score, grade) {
      const r = this.rankOf(mode, score);
      if (!r) return 0;
      const l = mem[mode] || (mem[mode] = []);
      l.splice(r - 1, 0, { name, score, grade, at: Date.now() });
      if (l.length > MAX) l.length = MAX;
      save();
      return r;
    },
    reset() {
      mem = {};
      plays = 0;
      save();
      try { localStorage.setItem(PLAYS, '0'); } catch (e) { /* noop */ }
    },
    get plays() { return plays; },
    countPlay() {
      plays++;
      try { localStorage.setItem(PLAYS, String(plays)); } catch (e) { /* noop */ }
    },
  };
})();
