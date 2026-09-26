// モード：カラーコード（抵抗の色の帯から値を読む）
const ColorGame = (() => {
  const COLORS = [
    { name: '黒', hex: '#1b1b1b' },
    { name: '茶', hex: '#7b4a2a' },
    { name: '赤', hex: '#d32f2f' },
    { name: '橙', hex: '#f57c00' },
    { name: '黄', hex: '#fbc02d' },
    { name: '緑', hex: '#388e3c' },
    { name: '青', hex: '#1976d2' },
    { name: '紫', hex: '#7b1fa2' },
    { name: '灰', hex: '#8a8a8a' },
    { name: '白', hex: '#f5f5f5' },
  ];
  const MULT = ['×1', '×10', '×100', '×1k', '×10k', '×100k'];

  function gen() {
    const d1 = 1 + U.rand(9);
    const d2 = U.rand(10);
    const m = U.rand(6);
    const base = d1 * 10 + d2;
    const v = base * 10 ** m;

    // 間違えやすい読み方を不正解にする
    const cands = [v * 10, v / 10, base * 10 ** (m + 2)];
    if (d2 !== 0 && d2 !== d1) cands.push((d2 * 10 + d1) * 10 ** m);
    cands.push((base + (d2 < 9 ? 1 : -1)) * 10 ** m);
    cands.push((base + (d1 < 9 ? 10 : -10)) * 10 ** m);

    const a = U.ohm(v);
    const seen = new Set([a]);
    const wrong = [];
    for (const c of U.shuffle(cands)) {
      const s = U.ohm(c);
      if (seen.has(s)) continue;
      seen.add(s);
      wrong.push(s);
      if (wrong.length === 3) break;
    }
    const all = U.shuffle([a, ...wrong]);
    const names = [d1, d2, m].map((i) => COLORS[i].name).join('・');
    return {
      bands: [d1, d2, m],
      choices: all,
      answer: all.indexOf(a),
      hint: `${names} → ${d1}${d2} ${MULT[m]} ＝ ${a}`,
    };
  }

  function resistorSvg(bands) {
    const xs = [150, 190, 230];
    const bandRects = bands
      .map((b, i) => `<rect x="${xs[i]}" y="36" width="20" height="88" fill="${COLORS[b].hex}"/>`)
      .join('');
    return `
      <svg class="resistor" viewBox="0 0 440 160" role="img" aria-label="抵抗">
        <line x1="0" y1="80" x2="440" y2="80" stroke="#9aa3ab" stroke-width="8"/>
        <path d="M110 40q0-14 16-14h40q10 0 16 8h76q6-8 16-8h40q16 0 16 14v80q0 14-16 14h-40q-10 0-16-8h-76q-6 8-16 8h-40q-16 0-16-14z"
              fill="#e9d3a4" stroke="#b89c68" stroke-width="3"/>
        <g clip-path="url(#rbody)">${bandRects}
          <rect x="288" y="36" width="16" height="88" fill="#c9a227"/>
        </g>
        <clipPath id="rbody"><path d="M110 40q0-14 16-14h40q10 0 16 8h76q6-8 16-8h40q16 0 16 14v80q0 14-16 14h-40q-10 0-16-8h-76q-6 8-16 8h-40q-16 0-16-14z"/></clipPath>
      </svg>`;
  }

  function legend() {
    return `<ol class="cc-legend" start="0">${COLORS.map((c, i) => `
      <li><span class="cc-chip" style="background:${c.hex}"></span><span class="cc-n">${i}</span><span class="cc-name">${c.name}</span></li>`).join('')}
    </ol>`;
  }

  return makeChoiceGame({
    title: 'カラーコード',
    cls: 'cg-color',
    gen,
    render(el, cur) {
      el.innerHTML = `
        <p class="cc-lead">この抵抗は何 Ω？<small>（左から 1桁目・2桁目・かける数・金＝誤差）</small></p>
        ${resistorSvg(cur.bands)}
        ${legend()}`;
    },
  });
})();
