// 4択ゲームの共通部分（小テスト・カラーコードで使う）
// gen() は { choices: [4つ], answer: 正解の番号, hint: 間違えたときの解説 } を返す
function makeChoiceGame({ title, cls, gen, render, base = 100 }) {
  let el = null;
  let ctx = null;
  let alive = false;
  let lock = false;
  let cur = null;
  let n = 0;
  let timer = 0;

  function onKey(e) {
    const i = '1234'.indexOf(e.key);
    if (i >= 0) {
      e.preventDefault();
      answer(i);
    }
  }

  function start(container, c) {
    el = container;
    ctx = c;
    alive = true;
    n = 0;
    el.innerHTML = `
      <div class="cg ${cls}">
        <div class="cg-no"></div>
        <div class="cg-question"></div>
        <div class="cg-choices">
          ${[0, 1, 2, 3].map((i) => `<button class="cg-choice" type="button" data-i="${i}"></button>`).join('')}
        </div>
        <div class="cg-feedback" aria-live="polite"></div>
      </div>`;
    U.$$('.cg-choice', el).forEach((b) => b.addEventListener('click', () => answer(+b.dataset.i)));
    window.addEventListener('keydown', onKey);
    next();
  }

  function stop() {
    alive = false;
    clearTimeout(timer);
    window.removeEventListener('keydown', onKey);
  }

  function next() {
    if (!alive) return;
    cur = gen();
    n++;
    lock = false;
    U.$('.cg-no', el).textContent = `第${n}問`;
    render(U.$('.cg-question', el), cur);
    U.$$('.cg-choice', el).forEach((b, i) => {
      b.className = 'cg-choice';
      b.innerHTML = `<span class="cg-key">${i + 1}</span><span class="cg-txt">${cur.choices[i]}</span>`;
    });
    U.$('.cg-feedback', el).innerHTML = '';
  }

  function answer(i) {
    if (!alive || lock || !cur) return;
    lock = true;
    const btns = U.$$('.cg-choice', el);
    btns[cur.answer].classList.add('is-correct');
    if (i === cur.answer) {
      ctx.hit(base, btns[i]);
      timer = setTimeout(next, 380);
    } else {
      btns[i].classList.add('is-wrong');
      U.$('.cg-feedback', el).innerHTML = cur.hint ? `<b>解説</b>${cur.hint}` : '';
      ctx.miss(btns[i]);
      timer = setTimeout(next, 1500);
    }
  }

  return { title, start, stop };
}
