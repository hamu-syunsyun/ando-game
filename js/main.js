// 画面の切り替え・タイマー・スコア・結果
(() => {
  const { $, $$ } = U;
  const GAMES = { quiz: QuizGame, color: ColorGame, wiring: WiringGame };

  // grades: [秀, 優, 良, 可] になる最低点。文化祭前に試遊して調整する
  const MODES = [
    {
      id: 'quiz', name: '小テスト', sub: '電気回路の早押し4択', time: '60秒',
      stages: [{ kind: 'quiz', time: 60 }],
      grades: [2400, 1700, 1100, 500],
      howto: [
        'オームの法則、合成抵抗、電力などの4択問題が出ます。',
        'クリック、またはキーボードの <kbd>1</kbd>〜<kbd>4</kbd> で答えます。',
        '連続で正解するとコンボでボーナス。まちがえると <b>残り時間 −3秒</b>。',
      ],
    },
    {
      id: 'color', name: 'カラーコード', sub: '抵抗の色を読め', time: '60秒',
      stages: [{ kind: 'color', time: 60 }],
      grades: [2800, 2000, 1300, 600],
      howto: [
        '抵抗についている色の帯を読んで、何 Ω かを答えます。',
        '左から「1桁目」「2桁目」「かける数（0の数）」です。色と数字の表は画面に出ています。',
        '例：黄・紫・赤 → 4, 7, ×100 → 4.7 kΩ',
        'まちがえると <b>残り時間 −3秒</b>。',
      ],
    },
    {
      id: 'wiring', name: '配線パズル', sub: 'ブレッドボードでLEDを光らせろ', time: '90秒',
      stages: [{ kind: 'wiring', time: 90 }],
      grades: [2600, 1900, 1200, 500],
      howto: [
        'ブレッドボードの線をクリックして回し、上の <b>＋5V</b> から下の <b>LED</b> まで電気をつなぎます。',
        '電気が届いている線は光ります。LED が光ったらクリア。',
        '右クリックで逆回転。どうしても無理なら「パス」（−5秒）。',
        '進むほど盤面が大きくなります。',
      ],
    },
    {
      id: 'final', name: '期末試験', sub: '3種目で総合判定', time: '約2分',
      stages: [{ kind: 'quiz', time: 40 }, { kind: 'color', time: 30 }, { kind: 'wiring', time: 60 }],
      grades: [4400, 3300, 2200, 1100],
      howto: [
        '<b>小テスト（40秒）→ カラーコード（30秒）→ 配線パズル（60秒）</b> を続けて受けます。',
        '合計点で成績がつきます。可以上で単位認定。',
        'コンボは種目をまたいで続きます。',
      ],
    },
  ];
  const MODE = Object.fromEntries(MODES.map((m) => [m.id, m]));
  const GRADES = ['秀', '優', '良', '可'];

  const COMMENTS = {
    秀: ['……文句なしです。来年、TA をやりませんか。', '正直、驚きました。満点に近いですね。', '私より速いかもしれません。'],
    優: ['よく勉強していますね。', 'この調子で後期もお願いします。', 'いいでしょう。優をつけておきます。'],
    良: ['まあ、良いでしょう。', '惜しいところがいくつかありましたね。', '基本はできています。あとは練習です。'],
    可: ['ギリギリです。オームの法則だけは忘れないように。', '今回は単位を出しますが……次はありませんよ。', '可、です。喜びすぎないように。'],
    不可: ['来年また会いましょう。', '再履修の教室でお待ちしています。', 'V = I × R から復習してください。', '出席だけで単位が出ると思わないように。'],
  };
  const TITLE_LINES = [
    '私の授業、甘くないですよ。',
    'オームの法則、言えますか？',
    '出席だけで単位が出ると思わないように。',
    'ブレッドボードの溝の左右は、つながっていませんよ。',
    '今年の不可は何人になるでしょうね。',
    'LED には抵抗を入れなさいと言ったはずです。',
  ];

  // ---------- 画面 ----------
  let screen = 'title';
  function show(id) {
    screen = id;
    $$('.screen').forEach((s) => s.classList.toggle('is-active', s.id === `screen-${id}`));
    if (id === 'title') startRankRotation(); else stopRankRotation();
  }

  $$('[data-ando]').forEach((d) => { d.innerHTML = ANDO_SVG; });

  function rankRows(modeId, highlight = 0) {
    const list = Ranking.list(modeId);
    let html = '';
    for (let i = 0; i < Ranking.MAX; i++) {
      const e = list[i];
      const cls = i + 1 === highlight ? ' class="is-new"' : '';
      html += e
        ? `<li${cls}><span class="r-no">${i + 1}</span><span class="r-name">${U.esc(e.name)}</span><span class="r-grade">${e.grade}</span><span class="r-score">${e.score}</span></li>`
        : `<li class="is-empty"><span class="r-no">${i + 1}</span><span class="r-name">―</span><span class="r-grade"></span><span class="r-score"></span></li>`;
    }
    return html;
  }

  // ---------- タイトル ----------
  let tabMode = 'final';
  let rankTimer = 0;

  function renderTitle() {
    $('#mode-list').innerHTML = MODES.map((m) => `
      <button class="mode-card mode-${m.id}" type="button" data-mode="${m.id}">
        <span class="mode-name">${m.name}</span>
        <span class="mode-sub">${m.sub}</span>
        <span class="mode-meta"><span>${m.time}</span><span>最高 ${Ranking.best(m.id)} 点</span></span>
      </button>`).join('');
    $('#title-bubble').textContent = U.pick(TITLE_LINES);
    $('#play-count').textContent = `これまでの受講者：${Ranking.plays} 人`;
    renderTabs();
  }

  function renderTabs() {
    $('#rank-tabs').innerHTML = MODES.map((m) =>
      `<button type="button" class="rank-tab${m.id === tabMode ? ' is-active' : ''}" data-tab="${m.id}">${m.name}</button>`).join('');
    $('#rank-list').innerHTML = rankRows(tabMode);
  }

  function startRankRotation() {
    stopRankRotation();
    rankTimer = setInterval(() => {
      const i = MODES.findIndex((m) => m.id === tabMode);
      tabMode = MODES[(i + 1) % MODES.length].id;
      renderTabs();
    }, 6000);
  }
  function stopRankRotation() { clearInterval(rankTimer); }

  $('#mode-list').addEventListener('click', (e) => {
    const b = e.target.closest('[data-mode]');
    if (!b) return;
    Sound.unlock();
    Sound.click();
    openHowto(MODE[b.dataset.mode]);
  });
  $('#rank-tabs').addEventListener('click', (e) => {
    const b = e.target.closest('[data-tab]');
    if (!b) return;
    tabMode = b.dataset.tab;
    renderTabs();
    startRankRotation();
  });
  $('#btn-fullscreen').addEventListener('click', () => {
    if (document.fullscreenElement) document.exitFullscreen();
    else document.documentElement.requestFullscreen?.();
  });

  // ---------- あそびかた ----------
  let selected = null;
  function openHowto(mode) {
    selected = mode;
    $('#howto-kicker').textContent = `${mode.sub}（${mode.time}）`;
    $('#howto-title').textContent = mode.name;
    $('#howto-list').innerHTML = mode.howto.map((h) => `<li>${h}</li>`).join('');
    show('howto');
    $('#btn-start').focus();
  }
  $('#btn-start').addEventListener('click', () => startMode(selected));
  $('#btn-howto-back').addEventListener('click', goTitle);

  // ---------- ゲーム ----------
  let run = null;     // 今のプレイの状態
  let rafId = 0;
  let overlayToken = 0;

  function startMode(mode) {
    Sound.unlock();
    cancelRun();
    run = {
      mode, stageIdx: -1, score: 0, combo: 0, maxCombo: 0,
      stats: {}, kind: null, timeLeft: 0, stageTime: 1, game: null, running: false, lastT: 0, lastSec: 0,
    };
    show('game');
    $('#stage').innerHTML = '';
    updateHud();
    nextStage();
  }

  function nextStage() {
    const r = run;
    r.stageIdx++;
    if (r.stageIdx >= r.mode.stages.length) { finish(); return; }
    const st = r.mode.stages[r.stageIdx];
    const G = GAMES[st.kind];
    const multi = r.mode.stages.length > 1;
    r.kind = st.kind;
    r.stats[st.kind] = r.stats[st.kind] || { hit: 0, miss: 0, pass: 0 };
    r.timeLeft = r.stageTime = st.time;
    $('#hud-stage').textContent = multi ? `第${r.stageIdx + 1}問　${G.title}` : G.title;
    $('#stage').innerHTML = '';
    updateHud();

    const seq = [];
    if (multi) seq.push(`第${r.stageIdx + 1}問<small>${G.title}</small>`);
    if (r.stageIdx === 0) seq.push('3', '2', '1');
    seq.push('はじめ！');
    playOverlay(seq, () => {
      if (run !== r) return;
      r.game = G;
      G.start($('#stage'), makeCtx(r));
      r.running = true;
      r.lastT = performance.now();
      r.lastSec = Math.ceil(r.timeLeft);
      rafId = requestAnimationFrame(loop);
    });
  }

  function loop(t) {
    const r = run;
    if (!r || !r.running) return;
    r.timeLeft -= Math.min(0.5, (t - r.lastT) / 1000);
    r.lastT = t;
    const sec = Math.ceil(r.timeLeft);
    if (sec !== r.lastSec) {
      r.lastSec = sec;
      if (sec <= 5 && sec > 0) Sound.tick();
    }
    if (r.timeLeft <= 0) {
      r.timeLeft = 0;
      updateHud();
      endStage();
      return;
    }
    updateHud();
    rafId = requestAnimationFrame(loop);
  }

  function endStage() {
    const r = run;
    r.running = false;
    r.game.stop();
    r.game = null;
    Sound.end();
    playOverlay(['そこまで！'], () => { if (run === r) nextStage(); }, 1200);
  }

  function makeCtx(r) {
    return {
      hit(base, anchor, sound = 'ok') {
        if (run !== r || !r.running) return;
        r.combo++;
        r.maxCombo = Math.max(r.maxCombo, r.combo);
        const pts = base + Math.min(r.combo - 1, 10) * 10;
        r.score += pts;
        r.stats[r.kind].hit++;
        Sound[sound]();
        popup(`+${pts}`, anchor, 'is-plus');
        updateHud();
      },
      miss(anchor) {
        if (run !== r || !r.running) return;
        r.combo = 0;
        r.stats[r.kind].miss++;
        r.timeLeft = Math.max(0, r.timeLeft - 3);
        Sound.ng();
        popup('−3秒', anchor, 'is-minus');
        flash();
        updateHud();
      },
      skip(sec, anchor) {
        if (run !== r || !r.running) return;
        r.combo = 0;
        r.stats[r.kind].pass++;
        r.timeLeft = Math.max(0, r.timeLeft - sec);
        Sound.ng();
        popup(`−${sec}秒`, anchor, 'is-minus');
        updateHud();
      },
    };
  }

  function updateHud() {
    const r = run;
    if (!r) return;
    const t = Math.max(0, r.timeLeft);
    $('#hud-time').textContent = Math.ceil(t);
    const fill = $('#time-fill');
    fill.style.width = `${(t / r.stageTime) * 100}%`;
    fill.classList.toggle('is-low', t <= 10);
    $('#hud-score').textContent = r.score;
    $('#hud-combo').innerHTML = r.combo >= 2 ? `<b>${r.combo}</b> コンボ` : '';
  }

  function playOverlay(items, done, holdLast = 700) {
    const token = ++overlayToken;
    const ov = $('#overlay');
    const txt = $('#overlay-text');
    ov.classList.add('is-show');
    let i = 0;
    const step = () => {
      if (token !== overlayToken) return;
      if (i >= items.length) {
        ov.classList.remove('is-show');
        done();
        return;
      }
      const item = items[i++];
      txt.innerHTML = item;
      txt.classList.remove('pop-in');
      void txt.offsetWidth;
      txt.classList.add('pop-in');
      if (/^\d$/.test(item)) Sound.count();
      else if (item === 'はじめ！') Sound.go();
      setTimeout(step, i === items.length ? holdLast : item.startsWith('第') ? 1300 : 700);
    };
    step();
  }

  function popup(text, anchor, cls) {
    const d = document.createElement('div');
    d.className = `pop ${cls}`;
    d.textContent = text;
    let x = innerWidth / 2, y = innerHeight / 2;
    if (anchor && anchor.getBoundingClientRect) {
      const b = anchor.getBoundingClientRect();
      x = b.left + b.width / 2;
      y = b.top + b.height / 2;
    }
    d.style.left = `${x}px`;
    d.style.top = `${y}px`;
    $('#fx').appendChild(d);
    setTimeout(() => d.remove(), 1000);
  }

  function flash() {
    const s = $('#screen-game');
    s.classList.remove('is-flash');
    void s.offsetWidth;
    s.classList.add('is-flash');
  }

  function cancelRun() {
    overlayToken++;
    $('#overlay').classList.remove('is-show');
    cancelAnimationFrame(rafId);
    if (run) {
      run.running = false;
      if (run.game) run.game.stop();
    }
    run = null;
  }

  $('#btn-quit').addEventListener('click', goTitle);

  // ---------- 結果 ----------
  let result = null;
  let idleTimer = 0;
  const IDLE_MS = 60000;

  function gradeOf(mode, score) {
    const i = mode.grades.findIndex((g) => score >= g);
    return i === -1 ? '不可' : GRADES[i];
  }

  const STAT_TEXT = {
    quiz: (s) => `小テスト：${s.hit}問正解／ミス ${s.miss}`,
    color: (s) => `カラーコード：${s.hit}本正解／ミス ${s.miss}`,
    wiring: (s) => `配線パズル：${s.hit}枚クリア／パス ${s.pass}`,
  };

  function finish() {
    const r = run;
    run = null;
    const grade = gradeOf(r.mode, r.score);
    const passed = grade !== '不可';
    Ranking.countPlay();
    result = { mode: r.mode, score: r.score, grade, registered: false };

    $('#result-mode').textContent = r.mode.name;
    const stamp = $('#result-stamp');
    stamp.textContent = grade;
    stamp.className = `stamp ${passed ? 'is-pass' : 'is-fail'}`;
    $('#result-verdict').textContent = passed ? '単位認定' : '再履修';
    $('#result-score').textContent = r.score;
    $('#result-stats').innerHTML = [
      ...Object.entries(r.stats).map(([k, s]) => `<li>${STAT_TEXT[k](s)}</li>`),
      `<li>最大コンボ：${r.maxCombo}</li>`,
    ].join('');
    $('#result-bubble').textContent = U.pick(COMMENTS[grade]);

    const rank = Ranking.rankOf(r.mode.id, r.score);
    const form = $('#name-form');
    form.hidden = !rank;
    $('#result-rank').innerHTML = rankRows(r.mode.id);
    show('result');
    passed ? Sound.pass() : Sound.fail();
    if (rank) {
      $('#name-rank').innerHTML = `<b>${rank}位</b> にランクイン！`;
      $('#name-input').value = '';
      setTimeout(() => $('#name-input').focus(), 50);
    } else {
      $('#btn-retry').focus();
    }
    resetIdle();
  }

  function register() {
    if (!result || result.registered) return;
    const name = $('#name-input').value.trim() || 'ななし';
    const r = Ranking.add(result.mode.id, name, result.score, result.grade);
    result.registered = true;
    $('#name-form').hidden = true;
    $('#result-rank').innerHTML = rankRows(result.mode.id, r);
    Sound.clear();
    $('#btn-retry').focus();
  }

  $('#name-form').addEventListener('submit', (e) => { e.preventDefault(); register(); });
  $('#btn-retry').addEventListener('click', () => {
    if (!$('#name-form').hidden) register();
    startMode(result.mode);
  });
  $('#btn-to-title').addEventListener('click', () => {
    if (!$('#name-form').hidden) register();
    goTitle();
  });

  // 放置されたら名前を「ななし」で登録してタイトルへ戻る
  function resetIdle() {
    clearTimeout(idleTimer);
    if (screen !== 'result' && screen !== 'howto') return;
    idleTimer = setTimeout(() => {
      if (screen === 'result' && !$('#name-form').hidden) register();
      goTitle();
    }, IDLE_MS);
  }
  ['pointerdown', 'keydown'].forEach((ev) => window.addEventListener(ev, resetIdle, true));

  function goTitle() {
    cancelRun();
    clearTimeout(idleTimer);
    renderTitle();
    show('title');
  }

  // ---------- キー操作 ----------
  let typed = '';
  window.addEventListener('keydown', (e) => {
    if (e.target.tagName === 'INPUT') return;
    if (e.key === 'm' || e.key === 'M') {
      $('#mute-badge').hidden = !Sound.toggle();
    }
    if (screen === 'howto' && e.key === 'Escape') goTitle();
    // 係の人用：タイトル画面で resetrank と打つとランキングを消せる
    if (screen === 'title' && e.key.length === 1) {
      typed = (typed + e.key.toLowerCase()).slice(-9);
      if (typed === 'resetrank') {
        typed = '';
        if (confirm('ランキングと受講者数をすべて消します。よろしいですか？')) {
          Ranking.reset();
          renderTitle();
        }
      }
    }
  });

  // ゲーム中の右クリックメニューや文字選択を防ぐ
  $('#screen-game').addEventListener('contextmenu', (e) => e.preventDefault());

  $('#mute-badge').hidden = !Sound.muted;
  renderTitle();
  show('title');
})();
