// モード：配線パズル（ブレッドボードのジャンパ線を回して、＋5V から LED までつなぐ）
const WiringGame = (() => {
  const N = 1, E = 2, S = 4, W = 8;
  // [向きのビット, 行の差, 列の差, 反対向き]
  const DIRS = [[N, -1, 0, S], [E, 0, 1, W], [S, 1, 0, N], [W, 0, -1, E]];

  // 時計回りに k 回まわしたときの接続
  function rot(m, k) {
    k = ((k % 4) + 4) % 4;
    for (let i = 0; i < k; i++) m = ((m << 1) | (m >> 3)) & 15;
    return m;
  }

  function sizeFor(boardIndex) {
    if (boardIndex < 2) return 4;
    if (boardIndex < 5) return 5;
    return 6;
  }

  // 一番上の行から一番下の行まで、正解の道を1本作る
  function makePath(n) {
    const minLen = n + Math.floor(n / 2) + 1;
    for (;;) {
      const sc = U.rand(n);
      const path = [[0, sc]];
      const seen = new Set([`0,${sc}`]);
      const dfs = (r, c) => {
        if (r === n - 1 && path.length >= minLen) return true;
        for (const [, dr, dc] of U.shuffle(DIRS)) {
          const nr = r + dr, nc = c + dc;
          if (nr < 0 || nr >= n || nc < 0 || nc >= n) continue;
          const k = `${nr},${nc}`;
          if (seen.has(k)) continue;
          seen.add(k); // 戻っても消さない（探索が爆発しないように）
          path.push([nr, nc]);
          if (dfs(nr, nc)) return true;
          path.pop();
        }
        return false;
      };
      if (dfs(0, sc)) return path;
    }
  }

  function makeBoard(n) {
    const path = makePath(n);
    const cells = Array.from({ length: n }, () => Array.from({ length: n }, () => null));
    path.forEach(([r, c], i) => {
      let m = 0;
      const link = ([pr, pc]) => {
        const d = DIRS.find(([, dr, dc]) => dr === pr - r && dc === pc - c);
        m |= d[0];
      };
      if (i > 0) link(path[i - 1]); else m |= N;
      if (i < path.length - 1) link(path[i + 1]); else m |= S;
      cells[r][c] = { solved: m, path: true };
    });

    // 道以外はダミーの線で埋める
    for (let r = 0; r < n; r++) {
      for (let c = 0; c < n; c++) {
        if (cells[r][c]) continue;
        const x = Math.random();
        const shape = x < 0.35 ? 5 : x < 0.75 ? 3 : x < 0.9 ? 7 : 0;
        cells[r][c] = { solved: rot(shape, U.rand(4)), path: false };
      }
    }

    // 飾り：道の途中のまっすぐな線を1本、抵抗にする
    const straights = path.slice(1, -1).filter(([r, c]) => [5, 10].includes(cells[r][c].solved));
    if (straights.length) {
      const [r, c] = U.pick(straights);
      cells[r][c].resistor = true;
    }

    const board = { n, cells, start: path[0][1], goal: path[path.length - 1][1], pathLen: path.length };
    // バラバラに回す（最初から解けていたらやり直し）
    do {
      for (const row of cells) for (const cell of row) { cell.shape = rot(cell.solved, U.rand(4)); cell.turns = 0; }
    } while (powerState(board).lit);
    return board;
  }

  const cur = (cell) => rot(cell.shape, cell.turns);

  // ＋5V から電気が届いているマスを調べる
  function powerState(b) {
    const on = new Set();
    if (!(cur(b.cells[0][b.start]) & N)) return { on, lit: false };
    const q = [[0, b.start]];
    on.add(`0,${b.start}`);
    while (q.length) {
      const [r, c] = q.shift();
      const m = cur(b.cells[r][c]);
      for (const [bit, dr, dc, opp] of DIRS) {
        if (!(m & bit)) continue;
        const nr = r + dr, nc = c + dc;
        if (nr < 0 || nr >= b.n || nc < 0 || nc >= b.n) continue;
        const k = `${nr},${nc}`;
        if (on.has(k) || !(cur(b.cells[nr][nc]) & opp)) continue;
        on.add(k);
        q.push([nr, nc]);
      }
    }
    const lit = on.has(`${b.n - 1},${b.goal}`) && !!(cur(b.cells[b.n - 1][b.goal]) & S);
    return { on, lit };
  }

  function tileSvg(cell) {
    const ends = { [N]: [50, 0], [E]: [100, 50], [S]: [50, 100], [W]: [0, 50] };
    let lines = '';
    for (const [bit] of DIRS) {
      if (cell.shape & bit) lines += `<line class="w" x1="50" y1="50" x2="${ends[bit][0]}" y2="${ends[bit][1]}"/>`;
    }
    const hub = cell.shape ? '<circle class="hub" cx="50" cy="50" r="8"/>' : '';
    let res = '';
    if (cell.resistor) {
      const vertical = !!(cell.shape & N);
      res = `<g ${vertical ? 'transform="rotate(90 50 50)"' : ''}>
        <rect x="28" y="38" width="44" height="24" rx="10" fill="#e9d3a4" stroke="#b89c68" stroke-width="2"/>
        <rect x="36" y="38" width="5" height="24" fill="#7b4a2a"/>
        <rect x="45" y="38" width="5" height="24" fill="#1b1b1b"/>
        <rect x="54" y="38" width="5" height="24" fill="#7b4a2a"/>
        <rect x="63" y="38" width="4" height="24" fill="#c9a227"/>
      </g>`;
    }
    return `<svg class="tile-svg" viewBox="0 0 100 100" style="transform:rotate(${cell.turns * 90}deg)">${lines}${cell.resistor ? '' : hub}${res}</svg>`;
  }

  const LED_SVG = `
    <svg class="led-svg" viewBox="0 0 60 60" aria-label="LED">
      <line x1="30" y1="0" x2="30" y2="60" class="led-leg"/>
      <path class="led-body" d="M16 40V24a14 14 0 0 1 28 0v16z"/>
      <rect x="12" y="38" width="36" height="6" rx="2" class="led-base"/>
    </svg>`;
  const PLUS_SVG = `
    <svg class="plus-svg" viewBox="0 0 60 60"><line x1="30" y1="0" x2="30" y2="60"/></svg>`;

  let el = null, ctx = null, alive = false, lock = false, board = null, boardNo = 0, timer = 0;

  function start(container, c) {
    el = container;
    ctx = c;
    alive = true;
    boardNo = 0;
    el.innerHTML = `
      <div class="wiring">
        <div class="wr-side">
          <p class="wr-no"></p>
          <p class="wr-tip"><b>クリック</b>で線を回す<br><b>右クリック</b>で逆に回す</p>
          <p class="wr-goal">＋5V から LED まで<br>電気をつなげ！</p>
          <button class="ghost-btn wr-pass" type="button">パス（−5秒）</button>
        </div>
        <div class="wr-board"></div>
      </div>`;
    U.$('.wr-pass', el).addEventListener('click', (e) => {
      if (!alive || lock) return;
      ctx.skip(5, e.currentTarget);
      newBoard();
    });
    newBoard();
  }

  function stop() {
    alive = false;
    clearTimeout(timer);
  }

  function newBoard() {
    if (!alive) return;
    board = makeBoard(sizeFor(boardNo));
    boardNo++;
    lock = false;
    render();
    update();
  }

  function render() {
    const { n, cells, start: sc, goal } = board;
    U.$('.wr-no', el).textContent = `${boardNo} 枚目（${n}×${n}）`;
    const ports = (col, inner) => Array.from({ length: n }, (_, c) => `<div class="port">${c === col ? inner : ''}</div>`).join('');
    let tiles = '';
    for (let r = 0; r < n; r++) {
      for (let c = 0; c < n; c++) {
        tiles += `<button class="tile" type="button" data-r="${r}" data-c="${c}" aria-label="${r + 1}行${c + 1}列">${tileSvg(cells[r][c])}</button>`;
      }
    }
    const wrap = U.$('.wr-board', el);
    wrap.style.setProperty('--n', n);
    wrap.innerHTML = `
      <div class="rail rail-plus"><span>＋5V</span></div>
      <div class="ports ports-top">${ports(sc, PLUS_SVG)}</div>
      <div class="grid">${tiles}</div>
      <div class="ports ports-bottom">${ports(goal, LED_SVG)}</div>
      <div class="rail rail-minus"><span>GND</span></div>`;
    const grid = U.$('.grid', wrap);
    grid.addEventListener('click', (e) => onTile(e, 1));
    grid.addEventListener('contextmenu', (e) => { e.preventDefault(); onTile(e, -1); });
  }

  function onTile(e, d) {
    const t = e.target.closest('.tile');
    if (!t || !alive || lock) return;
    const cell = board.cells[+t.dataset.r][+t.dataset.c];
    cell.turns += d;
    U.$('.tile-svg', t).style.transform = `rotate(${cell.turns * 90}deg)`;
    Sound.click();
    update();
  }

  function update() {
    const { on, lit } = powerState(board);
    U.$$('.tile', el).forEach((t) => t.classList.toggle('is-on', on.has(`${t.dataset.r},${t.dataset.c}`)));
    const led = U.$('.led-svg', el);
    led.classList.toggle('is-lit', lit);
    if (lit) {
      lock = true;
      U.$('.wr-board', el).classList.add('is-clear');
      ctx.hit(150 + 20 * board.pathLen, led, 'clear');
      timer = setTimeout(newBoard, 950);
    } else {
      U.$('.wr-board', el).classList.remove('is-clear');
    }
  }

  return { title: '配線パズル', start, stop };
})();
