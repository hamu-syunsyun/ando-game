// 共通の小物
const U = {
  $: (s, root = document) => root.querySelector(s),
  $$: (s, root = document) => Array.from(root.querySelectorAll(s)),
  rand: (n) => Math.floor(Math.random() * n),
  pick: (a) => a[Math.floor(Math.random() * a.length)],
  shuffle(a) {
    a = a.slice();
    for (let i = a.length - 1; i > 0; i--) {
      const j = Math.floor(Math.random() * (i + 1));
      [a[i], a[j]] = [a[j], a[i]];
    }
    return a;
  },
  // 浮動小数の誤差を消して有効数字3桁で表示する（0.30000000000000004 → "0.3"）
  num: (x) => String(parseFloat((+x).toPrecision(3))),
  ohm(v) {
    if (v >= 1e6) return U.num(v / 1e6) + ' MΩ';
    if (v >= 1e3) return U.num(v / 1e3) + ' kΩ';
    return U.num(v) + ' Ω';
  },
  esc: (s) => String(s).replace(/[&<>"']/g, (c) => ({ '&': '&amp;', '<': '&lt;', '>': '&gt;', '"': '&quot;', "'": '&#39;' }[c])),
};

// 安東先生（架空）の顔
const ANDO_SVG = `
<svg viewBox="0 0 120 120" role="img" aria-label="安東先生">
  <circle cx="60" cy="64" r="42" fill="#f1d2b0"/>
  <path d="M20 58c0-26 18-40 40-40s40 14 40 40c-6-10-14-16-22-16-10 0-14 6-18 6s-8-6-18-6c-8 0-16 6-22 16z" fill="#4a4a4a"/>
  <path d="M18 60q-2 8 4 14M102 60q2 8-4 14" stroke="#4a4a4a" stroke-width="5" fill="none" stroke-linecap="round"/>
  <g fill="none" stroke="#222" stroke-width="3">
    <rect x="32" y="56" width="22" height="15" rx="3"/>
    <rect x="66" y="56" width="22" height="15" rx="3"/>
    <path d="M54 62h12"/>
  </g>
  <circle cx="43" cy="64" r="2.6" fill="#222"/>
  <circle cx="77" cy="64" r="2.6" fill="#222"/>
  <path d="M34 50l16 3M86 50l-16 3" stroke="#333" stroke-width="3.5" stroke-linecap="round"/>
  <path d="M48 90q12-5 24 0" stroke="#7a3b2e" stroke-width="3.5" fill="none" stroke-linecap="round"/>
</svg>`;
