/* Noyau du guide : utilitaires, stockage, boutons Copier, moteur de schémas.
   Les données (étapes, contrats, planning) sont dans data/*.js ; les schémas dans schemas.js ; le rendu des pages dans app.js. */
(function(){
"use strict";
const G = window.G = window.G || {};
G.schemas = G.schemas || {};
G.roles = G.roles || {};
const NS = "http://www.w3.org/2000/svg";

/* ---------- utilitaires ---------- */
G.$ = (s, r) => (r || document).querySelector(s);
G.$$ = (s, r) => Array.from((r || document).querySelectorAll(s));
G.esc = s => String(s == null ? "" : s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;");
G.S = function(tag, a, p){ const e = document.createElementNS(NS, tag); for (const k in (a || {})) if (a[k] !== undefined && a[k] !== null) e.setAttribute(k, a[k]); if (p) p.appendChild(e); return e; };
G.T = function(x, y, s, cls, p, anc){ const e = G.S("text", {x, y, class:cls || "", "text-anchor":anc || "start"}, p); e.textContent = s; return e; };
G.reduce = !!(window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches);
G.store = function(k, v){ try { if (v === undefined) return localStorage.getItem(k); localStorage.setItem(k, v); } catch(e) { return null; } return null; };
G.json = function(k, d){ try { const v = JSON.parse(G.store(k) || "null"); return v == null ? d : v; } catch(e) { return d; } };

/* ---------- progression (localStorage, facultatif) ---------- */
G.done = G.json("p6-done", {});
G.setDone = function(id, v){ G.done[id] = !!v; G.store("p6-done", JSON.stringify(G.done)); };
G.checks = G.json("p6-checks", {});
G.setCheck = function(id, v){ G.checks[id] = !!v; G.store("p6-checks", JSON.stringify(G.checks)); };

/* ---------- thème ---------- */
G.initTheme = function(){
  const root = document.documentElement, b = document.getElementById("theme");
  if (!b) return;
  const sys = () => !!(window.matchMedia && window.matchMedia("(prefers-color-scheme: dark)").matches);
  const dark = () => { const t = root.getAttribute("data-theme"); return t ? t === "dark" : sys(); };
  const sync = () => { const d = dark(); b.textContent = d ? "Mode clair" : "Mode sombre"; b.setAttribute("aria-pressed", d); };
  const s = G.store("theme"); if (s === "dark" || s === "light") root.setAttribute("data-theme", s);
  sync();
  b.addEventListener("click", () => { const t = dark() ? "light" : "dark"; root.setAttribute("data-theme", t); G.store("theme", t); sync(); });
  try { window.matchMedia("(prefers-color-scheme: dark)").addEventListener("change", sync); } catch(e) {}
};

/* ---------- boutons Copier ---------- */
document.addEventListener("click", e => {
  const b = e.target.closest(".copy"); if (!b) return;
  const pre = b.parentNode.querySelector("pre"); if (!pre) return;
  const txt = pre.innerText;
  const ok = () => { b.textContent = "Copié"; setTimeout(() => { b.textContent = "Copier"; }, 1400); };
  const fallback = () => { const r = document.createRange(); r.selectNodeContents(pre); const s = window.getSelection(); s.removeAllRanges(); s.addRange(r); b.textContent = "Sélectionné : Ctrl+C"; };
  try { navigator.clipboard.writeText(txt).then(ok, fallback); } catch(err) { fallback(); }
});
window.addEventListener("beforeprint", () => { G.$$("details").forEach(d => { d.dataset.wasOpen = d.open ? "1" : ""; d.open = true; }); });
window.addEventListener("afterprint", () => { G.$$("details").forEach(d => { if (d.dataset.wasOpen === "") d.open = false; }); });

/* ---------- géométrie ---------- */
G.len = p => { let L = 0; for (let i = 1; i < p.length; i++) L += Math.hypot(p[i][0]-p[i-1][0], p[i][1]-p[i-1][1]); return L; };
G.at = (p, d) => {
  for (let i = 1; i < p.length; i++){ const s = Math.hypot(p[i][0]-p[i-1][0], p[i][1]-p[i-1][1]); if (d <= s || i === p.length - 1){ const t = s ? Math.min(1, d / s) : 1; return [p[i-1][0] + (p[i][0]-p[i-1][0]) * t, p[i-1][1] + (p[i][1]-p[i-1][1]) * t]; } d -= s; }
  return p[p.length - 1];
};

/* ---------- primitives de dessin (communes à tous les schémas) ---------- */
G.D = {
  /* zone colorée avec titre */
  zone(p, x, y, w, h, z, title, sub){ const g = G.S("g", {}, p); G.S("rect", {x, y, width:w, height:h, rx:12, class:`z c-${z}`}, g); if (title) G.T(x + 12, y + 20, title, `tz f-${z}`, g); if (sub) G.T(x + w - 10, y + 20, sub, "ts", g, "end"); return g; },
  /* boîte nœud ; k = clé pour mise en évidence */
  node(p, cx, cy, w, h, t1, t2, cls, k){ const g = G.S("g", {"data-k":k || null}, p); G.S("rect", {x:cx - w/2, y:cy - h/2, width:w, height:h, rx:8, class:"nd " + (cls || "")}, g); if (t2){ G.T(cx, cy - 3, t1, "tn", g, "middle"); G.T(cx, cy + 13, t2, "ti", g, "middle"); } else G.T(cx, cy + 4, t1, "tn", g, "middle"); return g; },
  wire(p, pts, cls, w){ return G.S("polyline", {points:pts.map(q => q.join(",")).join(" "), class:"wr " + (cls || ""), "stroke-width":w || null}, p); },
  label(p, x, y, s, cls, anc){ return G.T(x, y, s, cls || "ts", p, anc); },
  pill(p, x, y, type, label, maxX){
    const g = G.S("g", {class:"pl " + type}, p), r = G.S("rect", {rx:6, height:22}, g), t = G.T(x, y + 4, label, "", g, "middle");
    let w = 0; try { w = t.getComputedTextLength(); } catch(e) {} if (!w) w = label.length * 6.3; w += 18;
    const x0 = Math.max(4, Math.min((maxX || 996) - w, x - w / 2)); r.setAttribute("x", x0); r.setAttribute("y", y - 11); r.setAttribute("width", w); t.setAttribute("x", x0 + w / 2); return g;
  },
  arrow(p, x1, y1, x2, y2, cls){ const g = G.S("g", {}, p); G.S("line", {x1, y1, x2, y2, class:"wr " + (cls || ""), "stroke-width":2}, g); const a = Math.atan2(y2 - y1, x2 - x1), L = 9; G.S("path", {d:`M${x2} ${y2} L${x2 - L*Math.cos(a - .4)} ${y2 - L*Math.sin(a - .4)} L${x2 - L*Math.cos(a + .4)} ${y2 - L*Math.sin(a + .4)} Z`, class:(cls || "").replace(/s-/g, "f-") || "f-muted"}, g); return g; }
};

/* ---------- moteur de schémas ----------
   def = {
     q: "question à laquelle répond le schéma",
     vb: [largeur, hauteur], ab: true si bascule état A / état B, abLabels,
     draw(svg, ctx)            dessin de base (ctx.st = "A" | "B")
     steps(ctx) -> [ { cap, path, pkt, pills:[[x,y,type,texte]], hl:[clés], panel:{title, head, rows, mark}, fx(layer, ctx) } ]
     fin(ctx) -> légende de l'état final (affiché sans lancer l'animation)
     legend: [[couleur css, texte]]
     custom: true + mount(hôte) pour les outils interactifs (éditeur, simulateur)
   } */
G.mountSchema = function(host, id, st0){
  const def = G.schemas[id];
  host.innerHTML = "";
  if (!def){ host.innerHTML = `<p class="muted">Schéma « ${G.esc(id)} » introuvable.</p>`; return; }
  const box = document.createElement("div"); box.className = "sch"; host.appendChild(box);
  box.insertAdjacentHTML("beforeend", `<div class="sq"><b>Question</b><span>${def.q}</span></div>`);
  if (def.custom){ const b = document.createElement("div"); box.appendChild(b); def.mount(b); return; }
  const sv = document.createElement("div"); sv.className = "sv"; box.appendChild(sv);
  const svg = G.S("svg", {viewBox:`0 0 ${def.vb[0]} ${def.vb[1]}`, role:"img", "aria-label":def.q}, sv);
  if (def.minw) svg.style.minWidth = def.minw + "px";
  const cap = document.createElement("div"); cap.className = "cap"; cap.setAttribute("aria-live", "polite"); box.appendChild(cap);
  const side = document.createElement("div"); side.className = "side"; side.hidden = true; box.appendChild(side);
  if (def.legend) box.insertAdjacentHTML("beforeend", `<div class="legend">${def.legend.map(([c, t]) => `<span><i style="background:${c}"></i>${t}</span>`).join("")}</div>`);
  const bar = document.createElement("div"); bar.className = "sbar"; box.appendChild(bar);
  const ctx = {st:def.ab ? (st0 || def.ab0 || "A") : null, i:-1, playing:false, speed:1, cancel:null, timer:null};
  bar.innerHTML = `<button class="btn sm" type="button" data-a="first" title="Revenir au début">⏮ Début</button><button class="btn sm" type="button" data-a="prev">◀ Précédente</button><button class="btn sm pri" type="button" data-a="play">▶ Lecture</button><button class="btn sm" type="button" data-a="next">Suivante ▶</button><button class="btn sm" type="button" data-a="end">État final</button><label class="ts" style="font-size:12px">Vitesse <select data-a="speed" aria-label="Vitesse"><option value="0.5">0,5×</option><option value="1" selected>1×</option><option value="2">2×</option></select></label>${def.ab ? `<span class="seg" role="group" aria-label="État">${(def.abLabels || ["État A","État B"]).map((l, j) => `<button type="button" data-st="${j ? "B" : "A"}" aria-pressed="${(j ? "B" : "A") === ctx.st}">${l}</button>`).join("")}</span>` : ""}<span class="cnt"></span>`;
  let base = null, layer = null;
  const steps = () => def.steps(ctx);
  function drawBase(){ svg.innerHTML = ""; base = G.S("g", {}, svg); def.draw(base, ctx); layer = G.S("g", {}, svg); }
  function hl(keys){ G.$$("[data-k]", base).forEach(g => g.classList.toggle("hl", !!keys && keys.includes(g.getAttribute("data-k")))); }
  function panel(p){
    if (!p){ side.hidden = true; return; }
    side.hidden = false;
    side.innerHTML = `<p class="eyebrow">${p.title}</p><div class="tbl"><table><thead><tr>${p.head.map(h => `<th>${h}</th>`).join("")}</tr></thead><tbody>${p.rows.length ? p.rows.map((r, i) => `<tr class="${p.mark != null && (p.mark === i || (p.mark === "last" && i === p.rows.length - 1)) ? "new" : ""}">${r.map(c => `<td>${G.esc(c)}</td>`).join("")}</tr>`).join("") : `<tr><td colspan="${p.head.length}" class="muted">(vide)</td></tr>`}</tbody></table></div>`;
  }
  function trail(s, mode){ if (!s.path) return; G.S("polyline", {points:s.path.map(q => q.join(",")).join(" "), class:`trail ${s.pkt ? "s-" + s.pkt : "s-acc"}${mode ? " " + mode : ""}`}, layer); }
  function decorate(s){ if (s.fx) s.fx(layer, ctx); (s.pills || []).forEach(p => G.D.pill(layer, p[0], p[1], p[2], p[3], def.vb[0] - 4)); hl(s.hl); panel(s.panel); }
  function counter(){ const n = steps().length; bar.querySelector(".cnt").textContent = ctx.i < 0 ? `État final · ${n} étapes` : `Étape ${ctx.i + 1} / ${n}`; }
  function stop(){ ctx.playing = false; clearTimeout(ctx.timer); if (ctx.cancel){ ctx.cancel(); ctx.cancel = null; } bar.querySelector('[data-a="play"]').textContent = "▶ Lecture"; }
  function showFinal(){
    stop(); drawBase(); ctx.i = -1; const S = steps();
    S.forEach(s => trail(s, "fin"));
    const last = S[S.length - 1]; if (last){ if (last.fx) last.fx(layer, ctx); (last.pills || []).forEach(p => G.D.pill(layer, p[0], p[1], p[2], p[3], def.vb[0] - 4)); panel(last.panel); hl(last.hl); }
    (S.filter(s => s.keep).forEach(s => (s.pills || []).forEach(p => G.D.pill(layer, p[0], p[1], p[2], p[3], def.vb[0] - 4))));
    cap.innerHTML = `<span class="k">État final${ctx.st ? " · état " + ctx.st : ""}</span><span>${def.fin ? def.fin(ctx) : ""}</span><span class="muted" style="font-size:13px">Lancez la lecture ou avancez étape par étape pour voir comment on y arrive.</span>`;
    counter();
  }
  function show(i, animate, done){
    if (ctx.cancel){ ctx.cancel(); ctx.cancel = null; }
    drawBase(); const S = steps(); ctx.i = i; const s = S[i];
    for (let j = 0; j < i; j++){ trail(S[j], "old"); if (S[j].keep) (S[j].pills || []).forEach(p => G.D.pill(layer, p[0], p[1], p[2], p[3], def.vb[0] - 4)); }
    cap.innerHTML = `<span class="k">Étape ${i + 1}${ctx.st ? " · état " + ctx.st : ""}</span><span>${s.cap}</span>`;
    counter();
    if (s.path && animate && !G.reduce){
      const L = G.len(s.path);
      const pl = G.S("polyline", {points:s.path.map(q => q.join(",")).join(" "), class:`trail ${s.pkt ? "s-" + s.pkt : "s-acc"}`, "stroke-dasharray":L, "stroke-dashoffset":L}, layer);
      const dot = G.S("circle", {r:7, cx:s.path[0][0], cy:s.path[0][1], class:`pk ${s.pkt ? "f-" + s.pkt : "f-acc"}`}, layer);
      const dur = Math.max(500, L / (0.32 * ctx.speed)), t0 = performance.now(); let live = true, id = 0;
      const tick = now => { if (!live) return; const k = Math.min(1, (now - t0) / dur), q = G.at(s.path, L * k); dot.setAttribute("cx", q[0]); dot.setAttribute("cy", q[1]); pl.setAttribute("stroke-dashoffset", L * (1 - k)); if (k < 1) id = requestAnimationFrame(tick); else { ctx.cancel = null; decorate(s); if (done) done(); } };
      id = requestAnimationFrame(tick);
      ctx.cancel = () => { live = false; cancelAnimationFrame(id); };
    } else {
      trail(s, "");
      if (s.path){ const e = s.path[s.path.length - 1]; G.S("circle", {r:7, cx:e[0], cy:e[1], class:`pk ${s.pkt ? "f-" + s.pkt : "f-acc"}`}, layer); }
      decorate(s); if (done) done();
    }
  }
  function playFrom(i){
    const n = steps().length; if (i >= n){ stop(); showFinal(); return; }
    show(i, true, () => { if (ctx.playing) ctx.timer = setTimeout(() => playFrom(i + 1), (G.reduce ? 1800 : 1100) / ctx.speed); });
  }
  bar.addEventListener("click", e => {
    const b = e.target.closest("button"); if (!b) return;
    if (b.dataset.st){ ctx.st = b.dataset.st; G.$$("[data-st]", bar).forEach(x => x.setAttribute("aria-pressed", x.dataset.st === ctx.st)); stop(); showFinal(); return; }
    const a = b.dataset.a, n = steps().length;
    if (a === "play"){ if (ctx.playing){ stop(); return; } ctx.playing = true; b.textContent = "⏸ Pause"; playFrom(ctx.i < 0 || ctx.i >= n - 1 ? 0 : ctx.i + 1); }
    else if (a === "next"){ stop(); if (ctx.i < n - 1) show(ctx.i + 1, true); else showFinal(); }
    else if (a === "prev"){ stop(); if (ctx.i > 0) show(ctx.i - 1, false); else if (ctx.i === 0) showFinal(); else show(n - 1, false); }
    else if (a === "first"){ stop(); show(0, false); }
    else if (a === "end"){ showFinal(); }
  });
  bar.querySelector('[data-a="speed"]').addEventListener("change", e => { ctx.speed = +e.target.value; });
  showFinal();
};
})();
