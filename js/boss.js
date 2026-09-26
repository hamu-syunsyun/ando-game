// モード：3Dボス戦「安東を倒せ」（three.js を使用。vendor/three.min.js を同梱）
const BossGame = (() => {
  const ARENA_R = 20;
  const BOSS_MAX = 3000;
  const PLAYER_MAX = 1000;
  const BOSS_R = 1.7;
  const ELECTRO = 0xb57cff;

  let T = null;
  let renderer = null, scene = null, camera = null;
  let grad = null, outlineMat = null;
  let player = null, boss = null;
  const deco = { clouds: null, leds: [], islands: [] };
  const G = {};          // 使い回すジオメトリ
  const matCache = {};

  let el = null, ctx = null, alive = false, raf = 0, last = 0, ro = null;
  let S = null;          // 1回のプレイの状態
  const keys = new Set();
  const ui = {};

  const rnd = (a, b) => a + Math.random() * (b - a);
  const clamp = (x, a, b) => Math.max(a, Math.min(b, x));
  function turnTo(a, target, k) {
    let d = target - a;
    while (d > Math.PI) d -= Math.PI * 2;
    while (d < -Math.PI) d += Math.PI * 2;
    return a + d * Math.min(1, k);
  }
  const angDiff = (a, b) => Math.abs(Math.atan2(Math.sin(a - b), Math.cos(a - b)));
  const flatDist = (a, b) => Math.hypot(a.x - b.x, a.z - b.z);

  // ---------- 素材 ----------
  function toon(color, extra = {}) { return new T.MeshToonMaterial({ color, gradientMap: grad, ...extra }); }
  function basic(color) {
    if (!matCache[color]) matCache[color] = new T.MeshBasicMaterial({ color });
    return matCache[color];
  }
  function mesh(geo, m, { cast = true, outline = 0 } = {}) {
    const o = new T.Mesh(geo, m);
    o.castShadow = cast;
    if (outline) {
      const h = new T.Mesh(geo, outlineMat);
      h.scale.setScalar(1 + outline);
      o.add(h);
    }
    return o;
  }
  function own(m) { m.userData.own = true; return m; }

  function canvasTex(w, h, draw) {
    const c = document.createElement('canvas');
    c.width = w;
    c.height = h;
    draw(c.getContext('2d'), w, h);
    return new T.CanvasTexture(c);
  }

  // ---------- 初期化（最初の1回だけ） ----------
  function init() {
    if (renderer) return true;
    T = window.THREE;
    if (!T) return false;
    try {
      renderer = new T.WebGLRenderer({ antialias: true });
    } catch (e) {
      return false;
    }
    renderer.setPixelRatio(Math.min(window.devicePixelRatio || 1, 1.5));
    renderer.shadowMap.enabled = true;
    renderer.shadowMap.type = T.PCFSoftShadowMap;

    const g = new Uint8Array([90, 90, 90, 255, 175, 175, 175, 255, 255, 255, 255, 255]);
    grad = new T.DataTexture(g, 3, 1, T.RGBAFormat);
    grad.minFilter = grad.magFilter = T.NearestFilter;
    grad.needsUpdate = true;
    outlineMat = new T.MeshBasicMaterial({ color: 0x1d1a2b, side: T.BackSide });

    scene = new T.Scene();
    scene.background = canvasTex(2, 512, (c, w, h) => {
      const gr = c.createLinearGradient(0, 0, 0, h);
      gr.addColorStop(0, '#4d9df0');
      gr.addColorStop(0.55, '#a9d6ff');
      gr.addColorStop(1, '#e4f2ff');
      c.fillStyle = gr;
      c.fillRect(0, 0, w, h);
    });
    scene.fog = new T.Fog(0xd8ecff, 70, 200);
    camera = new T.PerspectiveCamera(58, 16 / 9, 0.1, 500);

    scene.add(new T.HemisphereLight(0xffffff, 0x7fa36a, 0.75));
    const sun = new T.DirectionalLight(0xfff1d6, 0.85);
    sun.position.set(18, 36, 14);
    sun.castShadow = true;
    sun.shadow.mapSize.set(1024, 1024);
    Object.assign(sun.shadow.camera, { left: -28, right: 28, top: 28, bottom: -28, near: 1, far: 90 });
    scene.add(sun);

    G.box = new T.BoxGeometry(1, 1, 1);
    G.spark = new T.BoxGeometry(0.14, 0.14, 0.14);
    G.circle = new T.CircleGeometry(1, 40);
    G.ring = new T.RingGeometry(0.93, 1, 48);
    G.wall = new T.CylinderGeometry(1, 1, 0.8, 64, 1, true);
    G.plane = new T.PlaneGeometry(1, 1);
    G.resBody = new T.CylinderGeometry(0.22, 0.22, 0.8, 12);
    G.resBand = new T.CylinderGeometry(0.235, 0.235, 0.09, 12);
    G.lead = new T.CylinderGeometry(0.04, 0.04, 1.6, 6);

    buildWorld();
    player = buildPlayer();
    boss = buildBoss();
    scene.add(player.g, boss.g);
    return true;
  }

  function buildWorld() {
    const ground = new T.Mesh(new T.CircleGeometry(260, 64), toon(0x8fcf72));
    ground.rotation.x = -Math.PI / 2;
    ground.position.y = -0.6;
    ground.receiveShadow = true;
    scene.add(ground);

    const base = new T.Mesh(new T.CylinderGeometry(ARENA_R + 1.2, ARENA_R + 2.4, 1.2, 64), toon(0xbfae8c));
    base.position.y = -0.6;
    base.receiveShadow = true;
    scene.add(base);

    const floorTex = canvasTex(1024, 1024, (c, w) => {
      const m = w / 2;
      c.fillStyle = '#eee4cf';
      c.fillRect(0, 0, w, w);
      // 基板の配線っぽい模様
      c.strokeStyle = '#d9c9a4';
      c.lineWidth = 10;
      c.lineCap = 'round';
      for (let i = 0; i < 26; i++) {
        const a = (i / 26) * Math.PI * 2;
        let r = 150 + (i % 3) * 40;
        let x = m + Math.cos(a) * r, y = m + Math.sin(a) * r;
        c.beginPath();
        c.moveTo(x, y);
        r += 120 + (i % 4) * 30;
        const x2 = m + Math.cos(a) * r, y2 = m + Math.sin(a) * r;
        if (i % 2) c.lineTo(x2, y); else c.lineTo(x, y2);
        c.lineTo(x2, y2);
        c.stroke();
        c.fillStyle = '#cdb88c';
        c.beginPath();
        c.arc(x2, y2, 14, 0, Math.PI * 2);
        c.fill();
      }
      c.strokeStyle = '#d4c197';
      [140, 470, 500].forEach((r, i) => {
        c.lineWidth = i ? 8 : 12;
        c.beginPath();
        c.arc(m, m, r, 0, Math.PI * 2);
        c.stroke();
      });
      c.fillStyle = 'rgba(180, 155, 105, .45)';
      c.font = 'bold 230px serif';
      c.textAlign = 'center';
      c.textBaseline = 'middle';
      c.fillText('Ω', m, m + 10);
    });
    const floor = new T.Mesh(new T.CircleGeometry(ARENA_R + 1.2, 64), toon(0xd9cfb9, { map: floorTex }));
    floor.rotation.x = -Math.PI / 2;
    floor.position.y = 0.01;
    floor.receiveShadow = true;
    scene.add(floor);

    // アリーナを囲む巨大な部品
    for (let i = 0; i < 9; i++) {
      const a = (i / 9) * Math.PI * 2 + 0.2;
      const p = [resistorPillar, capacitorPillar, ledPillar][i % 3]();
      p.position.set(Math.cos(a) * (ARENA_R + 5), 0, Math.sin(a) * (ARENA_R + 5));
      p.rotation.y = rnd(0, Math.PI * 2);
      scene.add(p);
    }

    // 木
    const trunkG = new T.CylinderGeometry(0.3, 0.45, 2.4, 6);
    const leafG = new T.IcosahedronGeometry(1.8, 0);
    const trunkM = toon(0x8a5a3b);
    const leafMs = [toon(0x5cae4f), toon(0x6fbf5a), toon(0x4f9d4a)];
    for (let i = 0; i < 46; i++) {
      const a = rnd(0, Math.PI * 2), r = rnd(34, 95);
      const t = new T.Group();
      const tr = new T.Mesh(trunkG, trunkM);
      tr.position.y = 1.2;
      const lf = new T.Mesh(leafG, U.pick(leafMs));
      lf.position.y = 3.2;
      lf.scale.set(1, rnd(1.1, 1.6), 1);
      t.add(tr, lf);
      t.position.set(Math.cos(a) * r, -0.6, Math.sin(a) * r);
      t.scale.setScalar(rnd(0.9, 1.8));
      scene.add(t);
    }

    // 浮き島
    for (let i = 0; i < 5; i++) {
      const a = (i / 5) * Math.PI * 2 + rnd(0, 0.6), r = rnd(95, 130);
      const is = new T.Group();
      const rock = new T.Mesh(new T.ConeGeometry(9, 16, 7), toon(0x9b7b5b));
      rock.rotation.x = Math.PI;
      rock.position.y = -8;
      const top = new T.Mesh(new T.CylinderGeometry(9.2, 9.2, 1.4, 7), toon(0x7cc36a));
      const tr = new T.Mesh(trunkG, trunkM);
      tr.position.y = 1.8;
      const lf = new T.Mesh(leafG, leafMs[0]);
      lf.position.set(0, 3.8, 0);
      is.add(rock, top, tr, lf);
      is.position.set(Math.cos(a) * r, rnd(22, 42), Math.sin(a) * r);
      is.userData.baseY = is.position.y;
      is.userData.phase = rnd(0, 6);
      deco.islands.push(is);
      scene.add(is);
    }

    // 雲
    deco.clouds = new T.Group();
    const cloudM = toon(0xffffff);
    const puff = new T.SphereGeometry(1, 10, 8);
    for (let i = 0; i < 14; i++) {
      const c = new T.Group();
      for (let k = 0; k < 5; k++) {
        const s = new T.Mesh(puff, cloudM);
        s.position.set(rnd(-4, 4), rnd(-0.6, 0.8), rnd(-1.5, 1.5));
        s.scale.setScalar(rnd(2, 3.6));
        c.add(s);
      }
      const a = rnd(0, Math.PI * 2), r = rnd(60, 150);
      c.position.set(Math.cos(a) * r, rnd(45, 70), Math.sin(a) * r);
      deco.clouds.add(c);
    }
    scene.add(deco.clouds);
  }

  const BAND = [0x1b1b1b, 0x7b4a2a, 0xd32f2f, 0xf57c00, 0xfbc02d, 0x388e3c, 0x1976d2, 0x7b1fa2];

  function resistorPillar() {
    const g = new T.Group();
    const lead = mesh(new T.CylinderGeometry(0.3, 0.3, 15, 8), toon(0xb8c0c8));
    lead.position.y = 7.5;
    const body = mesh(new T.CylinderGeometry(1.4, 1.4, 5.4, 20), toon(0xe9d3a4), { outline: 0.03 });
    body.position.y = 7.5;
    const capG = new T.SphereGeometry(1.6, 20, 12);
    [4.8, 10.2].forEach((y) => {
      const cap = mesh(capG, toon(0xe9d3a4));
      cap.position.y = y;
      cap.scale.y = 0.55;
      g.add(cap);
    });
    const bandG = new T.CylinderGeometry(1.45, 1.45, 0.55, 20);
    [5.9, 6.9, 7.9].forEach((y) => {
      const b = mesh(bandG, toon(U.pick(BAND)));
      b.position.y = y;
      g.add(b);
    });
    const gold = mesh(bandG, toon(0xc9a227));
    gold.position.y = 9.2;
    g.add(lead, body, gold);
    return g;
  }

  function capacitorPillar() {
    const g = new T.Group();
    const legG = new T.CylinderGeometry(0.2, 0.2, 3, 6);
    [-0.6, 0.6].forEach((x) => {
      const l = mesh(legG, toon(0xb8c0c8));
      l.position.set(x, 1.5, 0);
      g.add(l);
    });
    const body = mesh(new T.CylinderGeometry(1.7, 1.7, 6.5, 24), toon(0x2f5fb3), { outline: 0.03 });
    body.position.y = 6.2;
    const top = mesh(new T.CylinderGeometry(1.6, 1.6, 0.2, 24), toon(0xc9d1d9));
    top.position.y = 9.5;
    const stripe = mesh(new T.BoxGeometry(0.8, 6.3, 0.2), toon(0xe8eef5));
    stripe.position.set(0, 6.2, 1.62);
    g.add(body, top, stripe);
    return g;
  }

  function ledPillar() {
    const g = new T.Group();
    const legG = new T.CylinderGeometry(0.2, 0.2, 4, 6);
    [-0.6, 0.6].forEach((x) => {
      const l = mesh(legG, toon(0xb8c0c8));
      l.position.set(x, 2, 0);
      g.add(l);
    });
    const m = new T.MeshBasicMaterial({ color: 0xff5a50, transparent: true, opacity: 0.85 });
    const body = new T.Mesh(new T.CylinderGeometry(1.5, 1.5, 3, 20), m);
    body.position.y = 5.5;
    const dome = new T.Mesh(new T.SphereGeometry(1.5, 20, 12, 0, Math.PI * 2, 0, Math.PI / 2), m);
    dome.position.y = 7;
    const rim = mesh(new T.CylinderGeometry(1.75, 1.75, 0.4, 20), toon(0xd9453d));
    rim.position.y = 4.1;
    g.add(body, dome, rim);
    deco.leds.push(m);
    return g;
  }

  // ---------- 主人公 ----------
  function buildPlayer() {
    const g = new T.Group();
    const skin = toon(0xffe2c8), coat = toon(0x4a3fb5), trim = toon(0xf6d55c), dark = toon(0x2a2838), hairM = toon(0x3b2a4f);
    const o = 0.06;

    const body = mesh(new T.CylinderGeometry(0.3, 0.42, 0.85, 12), coat, { outline: o });
    body.position.y = 1.05;
    const belt = mesh(new T.CylinderGeometry(0.33, 0.33, 0.1, 12), trim);
    belt.position.y = 0.9;
    const head = mesh(new T.SphereGeometry(0.3, 18, 14), skin, { outline: o });
    head.position.y = 1.75;
    const hair = mesh(new T.SphereGeometry(0.34, 18, 14, 0, Math.PI * 2, 0, Math.PI * 0.55), hairM, { outline: 0.04 });
    hair.position.set(0, 1.78, -0.02);
    const tail = mesh(new T.ConeGeometry(0.14, 0.7, 8), hairM, { outline: o });
    tail.position.set(0, 1.55, -0.3);
    tail.rotation.x = 0.5;
    const eyeG = new T.SphereGeometry(0.045, 8, 6);
    [-0.1, 0.1].forEach((x) => {
      const e = new T.Mesh(eyeG, basic(0x3a2255));
      e.position.set(x, 1.76, 0.27);
      g.add(e);
    });
    // マント
    const cape = new T.Mesh(new T.PlaneGeometry(0.7, 0.95), toon(0x2d2780, { side: T.DoubleSide }));
    cape.position.set(0, 1.0, -0.36);
    cape.rotation.x = 0.15;
    cape.castShadow = true;

    const legG = new T.BoxGeometry(0.17, 0.62, 0.17);
    const mkLeg = (x) => {
      const pivot = new T.Group();
      pivot.position.set(x, 0.64, 0);
      const l = mesh(legG, dark, { outline: o });
      l.position.y = -0.31;
      pivot.add(l);
      return pivot;
    };
    const legL = mkLeg(-0.13), legR = mkLeg(0.13);

    const armG = new T.BoxGeometry(0.13, 0.55, 0.13);
    const mkArm = (x) => {
      const pivot = new T.Group();
      pivot.position.set(x, 1.38, 0);
      const a = mesh(armG, coat, { outline: o });
      a.position.y = -0.26;
      pivot.add(a);
      return pivot;
    };
    const armL = mkArm(-0.42), armR = mkArm(0.42);

    // 武器：巨大はんだごて
    const weapon = new T.Group();
    const handle = mesh(new T.CylinderGeometry(0.06, 0.06, 0.4, 8), trim, { outline: 0.1 });
    const rod = mesh(new T.CylinderGeometry(0.03, 0.03, 0.9, 8), toon(0xd0d6de));
    rod.position.y = 0.62;
    const tip = new T.Mesh(new T.ConeGeometry(0.05, 0.25, 8), basic(0xd9b8ff));
    tip.position.y = 1.18;
    const glow = new T.Mesh(new T.SphereGeometry(0.13, 10, 8), new T.MeshBasicMaterial({ color: ELECTRO, transparent: true, opacity: 0.5 }));
    glow.position.y = 1.2;
    weapon.add(handle, rod, tip, glow);
    weapon.position.y = -0.52;
    weapon.rotation.x = Math.PI / 2;
    armR.add(weapon);

    g.add(body, belt, head, hair, tail, cape, legL, legR, armL, armR);
    return { g, legL, legR, armL, armR, glow, cape };
  }

  // ---------- 安東先生（ボス） ----------
  function buildBoss() {
    const g = new T.Group();
    const inner = new T.Group();
    const mats = [];
    const tm = (c) => { const m = toon(c); mats.push(m); return m; };
    const skin = tm(0xf1d2b0), suit = tm(0x3a3f4b), shirt = tm(0xf4f4f4), tie = tm(0xc0392b), hairM = tm(0x505050), black = tm(0x1e1e1e);
    const o = 0.04;

    const legG = new T.CylinderGeometry(0.28, 0.24, 1.4, 10);
    [-0.4, 0.4].forEach((x) => {
      const l = mesh(legG, suit, { outline: o });
      l.position.set(x, 0.7, 0);
      inner.add(l);
    });
    const body = mesh(new T.CylinderGeometry(0.85, 1.05, 2.0, 16), suit, { outline: o });
    body.position.y = 2.3;
    const chest = mesh(new T.BoxGeometry(0.55, 1.1, 0.1), shirt);
    chest.position.set(0, 2.75, 0.86);
    chest.rotation.x = -0.08;
    const tieM = mesh(new T.BoxGeometry(0.2, 0.9, 0.08), tie);
    tieM.position.set(0, 2.65, 0.93);
    tieM.rotation.x = -0.08;

    const head = mesh(new T.SphereGeometry(0.95, 24, 18), skin, { outline: o });
    head.position.y = 4.1;
    const hair = mesh(new T.SphereGeometry(1.0, 24, 18, 0, Math.PI * 2, 0, Math.PI * 0.42), hairM, { outline: o });
    hair.position.set(0, 4.17, -0.05);
    const sideG = new T.SphereGeometry(0.35, 10, 8);
    [-0.85, 0.85].forEach((x) => {
      const s = mesh(sideG, hairM);
      s.position.set(x, 4.05, -0.1);
      s.scale.set(0.6, 1, 1);
      inner.add(s);
    });
    // メガネ
    const frameG = new T.TorusGeometry(0.24, 0.045, 6, 20);
    [-0.33, 0.33].forEach((x) => {
      const f = new T.Mesh(frameG, black);
      f.position.set(x, 4.12, 0.86);
      inner.add(f);
      const eye = new T.Mesh(new T.SphereGeometry(0.07, 8, 6), black);
      eye.position.set(x, 4.1, 0.84);
      inner.add(eye);
      const brow = new T.Mesh(new T.BoxGeometry(0.34, 0.07, 0.07), black);
      brow.position.set(x, 4.45, 0.84);
      brow.rotation.z = x < 0 ? -0.35 : 0.35;
      inner.add(brow);
    });
    const bridge = new T.Mesh(new T.BoxGeometry(0.2, 0.04, 0.04), black);
    bridge.position.set(0, 4.14, 0.9);
    const mouth = new T.Mesh(new T.BoxGeometry(0.4, 0.06, 0.06), tm(0x7a3b2e));
    mouth.position.set(0, 3.62, 0.86);
    mouth.rotation.z = 0.05;

    const armG = new T.CylinderGeometry(0.22, 0.2, 1.5, 10);
    const handG = new T.SphereGeometry(0.24, 10, 8);
    const mkArm = (x) => {
      const pivot = new T.Group();
      pivot.position.set(x, 3.05, 0);
      const a = mesh(armG, suit, { outline: o });
      a.position.y = -0.72;
      const h = mesh(handG, skin);
      h.position.y = -1.5;
      pivot.add(a, h);
      return pivot;
    };
    const armL = mkArm(-1.05), armR = mkArm(1.05);
    // 指示棒
    const stick = mesh(new T.CylinderGeometry(0.035, 0.05, 2.2, 6), tm(0x6b4a2b));
    stick.position.set(0, -1.55, 0.9);
    stick.rotation.x = Math.PI / 2;
    armR.add(stick);

    // 周りを回る教科書
    const coverTex = canvasTex(128, 180, (c, w, h) => {
      c.fillStyle = '#b3342f';
      c.fillRect(0, 0, w, h);
      c.fillStyle = '#fff';
      c.font = 'bold 26px sans-serif';
      c.textAlign = 'center';
      c.fillText('電気回路', w / 2, 70);
      c.font = '16px sans-serif';
      c.fillText('安東 著', w / 2, 140);
    });
    const book = new T.Group();
    const bookM = new T.Mesh(new T.BoxGeometry(0.9, 1.25, 0.2), [
      toon(0xf5f0e0), toon(0xf5f0e0), toon(0xf5f0e0), toon(0xf5f0e0),
      toon(0xffffff, { map: coverTex }), toon(0xb3342f),
    ]);
    bookM.castShadow = true;
    bookM.position.set(2.2, 0, 0);
    book.add(bookM);
    book.position.y = 3.2;

    // 第2形態のオーラ
    const aura = new T.Mesh(new T.SphereGeometry(2.6, 20, 14), new T.MeshBasicMaterial({ color: ELECTRO, transparent: true, opacity: 0.16, depthWrite: false }));
    aura.position.y = 2.6;
    aura.visible = false;

    inner.add(body, chest, tieM, head, hair, bridge, mouth, armL, armR, book, aura);
    inner.scale.setScalar(1.25);
    g.add(inner);

    // 影と位置の目安
    const shadow = new T.Mesh(G.circle, new T.MeshBasicMaterial({ color: 0x000000, transparent: true, opacity: 0.25, depthWrite: false }));
    shadow.rotation.x = -Math.PI / 2;
    shadow.scale.setScalar(BOSS_R);
    scene.add(shadow);

    return { g, inner, armL, armR, book, aura, mats, shadow };
  }

  // ---------- エフェクト ----------
  function addFx(fx) {
    S.fx.push(fx);
    if (fx.obj) scene.add(fx.obj);
    return fx;
  }
  function removeFx(f) {
    if (!f.obj) return;
    scene.remove(f.obj);
    f.obj.traverse((c) => { if (c.userData.own && c.material) c.material.dispose(); });
  }
  function later(sec, fn) { S.timers.push({ t: sec, fn }); }

  function flatMesh(geo, color, opacity) {
    const m = own(new T.Mesh(geo, new T.MeshBasicMaterial({ color, transparent: true, opacity, depthWrite: false })));
    m.userData.own = true;
    m.rotation.x = -Math.PI / 2;
    return m;
  }

  // 赤い円の予告 → 時間が来たら onFire
  function telegraph(pos, r, delay, dur, onFire) {
    const g = new T.Group();
    g.position.set(pos.x, 0.04, pos.z);
    const edge = flatMesh(G.ring, 0xff3b30, 0.9);
    edge.scale.setScalar(r);
    const fill = flatMesh(G.circle, 0xff4d4d, 0.3);
    fill.scale.setScalar(0.001);
    g.add(edge, fill);
    g.visible = false;
    let t = 0;
    return addFx({
      obj: g,
      update(dt) {
        t += dt;
        if (t < delay) return true;
        g.visible = true;
        const k = Math.min(1, (t - delay) / dur);
        fill.scale.setScalar(Math.max(0.001, r * k));
        if (k >= 1) { onFire(); return false; }
        return true;
      },
    });
  }

  function bolt(pos, color = 0xeadfff, width = 0.32) {
    const g = new T.Group();
    const m = own(new T.MeshBasicMaterial({ color, transparent: true, opacity: 1 }));
    const segs = 8;
    let a = new T.Vector3(rnd(-1, 1), 30, rnd(-1, 1));
    for (let i = 0; i < segs; i++) {
      const b = i === segs - 1 ? new T.Vector3(0, 0, 0) : new T.Vector3(rnd(-1.3, 1.3), 30 - (30 / segs) * (i + 1), rnd(-1.3, 1.3));
      const s = new T.Mesh(G.box, m);
      s.userData.own = true;
      s.scale.set(width, a.distanceTo(b), width);
      s.position.copy(a).add(b).multiplyScalar(0.5);
      s.quaternion.setFromUnitVectors(new T.Vector3(0, 1, 0), b.clone().sub(a).normalize());
      g.add(s);
      a = b;
    }
    const flash = flatMesh(G.circle, ELECTRO, 0.6);
    flash.position.y = 0.05;
    flash.scale.setScalar(2.2);
    g.add(flash);
    g.position.set(pos.x, 0, pos.z);
    let t = 0;
    addFx({
      obj: g,
      update(dt) {
        t += dt;
        m.opacity = 1 - t / 0.35;
        flash.material.opacity = 0.6 * (1 - t / 0.35);
        return t < 0.35;
      },
    });
  }

  function sparks(pos, color, n = 12, speed = 7) {
    for (let i = 0; i < n; i++) {
      const p = new T.Mesh(G.spark, basic(color));
      p.position.copy(pos);
      const v = new T.Vector3(rnd(-1, 1), rnd(0.2, 1.6), rnd(-1, 1)).normalize().multiplyScalar(speed * rnd(0.4, 1.2));
      const life = rnd(0.35, 0.7);
      let t = 0;
      addFx({
        obj: p,
        update(dt) {
          t += dt;
          v.y -= 18 * dt;
          p.position.addScaledVector(v, dt);
          p.scale.setScalar(Math.max(0.01, 1 - t / life));
          p.rotation.x += dt * 12;
          return t < life;
        },
      });
    }
  }

  function expandRing(pos, r, color, dur = 0.35) {
    const m = flatMesh(G.ring, color, 0.9);
    m.position.set(pos.x, 0.08, pos.z);
    let t = 0;
    addFx({
      obj: m,
      update(dt) {
        t += dt;
        const k = t / dur;
        m.scale.setScalar(0.3 + r * k);
        m.material.opacity = 0.9 * (1 - k);
        return t < dur;
      },
    });
  }

  function dmgNum(pos, text, cls) {
    const d = document.createElement('div');
    d.className = `dn ${cls}`;
    d.textContent = text;
    ui.nums.appendChild(d);
    S.nums.push({ d, p: pos.clone().add(new T.Vector3(rnd(-0.6, 0.6), 0, rnd(-0.6, 0.6))), t: 0 });
  }

  function say(text, sec = 2.4) {
    ui.sub.innerHTML = `<b>安東</b>${text}`;
    ui.sub.classList.add('is-show');
    S.subT = sec;
  }

  function shake(s) { S.cam.shake = Math.max(S.cam.shake, s); }

  function vignette() {
    ui.vig.classList.remove('is-hit');
    void ui.vig.offsetWidth;
    ui.vig.classList.add('is-hit');
  }

  // ---------- ダメージ ----------
  function dmgBoss(base, kind) {
    const B = S.b;
    if (S.over || B.hp <= 0) return;
    const crit = Math.random() < 0.2;
    let dmg = Math.round(base * (crit ? 1.8 : 1) * rnd(0.9, 1.1));
    dmg = Math.min(dmg, B.hp);
    B.hp -= dmg;
    S.dealt += dmg;
    ctx.add(dmg);
    ctx.setStat('dmg', S.dealt);
    B.flash = 0.12;
    const head = new T.Vector3(B.pos.x, B.y + 4.2, B.pos.z);
    dmgNum(head, dmg, crit ? 'is-crit' : 'is-electro');
    sparks(new T.Vector3(B.pos.x, B.y + 2.5, B.pos.z), ELECTRO, crit ? 16 : 9);
    Sound.hit();
    const P = S.p;
    P.energy = Math.min(100, P.energy + (kind === 'normal' ? 5 : kind === 'skill' ? 15 : 0));

    const r = B.hp / BOSS_MAX;
    if (B.phase === 1 && r <= 0.5) B.pendingPhase = true;
    if (!B.said75 && r <= 0.75) { B.said75 = true; say('まだ単位はあげられませんよ。'); }
    if (!B.said25 && r <= 0.25) { B.said25 = true; say('……なかなか、やりますね。'); }
    if (B.hp <= 0) win();
  }

  function dmgPlayer(amount) {
    const P = S.p;
    if (S.over || P.inv > 0 || P.dodge > 0) return false;
    P.hp = Math.max(0, P.hp - amount);
    P.inv = 0.7;
    P.hurtT = 0.7;
    S.hurt += amount;
    ctx.setStat('hurt', S.hurt);
    dmgNum(new T.Vector3(P.pos.x, P.pos.y + 2, P.pos.z), amount, 'is-hurt');
    vignette();
    shake(0.35);
    Sound.hurt();
    if (P.hp <= 0) lose();
    return true;
  }

  function win() {
    S.over = true;
    S.result = 'win';
    const bonus = 1000 + Math.round(ctx.timeLeft()) * 20 + Math.round(S.p.hp);
    ctx.add(bonus);
    ctx.setStat('win', true);
    ctx.setStat('bonus', bonus);
    say('……いいでしょう。単位を認めます。', 4);
    ui.banner.innerHTML = `撃破！<small>撃破ボーナス +${bonus}</small>`;
    ui.banner.classList.add('is-show');
    Sound.boom();
    shake(0.8);
    for (let i = 0; i < 4; i++) later(i * 0.25, () => sparks(new T.Vector3(S.b.pos.x, 3, S.b.pos.z), i % 2 ? 0xf6d55c : ELECTRO, 24, 10));
    later(2.6, () => ctx.end('撃破！'));
  }

  function lose() {
    S.over = true;
    S.result = 'lose';
    say('来年また会いましょう。', 4);
    later(2.2, () => ctx.end('力尽きた…'));
  }

  // ---------- 主人公の行動 ----------
  function camForward() {
    const y = S.cam.yaw;
    return new T.Vector3(-Math.sin(y), 0, -Math.cos(y));
  }

  function faceForAttack() {
    const P = S.p, B = S.b;
    if (flatDist(P.pos, B.pos) < 10) return Math.atan2(B.pos.x - P.pos.x, B.pos.z - P.pos.z);
    const f = camForward();
    return Math.atan2(f.x, f.z);
  }

  function startSwing() {
    const P = S.p;
    const idx = P.comboIdx;
    P.swing = { t: 0, dur: [0.3, 0.3, 0.46][idx], idx, face: faceForAttack(), hit: false };
    Sound.slash();
  }

  function meleeHit(sw) {
    const P = S.p, B = S.b;
    const d = flatDist(P.pos, B.pos);
    const dir = Math.atan2(B.pos.x - P.pos.x, B.pos.z - P.pos.z);
    const tipPos = new T.Vector3(P.pos.x + Math.sin(P.face) * 1.4, P.pos.y + 1.2, P.pos.z + Math.cos(P.face) * 1.4);
    sparks(tipPos, 0xd9b8ff, 5, 4);
    if (d < BOSS_R + 2.4 && angDiff(P.face, dir) < 1.4 && B.y < 2.5) {
      dmgBoss([45, 55, 95][sw.idx], 'normal');
      if (sw.idx === 2) shake(0.15);
    }
  }

  function dodge() {
    const P = S.p;
    if (S.over || P.dodge > 0 || P.stam < 25) return;
    P.stam -= 25;
    P.stamDelay = 0.8;
    P.dodge = 0.28;
    P.swing = null;
    const mv = inputVector();
    if (mv.lengthSq() > 0) P.dodgeDir.copy(mv);
    else P.dodgeDir.set(-Math.sin(P.face), 0, -Math.cos(P.face));
    P.face = Math.atan2(P.dodgeDir.x, P.dodgeDir.z);
    Sound.slash();
  }

  function jump() {
    const P = S.p;
    if (S.over || !P.onGround) return;
    P.vy = 8.8;
    P.onGround = false;
  }

  function skill() {
    const P = S.p, B = S.b;
    if (S.over || P.skillCd > 0) return;
    P.skillCd = 6;
    P.swing = null;
    expandRing(P.pos, 6, ELECTRO, 0.35);
    expandRing(P.pos, 4, 0xf0e2ff, 0.25);
    sparks(new T.Vector3(P.pos.x, 1, P.pos.z), ELECTRO, 20, 9);
    Sound.zap();
    shake(0.15);
    if (flatDist(P.pos, B.pos) < 5.8 + BOSS_R && B.y < 3) dmgBoss(170, 'skill');
  }

  function burst() {
    const P = S.p;
    if (S.over || P.energy < 100) return;
    P.energy = 0;
    P.inv = Math.max(P.inv, 1.8);
    P.swing = null;
    ui.cutin.classList.remove('is-show');
    void ui.cutin.offsetWidth;
    ui.cutin.classList.add('is-show');
    Sound.burst();
    for (let i = 0; i < 6; i++) {
      later(0.5 + i * 0.18, () => {
        const B = S.b;
        bolt({ x: B.pos.x + rnd(-1, 1), z: B.pos.z + rnd(-1, 1) }, 0xf0e2ff, 0.5);
        expandRing(B.pos, 4, ELECTRO, 0.3);
        shake(0.25);
        Sound.zap();
        dmgBoss(120, 'burst');
      });
    }
  }

  function inputVector() {
    const fz = (keys.has('KeyW') || keys.has('ArrowUp') ? 1 : 0) - (keys.has('KeyS') || keys.has('ArrowDown') ? 1 : 0);
    const rx = (keys.has('KeyD') || keys.has('ArrowRight') ? 1 : 0) - (keys.has('KeyA') || keys.has('ArrowLeft') ? 1 : 0);
    const y = S.cam.yaw;
    const v = new T.Vector3(-Math.sin(y) * fz + Math.cos(y) * rx, 0, -Math.cos(y) * fz - Math.sin(y) * rx);
    if (v.lengthSq() > 0) v.normalize();
    return v;
  }

  function updatePlayer(dt) {
    const P = S.p;
    P.inv = Math.max(0, P.inv - dt);
    P.hurtT = Math.max(0, P.hurtT - dt);
    P.skillCd = Math.max(0, P.skillCd - dt);
    P.energy = Math.min(100, P.energy + dt * 2);
    if (P.stamDelay > 0) P.stamDelay -= dt;
    else P.stam = Math.min(100, P.stam + dt * 30);

    P.vy -= 26 * dt;
    P.pos.y += P.vy * dt;
    if (P.pos.y <= 0) { P.pos.y = 0; P.vy = 0; P.onGround = true; }
    if (S.over) return;

    const mv = inputVector();
    const moving = mv.lengthSq() > 0;
    P.moving = false;
    if (P.dodge > 0) {
      P.dodge -= dt;
      P.pos.addScaledVector(P.dodgeDir, 19 * dt);
    } else if (P.swing) {
      P.pos.addScaledVector(mv, 1.2 * dt);
    } else if (moving) {
      P.pos.addScaledVector(mv, 6.8 * dt);
      P.face = turnTo(P.face, Math.atan2(mv.x, mv.z), dt * 14);
      P.moving = true;
    }

    if (P.swing) {
      const sw = P.swing;
      sw.t += dt;
      P.face = turnTo(P.face, sw.face, dt * 20);
      if (!sw.hit && sw.t >= sw.dur * 0.45) { sw.hit = true; meleeHit(sw); }
      if (sw.t >= sw.dur) {
        P.swing = null;
        P.comboTimer = 0.55;
        P.comboIdx = (sw.idx + 1) % 3;
      }
    } else {
      P.comboTimer -= dt;
      if (P.comboTimer <= 0) P.comboIdx = 0;
      if (S.attackHeld && P.dodge <= 0) startSwing();
    }

    // アリーナの外に出ない・ボスにめりこまない
    const hr = Math.hypot(P.pos.x, P.pos.z);
    if (hr > ARENA_R - 0.6) {
      P.pos.x *= (ARENA_R - 0.6) / hr;
      P.pos.z *= (ARENA_R - 0.6) / hr;
    }
    const B = S.b;
    const dx = P.pos.x - B.pos.x, dz = P.pos.z - B.pos.z;
    const d = Math.hypot(dx, dz);
    const min = BOSS_R + 0.45;
    if (d < min && d > 0.001 && B.y < 1.5) {
      P.pos.x = B.pos.x + (dx / d) * min;
      P.pos.z = B.pos.z + (dz / d) * min;
    }
  }

  // ---------- ボスの攻撃 ----------
  const LINES = {
    lightning: ['抜き打ち小テストです。', '雷に打たれたように覚えなさい。'],
    shots: ['抵抗は無駄です。', 'カラーコード、読めますか？'],
    slam: ['再履修です！', '跳んでよけなさい！'],
    charge: ['遅刻は認めません！', '廊下を走ってはいけません……私以外は。'],
  };

  const ATTACKS = {
    lightning() {
      const B = S.b;
      say(U.pick(LINES.lightning));
      B.pose = 'raise';
      const n = B.phase === 2 ? 7 : 4;
      const gap = B.phase === 2 ? 0.14 : 0.2;
      for (let i = 0; i < n; i++) {
        const P = S.p;
        const pos = i === 0 ? { x: P.pos.x, z: P.pos.z } : { x: P.pos.x + rnd(-5, 5), z: P.pos.z + rnd(-5, 5) };
        telegraph(pos, 2.3, i * gap, 1.0, () => {
          bolt(pos);
          sparks(new T.Vector3(pos.x, 0.3, pos.z), ELECTRO, 8, 6);
          Sound.zap();
          if (flatDist(S.p.pos, pos) < 2.3) dmgPlayer(170);
          if (flatDist(S.p.pos, pos) < 8) shake(0.15);
        });
      }
      let t = 0;
      return (dt) => (t += dt) < 1.2 + n * gap;
    },
    shots() {
      const B = S.b;
      say(U.pick(LINES.shots));
      B.pose = 'point';
      const waves = B.phase === 2 ? 3 : 2;
      const k = B.phase === 2 ? 7 : 5;
      let t = 0, fired = 0;
      return (dt) => {
        t += dt;
        if (fired < waves && t > 0.55 + fired * 0.55) {
          const P = S.p;
          const base = Math.atan2(P.pos.x - B.pos.x, P.pos.z - B.pos.z) + (fired % 2 ? 0.09 : 0);
          for (let i = 0; i < k; i++) shoot(base + (i - (k - 1) / 2) * 0.2);
          fired++;
          Sound.slash();
        }
        return t < 0.9 + waves * 0.55;
      };
    },
    slam() {
      const B = S.b;
      say(U.pick(LINES.slam));
      B.pose = 'slam';
      const waves = B.phase === 2 ? 2 : 1;
      let t = 0, landed = false;
      return (dt) => {
        t += dt;
        if (t < 0.75) B.y = Math.sin((t / 0.75) * Math.PI / 2) * 6;
        else if (t < 0.95) B.y = 6 * (1 - (t - 0.75) / 0.2);
        else {
          B.y = 0;
          if (!landed) {
            landed = true;
            shake(0.6);
            Sound.boom();
            sparks(new T.Vector3(B.pos.x, 0.3, B.pos.z), 0xcdbfa3, 20, 9);
            for (let i = 0; i < waves; i++) later(i * 0.55, () => shockwave(B.pos.clone()));
          }
        }
        return t < 2.0 + (waves - 1) * 0.55;
      };
    },
    charge() {
      const B = S.b;
      say(U.pick(LINES.charge));
      B.pose = 'charge';
      const P = S.p;
      const dir = new T.Vector3(P.pos.x - B.pos.x, 0, P.pos.z - B.pos.z).normalize();
      const len = 24, width = 3.4;
      // 予告の帯
      const band = flatMesh(G.plane, 0xff4d4d, 0.3);
      band.scale.set(width, len, 1);
      band.rotation.z = Math.atan2(dir.x, dir.z);
      band.position.set(B.pos.x + dir.x * len / 2, 0.05, B.pos.z + dir.z * len / 2);
      let bt = 0;
      addFx({ obj: band, update(dt) { bt += dt; band.material.opacity = 0.2 + 0.2 * Math.abs(Math.sin(bt * 10)); return bt < 0.9; } });
      B.face = Math.atan2(dir.x, dir.z);
      B.lockFace = true;
      let t = 0, travelled = 0, hit = false;
      return (dt) => {
        t += dt;
        if (t > 0.9 && travelled < len) {
          const step = 30 * dt;
          travelled += step;
          B.pos.addScaledVector(dir, step);
          const hr = Math.hypot(B.pos.x, B.pos.z);
          if (hr > ARENA_R - 2) {
            B.pos.multiplyScalar((ARENA_R - 2) / hr);
            travelled = len;
          }
          if (!hit && flatDist(S.p.pos, B.pos) < BOSS_R + 0.7 && S.p.pos.y < 2) {
            hit = dmgPlayer(220);
          }
          if (Math.random() < 0.5) sparks(new T.Vector3(B.pos.x, 0.3, B.pos.z), 0xcdbfa3, 2, 4);
        }
        if (t > 2.0) { B.lockFace = false; return false; }
        return true;
      };
    },
  };

  function atkIntro() {
    say('……私から単位を取るつもりですか？', 2.6);
    let t = 0;
    return (dt) => (t += dt) < 2.4;
  }

  function atkRoar() {
    const B = S.b;
    say('……本気を出しましょう。', 2.6);
    B.pose = 'raise';
    boss.aura.visible = true;
    shake(0.6);
    Sound.boom();
    expandRing(B.pos, 12, ELECTRO, 0.7);
    let t = 0;
    return (dt) => (t += dt) < 1.6;
  }

  function shoot(angle) {
    const B = S.b;
    const g = new T.Group();
    const body = new T.Mesh(G.resBody, basic(0xe9d3a4));
    body.rotation.x = Math.PI / 2;
    const lead = new T.Mesh(G.lead, basic(0xb8c0c8));
    lead.rotation.x = Math.PI / 2;
    g.add(body, lead);
    [-0.2, 0, 0.2].forEach((z) => {
      const b = new T.Mesh(G.resBand, basic(U.pick(BAND)));
      b.rotation.x = Math.PI / 2;
      b.position.z = z;
      g.add(b);
    });
    const glow = new T.Mesh(G.spark, basic(0xff6b5f));
    glow.scale.setScalar(3);
    g.add(glow);
    const v = new T.Vector3(Math.sin(angle), 0, Math.cos(angle)).multiplyScalar(B.phase === 2 ? 15 : 12);
    g.position.set(B.pos.x + v.x * 0.12, 1.1, B.pos.z + v.z * 0.12);
    g.rotation.y = angle;
    let t = 0;
    addFx({
      obj: g,
      update(dt) {
        t += dt;
        g.position.addScaledVector(v, dt);
        g.rotation.z += dt * 14;
        const P = S.p;
        if (Math.hypot(g.position.x - P.pos.x, g.position.z - P.pos.z) < 0.8 && Math.abs(P.pos.y + 1 - g.position.y) < 1.1) {
          if (dmgPlayer(110)) {
            sparks(g.position, 0xff6b5f, 8, 5);
            return false;
          }
        }
        return t < 3.5;
      },
    });
  }

  function shockwave(center) {
    const m = own(new T.MeshBasicMaterial({ color: ELECTRO, transparent: true, opacity: 0.6, side: T.DoubleSide, depthWrite: false }));
    const wall = new T.Mesh(G.wall, m);
    wall.userData.own = true;
    wall.position.set(center.x, 0.4, center.z);
    const ring = flatMesh(G.ring, 0xd9b8ff, 0.9);
    ring.position.y = -0.34;
    wall.add(ring);
    let r = 1, hit = false;
    Sound.boom();
    addFx({
      obj: wall,
      update(dt) {
        r += 11 * dt;
        wall.scale.set(r, 1, r);
        const P = S.p;
        const d = flatDist(P.pos, center);
        if (!hit && Math.abs(d - r) < 0.55 && P.pos.y < 0.55) hit = dmgPlayer(190);
        m.opacity = 0.6 * (1 - r / 30);
        return r < 30;
      },
    });
  }

  function updateBoss(dt) {
    const B = S.b;
    B.flash = Math.max(0, B.flash - dt);
    B.lagHp += (B.hp - B.lagHp) * Math.min(1, dt * 2.5);
    if (S.over) {
      if (S.result === 'win') {
        B.sink = Math.min(1, (B.sink || 0) + dt * 0.8);
      }
      return;
    }
    const P = S.p;
    const d = flatDist(P.pos, B.pos);
    if (!B.lockFace) B.face = turnTo(B.face, Math.atan2(P.pos.x - B.pos.x, P.pos.z - B.pos.z), dt * 3);

    if (B.atk) {
      if (B.atk(dt) === false) {
        B.atk = null;
        B.pose = 'idle';
        B.lockFace = false;
        B.t = B.phase === 2 ? rnd(0.7, 1.2) : rnd(1.3, 2.1);
      }
      return;
    }
    if (B.pendingPhase) {
      B.pendingPhase = false;
      B.phase = 2;
      B.atk = atkRoar();
      return;
    }
    B.t -= dt;
    if (d > 6) {
      const sp = (B.phase === 2 ? 3.4 : 2.3) * dt;
      B.pos.x += ((P.pos.x - B.pos.x) / d) * sp;
      B.pos.z += ((P.pos.z - B.pos.z) / d) * sp;
      B.walking = true;
    } else {
      B.walking = false;
    }
    if (B.t <= 0) {
      const opts = Object.keys(ATTACKS).filter((k) => k !== B.last);
      const k = U.pick(opts);
      B.last = k;
      B.walking = false;
      B.atk = ATTACKS[k]();
    }
  }

  // ---------- 見た目の更新 ----------
  function updateModels(dt) {
    const P = S.p, B = S.b;
    S.walkT += dt * (P.moving ? 11 : 0);

    // 主人公
    const pg = player.g;
    pg.position.copy(P.pos);
    pg.rotation.y = P.face;
    const swingL = P.moving ? Math.sin(S.walkT) * 0.7 : 0;
    player.legL.rotation.x = swingL;
    player.legR.rotation.x = -swingL;
    player.armL.rotation.x = -swingL * 0.6;
    if (P.swing) {
      const k = P.swing.t / P.swing.dur;
      if (P.swing.idx === 1) {
        player.armR.rotation.set(-1.4, 0, 0);
        player.armR.rotation.y = 1.6 - k * 3.2;
      } else {
        player.armR.rotation.set(-2.6 + k * 3.2, 0, 0);
      }
    } else {
      player.armR.rotation.set(swingL * 0.6 - 0.2, 0, 0);
    }
    pg.rotation.x = P.dodge > 0 ? 0.35 : 0;
    player.cape.rotation.x = 0.15 + (P.moving ? 0.35 : 0) + (P.dodge > 0 ? 0.5 : 0);
    player.glow.scale.setScalar(1 + Math.sin(S.t * 10) * 0.2 + (P.swing ? 0.8 : 0));
    pg.visible = !(P.hurtT > 0 && Math.floor(S.t * 20) % 2 === 0);
    if (S.over && S.result === 'lose') {
      pg.rotation.x = turnTo(pg.rotation.x, -Math.PI / 2, dt * 4);
      pg.visible = true;
    }

    // ボス
    const bob = Math.sin(S.t * 2) * 0.08;
    boss.g.position.set(B.pos.x, B.y + bob - (B.sink || 0) * 1.5, B.pos.z);
    boss.g.rotation.y = B.face;
    boss.g.rotation.z = (B.sink || 0) * 0.5;
    boss.shadow.position.set(B.pos.x, 0.03, B.pos.z);
    boss.shadow.scale.setScalar(BOSS_R * (1 - Math.min(0.6, B.y / 12)));
    const walk = B.walking ? Math.sin(S.t * 6) * 0.3 : 0;
    const poses = {
      idle: [walk, -walk],
      raise: [0.2, -2.8],
      point: [0.1, -1.5],
      slam: [-2.6, -2.6],
      charge: [0.9, 0.9],
    };
    const [l, r] = poses[B.pose] || poses.idle;
    boss.armL.rotation.x = turnTo(boss.armL.rotation.x, l, dt * 10);
    boss.armR.rotation.x = turnTo(boss.armR.rotation.x, r, dt * 10);
    boss.inner.rotation.x = B.pose === 'charge' ? 0.25 : 0;
    boss.book.rotation.y += dt * (B.phase === 2 ? 2.4 : 1.2);
    if (boss.aura.visible) boss.aura.scale.setScalar(1 + Math.sin(S.t * 6) * 0.06);
    const fl = B.flash > 0 ? 0.5 : 0;
    boss.mats.forEach((m) => m.emissive.setScalar(fl));

    // 背景
    deco.clouds.rotation.y += dt * 0.004;
    deco.islands.forEach((is) => { is.position.y = is.userData.baseY + Math.sin(S.t * 0.5 + is.userData.phase) * 0.8; });
    const ledK = B.phase === 2 ? 0.55 + 0.4 * Math.abs(Math.sin(S.t * 5)) : 0.8;
    deco.leds.forEach((m) => { m.opacity = ledK; });
  }

  function updateCamera(dt) {
    const P = S.p, c = S.cam;
    const target = new T.Vector3(P.pos.x, P.pos.y + 1.6, P.pos.z);
    const cp = Math.cos(c.pitch);
    camera.position.set(
      target.x + Math.sin(c.yaw) * cp * c.dist,
      target.y + Math.sin(c.pitch) * c.dist,
      target.z + Math.cos(c.yaw) * cp * c.dist,
    );
    if (c.shake > 0) {
      c.shake = Math.max(0, c.shake - dt);
      const s = c.shake * 0.6;
      camera.position.x += rnd(-s, s);
      camera.position.y += rnd(-s, s);
    }
    camera.position.y = Math.max(0.4, camera.position.y);
    camera.lookAt(target);
  }

  function updateNums(dt) {
    const w = ui.nums.clientWidth, h = ui.nums.clientHeight;
    for (let i = S.nums.length - 1; i >= 0; i--) {
      const n = S.nums[i];
      n.t += dt;
      n.p.y += dt * 1.4;
      const v = n.p.clone().project(camera);
      if (n.t > 0.9 || v.z > 1) {
        n.d.remove();
        S.nums.splice(i, 1);
        continue;
      }
      const x = ((v.x + 1) / 2) * w, y = ((1 - v.y) / 2) * h;
      const sc = n.t < 0.12 ? 1.5 - n.t * 4 : 1;
      n.d.style.transform = `translate(${x}px, ${y}px) translate(-50%, -50%) scale(${sc})`;
      n.d.style.opacity = n.t > 0.6 ? String(1 - (n.t - 0.6) / 0.3) : '1';
    }
  }

  function updateHud(dt) {
    const P = S.p, B = S.b;
    ui.bossFill.style.width = `${(B.hp / BOSS_MAX) * 100}%`;
    ui.bossLag.style.width = `${(B.lagHp / BOSS_MAX) * 100}%`;
    ui.bossBox.classList.toggle('is-p2', B.phase === 2);
    ui.hpFill.style.width = `${(P.hp / PLAYER_MAX) * 100}%`;
    ui.hpNum.textContent = `${Math.ceil(P.hp)} / ${PLAYER_MAX}`;
    ui.stam.style.width = `${P.stam}%`;
    ui.skE.classList.toggle('is-cd', P.skillCd > 0);
    ui.skEcd.textContent = P.skillCd > 0 ? P.skillCd.toFixed(1) : '';
    ui.skQ.classList.toggle('is-ready', P.energy >= 100);
    ui.skQring.style.strokeDashoffset = String(176 * (1 - P.energy / 100));
    if (S.subT > 0) {
      S.subT -= dt;
      if (S.subT <= 0) ui.sub.classList.remove('is-show');
    }
    ui.lock.classList.toggle('is-show', !S.over && document.pointerLockElement !== renderer.domElement);
  }

  // ---------- ループ ----------
  function frame(now) {
    if (!alive) return;
    raf = requestAnimationFrame(frame);
    const dt = Math.min(0.05, (now - last) / 1000);
    last = now;
    S.t += dt;
    for (let i = S.timers.length - 1; i >= 0; i--) {
      const tm = S.timers[i];
      tm.t -= dt;
      if (tm.t <= 0) {
        S.timers.splice(i, 1);
        tm.fn();
        if (!alive) return;
      }
    }
    updatePlayer(dt);
    updateBoss(dt);
    for (let i = S.fx.length - 1; i >= 0; i--) {
      const f = S.fx[i];
      if (f.update(dt) === false) {
        removeFx(f);
        S.fx.splice(i, 1);
      }
    }
    updateModels(dt);
    updateCamera(dt);
    updateNums(dt);
    updateHud(dt);
    renderer.render(scene, camera);
  }

  // ---------- 入力 ----------
  const GAME_KEYS = ['KeyW', 'KeyA', 'KeyS', 'KeyD', 'ArrowUp', 'ArrowDown', 'ArrowLeft', 'ArrowRight', 'Space', 'ShiftLeft', 'ShiftRight', 'KeyE', 'KeyQ', 'KeyJ'];
  function onKeyDown(e) {
    if (!GAME_KEYS.includes(e.code)) return;
    e.preventDefault();
    if (e.repeat) return;
    keys.add(e.code);
    if (e.code === 'Space') jump();
    if (e.code === 'ShiftLeft' || e.code === 'ShiftRight') dodge();
    if (e.code === 'KeyE') skill();
    if (e.code === 'KeyQ') burst();
    if (e.code === 'KeyJ') S.attackHeld = true; // マウスがうまく使えないとき用
  }
  function onKeyUp(e) {
    keys.delete(e.code);
    if (e.code === 'KeyJ') S.attackHeld = false;
  }
  function locked() { return document.pointerLockElement === renderer.domElement; }
  function requestLock() {
    try {
      const p = renderer.domElement.requestPointerLock();
      if (p && p.catch) p.catch(() => {});
    } catch (e) { /* 使えない環境では J キーで攻撃できる */ }
  }
  function onMouseDown(e) {
    if (e.button === 0) {
      if (!locked()) {
        requestLock();
        return;
      }
      S.attackHeld = true;
    }
    if (e.button === 2) dodge();
  }
  function onMouseUp(e) { if (e.button === 0) S.attackHeld = false; }
  function onMouseMove(e) {
    if (!locked()) return;
    S.cam.yaw -= e.movementX * 0.0026;
    S.cam.pitch = clamp(S.cam.pitch + e.movementY * 0.002, 0.05, 1.15);
  }
  function onLockChange() { if (!locked()) S.attackHeld = false; }
  function onBlur() { keys.clear(); if (S) S.attackHeld = false; }

  // ---------- 開始・終了 ----------
  function resize() {
    const w = el.clientWidth, h = el.clientHeight;
    if (!w || !h) return;
    renderer.setSize(w, h, false);
    camera.aspect = w / h;
    camera.updateProjectionMatrix();
  }

  function start(container, c) {
    el = container;
    ctx = c;
    if (!init()) {
      el.innerHTML = '<p class="boss-error">このパソコンでは3D表示が使えませんでした。<br>ほかのモードで遊んでください。</p>';
      setTimeout(() => ctx.end('3D非対応'), 2500);
      return;
    }
    el.innerHTML = `
      <div class="boss3d">
        <div class="b-boss">
          <div class="b-name"><span class="b-lv">Lv.90</span>電気回路担当　安東</div>
          <div class="b-bar"><div class="b-lag"></div><div class="b-fill"></div></div>
        </div>
        <div class="b-player">
          <div class="b-hp"><div class="b-hp-fill"></div><span class="b-hp-num"></span></div>
          <div class="b-stam"><div class="b-stam-fill"></div></div>
        </div>
        <div class="b-skills">
          <div class="sk sk-e"><span class="sk-icon">⚡</span><span class="sk-cd"></span><kbd>E</kbd><span class="sk-name">放電</span></div>
          <div class="sk sk-q">
            <svg viewBox="0 0 64 64"><circle cx="32" cy="32" r="28" class="sk-ring-bg"/><circle cx="32" cy="32" r="28" class="sk-ring"/></svg>
            <span class="sk-icon">Ω</span><kbd>Q</kbd><span class="sk-name">V=IR</span>
          </div>
        </div>
        <ul class="b-keys">
          <li><kbd>WASD</kbd>移動</li><li><kbd>マウス</kbd>視点</li><li><kbd>左クリック</kbd>攻撃</li>
          <li><kbd>Shift</kbd>回避</li><li><kbd>Space</kbd>ジャンプ</li>
        </ul>
        <div class="b-sub"></div>
        <div class="b-nums"></div>
        <div class="b-vig"></div>
        <div class="b-cutin"><div class="b-cutin-band"><small>元素爆発</small>V = I R</div></div>
        <div class="b-banner"></div>
        <div class="b-lock"><b>画面をクリック</b>して操作開始<small>マウスで視点を動かします（Esc でマウスを戻す）</small></div>
      </div>`;
    const root = U.$('.boss3d', el);
    root.prepend(renderer.domElement);
    Object.assign(ui, {
      root,
      bossBox: U.$('.b-boss', root), bossFill: U.$('.b-fill', root), bossLag: U.$('.b-lag', root),
      hpFill: U.$('.b-hp-fill', root), hpNum: U.$('.b-hp-num', root), stam: U.$('.b-stam-fill', root),
      skE: U.$('.sk-e', root), skEcd: U.$('.sk-cd', root), skQ: U.$('.sk-q', root), skQring: U.$('.sk-ring', root),
      sub: U.$('.b-sub', root), nums: U.$('.b-nums', root), vig: U.$('.b-vig', root),
      cutin: U.$('.b-cutin', root), banner: U.$('.b-banner', root), lock: U.$('.b-lock', root),
    });

    S = {
      t: 0, over: false, result: null, subT: 0, walkT: 0, dealt: 0, hurt: 0,
      fx: [], nums: [], timers: [], attackHeld: false,
      p: {
        pos: new T.Vector3(0, 0, 9), vy: 0, face: Math.PI, hp: PLAYER_MAX, stam: 100, stamDelay: 0,
        inv: 0, dodge: 0, dodgeDir: new T.Vector3(), swing: null, comboIdx: 0, comboTimer: 0,
        skillCd: 0, energy: 40, onGround: true, moving: false, hurtT: 0,
      },
      b: {
        pos: new T.Vector3(0, 0, -7), y: 0, face: 0, hp: BOSS_MAX, lagHp: BOSS_MAX, phase: 1,
        atk: null, t: 0, last: null, pose: 'idle', flash: 0, pendingPhase: false, lockFace: false,
      },
      cam: { yaw: 0, pitch: 0.32, dist: 8.5, shake: 0 },
    };
    S.b.atk = atkIntro();
    boss.aura.visible = false;
    boss.inner.rotation.set(0, 0, 0);
    player.g.rotation.set(0, Math.PI, 0);
    player.g.visible = true;
    ctx.setStat('dmg', 0);

    ro = new ResizeObserver(resize);
    ro.observe(el);
    resize();

    window.addEventListener('keydown', onKeyDown);
    window.addEventListener('keyup', onKeyUp);
    window.addEventListener('mouseup', onMouseUp);
    window.addEventListener('mousemove', onMouseMove);
    window.addEventListener('blur', onBlur);
    renderer.domElement.addEventListener('mousedown', onMouseDown);
    document.addEventListener('pointerlockchange', onLockChange);
    // クリックの案内の上からでも始められるように
    ui.lock.addEventListener('mousedown', (e) => { if (e.button === 0) requestLock(); });

    alive = true;
    last = performance.now();
    raf = requestAnimationFrame(frame);
  }

  function stop() {
    if (!alive) return;
    alive = false;
    cancelAnimationFrame(raf);
    window.removeEventListener('keydown', onKeyDown);
    window.removeEventListener('keyup', onKeyUp);
    window.removeEventListener('mouseup', onMouseUp);
    window.removeEventListener('mousemove', onMouseMove);
    window.removeEventListener('blur', onBlur);
    renderer.domElement.removeEventListener('mousedown', onMouseDown);
    document.removeEventListener('pointerlockchange', onLockChange);
    if (document.pointerLockElement) document.exitPointerLock();
    if (ro) ro.disconnect();
    keys.clear();
    S.fx.forEach(removeFx);
    S.fx = [];
    S.nums.forEach((n) => n.d.remove());
    S.nums = [];
    S.timers = [];
  }

  return { title: 'ボス戦', start, stop };
})();
