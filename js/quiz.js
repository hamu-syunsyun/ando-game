// モード：小テスト（電気回路の4択）
const QuizGame = (() => {
  const { pick, shuffle, num } = U;

  // 正解と「ありがちな間違い」から4択を作る
  function numChoices(ans, unit, cands) {
    const a = num(ans);
    const seen = new Set([a]);
    const wrong = [];
    const extra = [ans * 2, ans / 2, ans * 10, ans / 10, ans * 3, ans + 1, ans * 1.5];
    for (const c of [...shuffle(cands), ...extra]) {
      if (!(c > 0) || !isFinite(c)) continue;
      const s = num(c);
      if (seen.has(s)) continue;
      seen.add(s);
      wrong.push(s);
      if (wrong.length === 3) break;
    }
    const all = shuffle([a, ...wrong]);
    return { choices: all.map((s) => `${s} ${unit}`), answer: all.indexOf(a) };
  }

  const PARALLEL = [
    [6, 3, 2], [4, 4, 2], [12, 6, 4], [10, 10, 5], [20, 30, 12], [60, 30, 20],
    [12, 4, 3], [100, 100, 50], [30, 15, 10], [20, 20, 10], [40, 10, 8], [6, 12, 4],
  ];

  const GEN = {
    ohmV() {
      const I = pick([0.1, 0.2, 0.5, 1, 2, 3, 4]);
      const R = pick([2, 5, 10, 20, 50, 100]);
      const V = I * R;
      return {
        q: `抵抗 <var>R</var> = ${R} Ω に<br>電流 <var>I</var> = ${num(I)} A が流れています。<br>抵抗にかかる電圧 <var>V</var> は？`,
        ...numChoices(V, 'V', [R / I, I + R, V * 2]),
        hint: `V = I × R = ${num(I)} × ${R} = ${num(V)} V`,
      };
    },
    ohmI() {
      const R = pick([2, 4, 5, 10, 20, 50]);
      const I = pick([0.5, 1, 2, 3, 5]);
      const V = I * R;
      return {
        q: `抵抗 <var>R</var> = ${R} Ω に<br>電圧 <var>V</var> = ${num(V)} V をかけました。<br>流れる電流 <var>I</var> は？`,
        ...numChoices(I, 'A', [R / V, V * R, V - R]),
        hint: `I = V ÷ R = ${num(V)} ÷ ${R} = ${num(I)} A`,
      };
    },
    ohmR() {
      const I = pick([0.1, 0.2, 0.5, 1, 2]);
      const R = pick([5, 10, 20, 50, 100, 200]);
      const V = I * R;
      return {
        q: `ある抵抗に電圧 ${num(V)} V をかけたら<br>電流 ${num(I)} A が流れました。<br>抵抗 <var>R</var> は？`,
        ...numChoices(R, 'Ω', [V * I, I / V, V + I]),
        hint: `R = V ÷ I = ${num(V)} ÷ ${num(I)} = ${num(R)} Ω`,
      };
    },
    series() {
      const n = pick([2, 2, 3]);
      const rs = Array.from({ length: n }, () => pick([10, 20, 30, 47, 50, 100, 220]));
      const s = rs.reduce((a, b) => a + b, 0);
      const par = 1 / rs.reduce((a, r) => a + 1 / r, 0);
      return {
        q: `${rs.map((r) => `${r} Ω`).join('、')} の抵抗を<br><b>直列</b>につなぎました。<br>合成抵抗は？`,
        ...numChoices(s, 'Ω', [par, s + 10, s - 10, Math.max(...rs)]),
        hint: `直列はそのまま足し算。${rs.join(' + ')} = ${s} Ω`,
      };
    },
    parallel() {
      const [a, b, r] = pick(PARALLEL);
      return {
        q: `${a} Ω と ${b} Ω の抵抗を<br><b>並列</b>につなぎました。<br>合成抵抗は？`,
        ...numChoices(r, 'Ω', [a + b, (a + b) / 2, a * b]),
        hint: `2本の並列は「積 ÷ 和」。(${a} × ${b}) ÷ (${a} + ${b}) = ${r} Ω`,
      };
    },
    powerVI() {
      const V = pick([1.5, 3, 5, 6, 9, 12, 100]);
      const I = pick([0.5, 1, 2, 3]);
      const P = V * I;
      return {
        q: `電圧 ${V} V で 電流 ${I} A が流れる機器の<br>消費電力 <var>P</var> は？`,
        ...numChoices(P, 'W', [V / I, V + I, P * 2]),
        hint: `P = V × I = ${V} × ${I} = ${num(P)} W`,
      };
    },
    powerIR() {
      const I = pick([1, 2, 3, 4]);
      const R = pick([2, 5, 10]);
      const P = I * I * R;
      return {
        q: `${R} Ω の抵抗に 電流 ${I} A が流れています。<br>抵抗の消費電力 <var>P</var> は？`,
        ...numChoices(P, 'W', [I * R, 2 * I * R, P / 2]),
        hint: `P = I² × R = ${I}² × ${R} = ${P} W`,
      };
    },
    divider() {
      for (;;) {
        const V = pick([5, 6, 9, 10, 12]);
        const [p, q] = pick([[1, 1], [1, 2], [2, 1], [1, 3], [3, 1], [1, 4], [2, 3], [3, 2]]);
        const k = pick([1, 2, 10]);
        const out = (V * q) / (p + q);
        if (out * 2 !== Math.round(out * 2)) continue;
        const R1 = p * k;
        const R2 = q * k;
        return {
          q: `${V} V の電源に R1 = ${R1} kΩ と R2 = ${R2} kΩ を<br>直列につなぎました。<br><b>R2</b> にかかる電圧は？`,
          ...numChoices(out, 'V', [V - out, V / 2, V]),
          hint: `分圧の式。${V} × ${R2} ÷ (${R1} + ${R2}) = ${num(out)} V`,
        };
      }
    },
  };

  // 知識問題：a[0] が正解
  const CONCEPTS = [
    { q: '電気抵抗の単位は？', a: ['Ω（オーム）', 'A（アンペア）', 'V（ボルト）', 'W（ワット）'], hint: '抵抗は Ω。オームの法則の「オーム」です。' },
    { q: '電流の単位は？', a: ['A（アンペア）', 'V（ボルト）', 'Ω（オーム）', 'F（ファラド）'], hint: '電流は A（アンペア）。' },
    { q: '電力の単位は？', a: ['W（ワット）', 'J（ジュール）', 'V（ボルト）', 'Hz（ヘルツ）'], hint: '電力は W。1秒あたりのエネルギーです。' },
    { q: 'コンデンサの静電容量の単位は？', a: ['F（ファラド）', 'H（ヘンリー）', 'Wb（ウェーバ）', 'S（ジーメンス）'], hint: '静電容量は F。μF や pF がよく出てきます。' },
    { q: 'コイルのインダクタンスの単位は？', a: ['H（ヘンリー）', 'F（ファラド）', 'T（テスラ）', 'Ω（オーム）'], hint: 'インダクタンスは H（ヘンリー）。' },
    { q: '電荷（電気の量）の単位は？', a: ['C（クーロン）', 'A（アンペア）', 'W（ワット）', 'Wh（ワット時）'], hint: '電荷は C。1 A が 1 秒流れると 1 C。' },
    { q: '抵抗を<b>直列</b>につないだとき、<br>どの抵抗でも同じになるのは？', a: ['電流', '電圧', '抵抗値', '消費電力'], hint: '直列は一本道なので、どこでも電流が同じ。' },
    { q: '抵抗を<b>並列</b>につないだとき、<br>どの抵抗でも同じになるのは？', a: ['電圧', '電流', '抵抗値', '消費電力'], hint: '並列は両端が同じ点につながるので、電圧が同じ。' },
    { q: '「分岐点に流れ込む電流の和 ＝ 流れ出る電流の和」<br>を何という？', a: ['キルヒホッフの電流則', 'キルヒホッフの電圧則', 'オームの法則', 'ファラデーの法則'], hint: 'キルヒホッフの第1法則（電流則）です。' },
    { q: '「回路を一周すると電圧の和は 0」<br>を何という？', a: ['キルヒホッフの電圧則', 'キルヒホッフの電流則', 'ジュールの法則', 'フレミングの法則'], hint: 'キルヒホッフの第2法則（電圧則）です。' },
    { q: 'LED の足のうち、<b>長いほう</b>は？', a: ['アノード（＋）', 'カソード（－）', 'ゲート', 'エミッタ'], hint: '長い足がアノード（＋）。逆につなぐと光りません。' },
    { q: 'LED に直列につないで、<br>電流を制限する部品は？', a: ['抵抗', 'コンデンサ', 'スイッチ', 'コイル'], hint: '電流制限抵抗。これがないと LED が壊れます。' },
    { q: '1 kΩ は何 Ω？', a: ['1000 Ω', '100 Ω', '10000 Ω', '1000000 Ω'], hint: 'k（キロ）は 1000 倍。' },
    { q: '1 mA は何 A？', a: ['0.001 A', '0.01 A', '0.1 A', '1000 A'], hint: 'm（ミリ）は 1/1000。' },
    { q: '1 μF は何 F？', a: ['0.000001 F', '0.001 F', '1000 F', '1000000 F'], hint: 'μ（マイクロ）は 100万分の1。' },
    { q: '抵抗の逆数を何という？', a: ['コンダクタンス', 'インピーダンス', 'リアクタンス', 'キャパシタンス'], hint: 'コンダクタンス。単位は S（ジーメンス）。' },
    { q: '交流回路で、抵抗・コイル・コンデンサを<br>まとめた「交流の抵抗」を何という？', a: ['インピーダンス', 'コンダクタンス', 'アドミタンス', 'インダクタンス'], hint: 'インピーダンス。記号は Z です。' },
    { q: 'ブレッドボードの<b>中央の溝</b>をはさんだ<br>左右の穴どうしは？', a: ['つながっていない', 'つながっている', '抵抗でつながっている', 'GND につながっている'], hint: '溝の左右は別々。IC をまたがせて挿すためです。' },
    { q: 'ブレッドボードの端にある<br>長い縦（横）の列はふつう何に使う？', a: ['電源と GND', '抵抗専用', 'LED専用', '何もつながっていない'], hint: '電源レール。赤線が＋、青線が－（GND）。' },
    { q: '乾電池 1 本の電圧はおよそ？', a: ['1.5 V', '3 V', '5 V', '9 V'], hint: '単3などの乾電池は 1.5 V。' },
    { q: '日本の家庭のコンセントの電圧は？', a: ['100 V', '200 V', '12 V', '5 V'], hint: '日本は 100 V（エアコン用などに 200 V もあります）。' },
    { q: 'USB から取れる電源の電圧は（標準）？', a: ['5 V', '3.3 V', '12 V', '100 V'], hint: 'USB の標準は 5 V。' },
    { q: '＋と－が抵抗なしで直接つながることを<br>何という？', a: ['ショート（短絡）', 'オープン（開放）', 'アース（接地）', 'ループ'], hint: 'ショート。大電流が流れて危険です。' },
    { q: '電流の向きは？', a: ['＋極から－極へ', '－極から＋極へ', '決まっていない', '電子と同じ向き'], hint: '電流は＋から－へ。電子の動きとは逆向きです。' },
  ];

  let lastType = null;
  let used = new Set();

  function concept() {
    if (used.size >= CONCEPTS.length) used = new Set();
    let i;
    do { i = U.rand(CONCEPTS.length); } while (used.has(i));
    used.add(i);
    const c = CONCEPTS[i];
    const order = shuffle([0, 1, 2, 3]);
    return { q: c.q, choices: order.map((k) => c.a[k]), answer: order.indexOf(0), hint: c.hint };
  }

  const TYPES = [...Object.keys(GEN), 'concept', 'concept', 'concept', 'concept'];

  function gen() {
    let t;
    do { t = pick(TYPES); } while (t === lastType && t !== 'concept');
    lastType = t;
    return t === 'concept' ? concept() : GEN[t]();
  }

  return makeChoiceGame({
    title: '小テスト',
    cls: 'cg-quiz',
    gen,
    render(el, cur) { el.innerHTML = `<p class="q-text">${cur.q}</p>`; },
  });
})();
