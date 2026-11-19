/* Schémas de l'accueil et de la page Contrats, plus la géométrie commune de l'architecture (réutilisée par les rôles). */
(function(){
"use strict";
const {S, T, D, $$} = G;

/* ---------- géométrie commune de l'architecture (reprise du module 0) ---------- */
const H = {
 nginx:{n:"nginx", x:600, row:"dmz", ip:"10.20.0.10", B:{seg:"DMZ-WEB", m:27, vlan:21}, A:{vlan:20}},
 pivot:{n:"dmz-pivot", x:698, row:"dmz", ip:"10.20.0.20", B:{seg:"DMZ-WEB", m:27, vlan:21}, A:{vlan:20}},
 bind9:{n:"bind9", x:815, row:"dmz", ip:"10.20.0.40", B:{seg:"DMZ-DNS", m:27, vlan:22}, A:{vlan:20}},
 api:{n:"API Flask", x:925, row:"dmz", ip:"10.20.0.70", B:{seg:"DMZ-APP", m:27, vlan:23}, A:{vlan:20}},
 c01:{n:"client-01", x:600, row:"lan", ip:"10.10.0.10", B:{seg:"LAN-USERS", m:26, vlan:11}, A:{vlan:10}},
 c03:{n:"client-03", x:698, row:"lan", ip:"10.10.0.12", B:{seg:"LAN-USERS", m:26, vlan:11}, A:{vlan:10}},
 pg:{n:"PostgreSQL", x:815, row:"lan", ip:"10.10.0.70", B:{seg:"LAN-DB", m:27, vlan:12}, A:{vlan:10}},
 admin:{n:"poste admin", x:925, row:"lan", ip:"10.10.0.100", B:{seg:"LAN-ADMIN", m:27, vlan:13}, A:{vlan:10}}
};
const ROW = {dmz:{cy:135, laneA:208, laneB:{21:192, 22:208, 23:224}, zone:[545,48,440,200], seg:82}, lan:{cy:369, laneA:442, laneB:{11:426, 12:442, 13:458}, zone:[545,284,440,200], seg:316}};
const SEGS = [["DMZ-WEB · 10.20.0.0/27","dmz",549,751],["DMZ-DNS · .32/27","dmz",763,867],["DMZ-APP · .64/27","dmz",873,979],["LAN-USERS · 10.10.0.0/26","lan",549,751],["LAN-DB · .64/27","lan",763,867],["LAN-ADMIN · .96/27","lan",873,979]];
const laneY = (k, st) => st === "A" ? ROW[H[k].row].laneA : ROW[H[k].row].laneB[H[k].B.vlan];
G.ARCH = {H, ROW, laneY,
 P:{ce:[120,190], kali:[120,350], if0:[270,275], surW:[309,275], pf:[380,275], surT:[451,275], if1:[490,275], bus:[530,275]},
 hb:k => [H[k].x, ROW[H[k].row].cy + 28],
 above:k => [H[k].x, ROW[H[k].row].cy - 42],
 down(k, st){ const y = laneY(k, st); return [[530, y], [H[k].x, y], G.ARCH.hb(k)]; },
 up(k, st){ return G.ARCH.down(k, st).reverse(); },
 direct(a, b, st){ const y = laneY(a, st); return [G.ARCH.hb(a), [H[a].x, y], [H[b].x, y], G.ARCH.hb(b)]; }
};
/* dessine l'architecture complète ; owners = true pour afficher les pastilles de rôle */
G.drawArch = function(p, st, opt){
  opt = opt || {};
  const own = (g, k, x, y) => { if (!opt.owners) return; (G.OWNERS[k] || []).forEach((r, i) => { const R = G.ROLES.find(z => z.id === r); S("circle", {cx:x - i * 13, cy:y, r:5.5, style:`fill:var(${R.c});stroke:var(--surface);stroke-width:1.5`}, g); }); };
  const grp = k => S("g", {"data-k":k, class:opt.click ? "clk" : null, tabindex:opt.click ? "0" : null, role:opt.click ? "button" : null}, p);
  let g = grp("wan"); D.zone(g, 20, 48, 238, 420, "wan", "WAN simulé", "203.0.113.0/24"); own(g, "wan", 142, 63);
  D.wire(p, [[170,190],[238,190],[238,350],[170,350]], "s-wan"); D.wire(p, [[238,275],[270,275]], "s-wan");
  T(244, 372, "p6-wan", "ts", p, "middle");
  g = grp("ce"); D.node(g, 120, 190, 100, 56, "client-ext", "203.0.113.10", "", null); own(g, "ce", 166, 166);
  g = grp("kali"); D.node(g, 120, 350, 100, 56, "Kali", "203.0.113.66", "", null); own(g, "kali", 166, 326);
  g = grp("mgmt"); D.zone(g, 20, 508, 965, 134, "mgmt", "MGMT hors bande · VMnet9 · 10.99.0.0/24", "SSH depuis 10.99.0.10 · syslog et NTP vers 10.99.0.10"); own(g, "mgmt", 318, 525);
  D.wire(p, [[120,568],[860,568]], "s-mgmt"); D.wire(p, [[120,378],[120,568]], "s-mgmt"); D.wire(p, [[380,370],[380,568]], "s-mgmt"); D.wire(p, [[620,568],[620,580]], "s-mgmt"); D.wire(p, [[860,568],[860,580]], "s-mgmt");
  T(386, 560, "10.99.0.1", "ts", p); T(126, 560, "10.99.0.66 (proposé)", "ts", p);
  g = grp("dock"); D.node(g, 620, 604, 240, 48, "Hôte Docker · conteneurs", "10.99.0.20 (proposé)", "", null); own(g, "dock", 732, 586);
  g = grp("coll"); D.node(g, 860, 604, 170, 48, "Collecteur 10.99.0.10", "C2 · C6 · C9 · C11 · NTP", "", null); own(g, "coll", 937, 586);
  [["dmz","c-dmz","f-dmz"],["lan","c-lan","f-lan"]].forEach(([z, c, f]) => {
    const r = ROW[z].zone; g = grp(z);
    S("rect", {x:r[0], y:r[1], width:r[2], height:r[3], rx:12, class:`z ${c}`, "stroke-dasharray":st === "B" ? "7 5" : null}, g);
    T(r[0] + 14, r[1] + 20, st === "A" ? (z === "dmz" ? "DMZ · 10.20.0.0/24 · VLAN 20" : "LAN · 10.10.0.0/24 · VLAN 10") : (z === "dmz" ? "DMZ découpée" : "LAN découpé"), `tz ${f}`, g);
    own(g, z, r[0] + r[2] - 10, r[1] + 14);
  });
  if (st === "B") SEGS.forEach(([l, z, x1, x2]) => { S("rect", {x:x1, y:ROW[z].seg, width:x2 - x1, height:88, rx:8, class:`z c-${z}`, "fill-opacity":.1}, p); T(x1 + 6, ROW[z].seg + 13, l, `ts f-${z}`, p); });
  /* voies VLAN et bus trunk */
  const lanes = [];
  ["dmz","lan"].forEach(z => {
    const ks = Object.keys(H).filter(k => H[k].row === z), grpY = {};
    ks.forEach(k => { const y = laneY(k, st); (grpY[y] = grpY[y] || []).push(k); });
    Object.keys(grpY).forEach(y => { const hs = grpY[y], mx = Math.max(...hs.map(k => H[k].x)); lanes.push(+y);
      D.wire(p, [[530, +y], [mx, +y]], `s-${z}`); hs.forEach(k => D.wire(p, [[H[k].x, ROW[z].cy + 28], [H[k].x, +y]], `s-${z}`));
      T(mx + 7, +y + 3.5, `VLAN ${st === "A" ? H[hs[0]].A.vlan : H[hs[0]].B.vlan}`, `ts f-${z}`, p); });
  });
  g = grp("trunk"); S("polyline", {points:`490,275 530,275 530,${Math.min(...lanes)} 530,${Math.max(...lanes)}`, class:"wr s-trunk", "stroke-width":4.5}, g);
  S("polyline", {points:`490,275 530,275`, class:"wr s-trunk", "stroke-width":4.5}, g);
  T(511, 293, "802.1Q", "ts", g, "middle"); T(511, 305, "p6-trunk", "ts", g, "middle"); own(g, "trunk", 536, 300);
  /* OPNsense */
  g = grp("opn"); S("rect", {x:270, y:180, width:220, height:190, rx:12, class:"nd acc"}, g); T(380, 202, "OPNsense", "tb", g, "middle"); T(380, 218, "passerelle de chaque segment", "ts", g, "middle"); own(g, "opn", 482, 192);
  T(281, 246, "em0", "ts", g); T(480, 246, "em1", "ts", g, "end"); T(380, 362, "em2", "ts", g, "middle");
  const off = st === "A" && !opt.idsInA;
  [["surW",280],["surT",422]].forEach(([k, x]) => { const gg = grp(k); S("rect", {x, y:252, width:58, height:46, rx:7, class:"nd ids" + (off ? " off" : "")}, gg); T(x + 29, 272, "Suricata", "tn", gg, "middle"); T(x + 29, 287, off ? "absent (A)" : "C3", "ts", gg, "middle"); own(gg, k, x + 52, 250); });
  g = grp("pf"); S("rect", {x:350, y:252, width:60, height:46, rx:7, class:"c-acc", "fill-opacity":1}, g); T(380, 281, "pf", "tb f-on", g, "middle"); own(g, "pf", 404, 250);
  g = grp("nat"); S("rect", {x:325, y:310, width:110, height:24, rx:5, class:"nd acc", "stroke-dasharray":"3 2"}, g); T(380, 326, "NAT 203.0.113.1", "ts", g, "middle"); own(g, "nat", 430, 308);
  Object.keys(H).forEach(k => { const h = H[k], cy = ROW[h.row].cy; g = grp(k); D.node(g, h.x, cy, 92, 56, h.n, `${h.ip}/${st === "A" ? 24 : h.B.m}`, h.row === "dmz" ? "s-dmz" : "s-lan"); g.querySelector("rect").setAttribute("style", `stroke:var(--${h.row})`); own(g, k, h.x + 40, cy - 30); });
};

/* ---------- accueil : carte avec responsables ---------- */
const ELT = {wan:"WAN simulé et confinement", kali:"Kali (attaquant externe)", ce:"client-ext (client légitime externe)", opn:"OPNsense", pf:"pf (C1)", nat:"NAT de destination", surW:"Suricata sur em0 (C3)", surT:"Suricata sur le trunk (C3)", trunk:"Trunk 802.1Q (p6-trunk)", dmz:"DMZ et ses segments", lan:"LAN et ses segments", nginx:"nginx", pivot:"dmz-pivot", bind9:"bind9", api:"API Flask", c01:"client-01 (sonde légitime)", c03:"client-03 (attaquant interne)", pg:"PostgreSQL", admin:"poste admin", mgmt:"Réseau MGMT (VMnet9)", coll:"Collecteur", dock:"Hôte Docker"};
G.schemas["home-archi"] = {
 q:"Qui est responsable de chaque élément de l'architecture ?",
 custom:true,
 mount(host){
  let st = "B";
  host.innerHTML = `<div class="sv"><svg viewBox="0 0 1000 652" role="img" aria-label="Architecture et responsables" style="min-width:760px"></svg></div><div class="cap" aria-live="polite"></div><div class="legend">${G.ROLES.map(r => `<span><i style="background:var(${r.c});border-radius:50%;width:11px;height:11px"></i>${r.n} · ${r.court}</span>`).join("")}</div><div class="sbar"><span class="seg" role="group" aria-label="État"><button type="button" data-st="A" aria-pressed="false">État A</button><button type="button" data-st="B" aria-pressed="true">État B</button></span><span class="cnt">Cliquez sur un élément</span></div>`;
  const svg = host.querySelector("svg"), cap = host.querySelector(".cap");
  const draw = () => { svg.innerHTML = ""; G.drawArch(svg, st, {owners:true, click:true}); };
  const show = k => { $$("[data-k]", svg).forEach(g => g.classList.toggle("hl", g.getAttribute("data-k") === k)); const rs = (G.OWNERS[k] || []).map(id => G.ROLES.find(r => r.id === id)); cap.innerHTML = `<span class="k">${ELT[k] || k}</span><span>Responsable${rs.length > 1 ? "s" : ""} : ${rs.map(r => `<a href="#role-${r.id}" style="color:var(${r.c});font-weight:700">${r.n} · ${r.titre}</a>`).join(" ; ")}</span>`; };
  svg.addEventListener("click", e => { const g = e.target.closest("[data-k]"); if (g) show(g.getAttribute("data-k")); });
  svg.addEventListener("keydown", e => { if (e.key === "Enter" || e.key === " "){ const g = e.target.closest("[data-k]"); if (g){ e.preventDefault(); show(g.getAttribute("data-k")); } } });
  host.querySelector(".seg").addEventListener("click", e => { const b = e.target.closest("[data-st]"); if (!b) return; st = b.dataset.st; $$("[data-st]", host).forEach(x => x.setAttribute("aria-pressed", x.dataset.st === st)); draw(); });
  draw();
  cap.innerHTML = `<span class="k">Lecture</span><span>Chaque pastille de couleur indique le rôle responsable de l'élément. Cliquez sur un élément pour voir son ou ses responsables et ouvrir leur espace. Basculez entre l'état A et l'état B : les hôtes ne bougent pas.</span>`;
 }
};

/* ---------- accueil : Gantt ---------- */
G.schemas["home-gantt"] = {
 q:"Qui fait quoi, quand, et qu'est-ce qui retarde tout le monde si ça glisse ?",
 custom:true,
 mount(host){
  host.innerHTML = `<div class="sv"><svg role="img" aria-label="Planning" style="min-width:900px"></svg></div><div class="cap" aria-live="polite"></div><div class="legend"><span><i style="background:var(--fg)"></i>bordure épaisse : chemin critique</span><span><i style="background:var(--warn)"></i>losange : jalon du rapport</span><span><i style="background:var(--acc)"></i>trait vertical : aujourd'hui</span></div><div class="sbar"><label class="ts" style="font-size:12.5px"><input type="checkbox" id="g-all"> afficher toutes les dépendances</label><span class="cnt">Cliquez sur une barre</span></div>`;
  const svg = host.querySelector("svg"), cap = host.querySelector(".cap");
  const X0 = 160, DW = 54, d0 = 13, d1 = 30, W = X0 + (d1 - d0 + 1) * DW + 10;
  /* attribution des sous-lignes pour éviter les chevauchements */
  const rows = [];
  G.ROLES.forEach(r => { const lanes = []; G.TASKS.filter(t => t.r === r.id).forEach(t => { let L = lanes.findIndex(e => e < t.d1); if (L < 0){ L = lanes.length; lanes.push(0); } lanes[L] = t.d2; t._lane = L; }); rows.push({r, n:Math.max(1, lanes.length)}); });
  let y = 70; rows.forEach(o => { o.y = y; y += o.n * 30 + 14; });
  const Hh = y + 20;
  svg.setAttribute("viewBox", `0 0 ${W} ${Hh}`);
  const X = d => X0 + (d - d0) * DW;
  const today = (() => { const n = new Date(); return n.getFullYear() === 2026 && n.getMonth() === 10 ? n.getDate() : null; })();
  function draw(all){
    svg.innerHTML = "";
    for (let d = d0; d <= d1; d++){ const we = [14,15,21,22,28,29].includes(d); S("rect", {x:X(d), y:44, width:DW, height:Hh - 54, style:`fill:${we ? "var(--surface-2)" : "transparent"}`}, svg); S("line", {x1:X(d), y1:44, x2:X(d), y2:Hh - 10, style:"stroke:var(--line);stroke-width:1"}, svg); T(X(d) + DW / 2, 40, String(d), "tl", svg, "middle"); }
    T(X0 + 2, 16, "novembre 2026 · colonnes grisées : week-ends", "ts", svg);
    G.MILESTONES.forEach(m => { const x = X(m.d) + DW / 2; const g = S("g", {}, svg); S("path", {d:`M${x} 48 l7 7 l-7 7 l-7 -7 z`, style:"fill:var(--warn)"}, g); const tt = S("title", {}, g); tt.textContent = `${m.d} nov. : ${m.t}`; });
    T(8, 59, "Jalons", "tl", svg);
    if (today) S("line", {x1:X(today) + DW / 2, y1:44, x2:X(today) + DW / 2, y2:Hh - 10, style:"stroke:var(--acc);stroke-width:2.5"}, svg);
    rows.forEach(o => { T(8, o.y + 19, `${o.r.n} · ${o.r.court}`, "tn", svg).setAttribute("style", `fill:var(${o.r.c})`); S("line", {x1:0, y1:o.y + o.n * 30 + 6, x2:W, y2:o.y + o.n * 30 + 6, style:"stroke:var(--line);stroke-dasharray:2 3"}, svg); });
    const pos = {};
    G.TASKS.forEach(t => { const o = rows.find(z => z.r.id === t.r); pos[t.id] = {x:X(t.d1) + 2, w:(t.d2 - t.d1 + 1) * DW - 4, y:o.y + t._lane * 30 + 2}; });
    G.TASKS.forEach(t => (t.dep || []).forEach(dId => { const a = pos[dId], b = pos[t.id], dt = G.TASKS.find(z => z.id === dId); if (!a || !b) return; const crit = t.crit && dt.crit; if (!all && !crit) return;
      const x1 = a.x + a.w, y1 = a.y + 12, x2 = b.x, y2 = b.y + 12, mx = Math.max(x1 + 8, x2 - 10);
      S("path", {d:`M${x1} ${y1} H${mx} V${y2} H${x2 - 3}`, style:`fill:none;stroke:${crit ? "var(--fg)" : "var(--muted)"};stroke-width:${crit ? 1.8 : 1};stroke-dasharray:${crit ? "none" : "3 3"}`}, svg);
      S("path", {d:`M${x2} ${y2} l-7 -4 v8 z`, style:`fill:${crit ? "var(--fg)" : "var(--muted)"}`}, svg); }));

    G.TASKS.forEach(t => { const o = rows.find(z => z.r.id === t.r); const x = X(t.d1) + 2, w = (t.d2 - t.d1 + 1) * DW - 4, yy = o.y + t._lane * 30 + 2; pos[t.id] = {x, w, y:yy};
      const g = S("g", {class:"clk", "data-t":t.id, tabindex:"0", role:"button", "aria-label":t.t}, svg);
      S("rect", {x, y:yy, width:w, height:24, rx:5, style:`fill:var(${o.r.c});fill-opacity:${t.crit ? .9 : .45};stroke:${t.crit ? "var(--fg)" : "none"};stroke-width:${t.crit ? 2.5 : 0}`}, g);
      const tx = T(x + 6, yy + 16, t.t, "tl", g); tx.setAttribute("style", `fill:${t.crit ? "var(--surface)" : "var(--fg)"}`);
      const cw = w - 10; try { while (tx.getComputedTextLength() > cw && tx.textContent.length > 4) tx.textContent = tx.textContent.slice(0, -2) + "…"; } catch(e) {}
      const tt = S("title", {}, g); tt.textContent = `${t.t} · ${t.d1}–${t.d2} nov.`; });
  }
  const show = id => { const t = G.TASKS.find(z => z.id === id), r = G.ROLES.find(z => z.id === t.r); const deps = (t.dep || []).map(d => G.TASKS.find(z => z.id === d)); const succ = G.TASKS.filter(z => (z.dep || []).includes(id));
    cap.innerHTML = `<span class="k">${t.d1}${t.d2 !== t.d1 ? "–" + t.d2 : ""} nov. · rôle ${r.n}${t.crit ? " · chemin critique" : ""}</span><span><b>${t.t}</b>${t.step ? ` · <a href="#role-${t.step}">ouvrir l'étape</a>` : ""}</span><span>Attend : ${deps.length ? deps.map(d => `${d.t} (rôle ${G.ROLES.find(z => z.id === d.r).n})`).join(", ") : "rien"}. Bloque : ${succ.length ? succ.map(d => `${d.t} (rôle ${G.ROLES.find(z => z.id === d.r).n})`).join(", ") : "aucune tâche"}.</span>`; };
  svg.addEventListener("click", e => { const g = e.target.closest("[data-t]"); if (g) show(g.getAttribute("data-t")); });
  svg.addEventListener("keydown", e => { if (e.key === "Enter"){ const g = e.target.closest("[data-t]"); if (g) show(g.getAttribute("data-t")); } });
  host.querySelector("#g-all").addEventListener("change", e => draw(e.target.checked));
  draw(false);
  cap.innerHTML = `<span class="k">Chemin critique</span><span>VMware et OPNsense de base (rôle 1, fait) → VLAN, NAT, FA (fait par le rôle 1) → services A (4) → C2 et test_flux (2) → campagne A (5) → état B (2, 4) → IDS puis IPS (3) → campagne B (5) → analyse (6) → rapport et démonstration (1). Un jour de retard sur une barre à bordure épaisse décale la remise. Exemple : le rôle 5 attend que les rôles 2 et 4 aient fini l'état A, et que les rôles 3 et 6 aient livré la sonde et la collecte.</span>`;
 }
};

/* ---------- contrats : flux des fichiers ---------- */
G.schemas["contrats-flux"] = {
 q:"Quel rôle produit chaque fichier, et qui en dépend ?",
 custom:true,
 mount(host){
  host.innerHTML = `<div class="sv"><svg viewBox="0 0 1000 470" role="img" aria-label="Producteurs et consommateurs des fichiers" style="min-width:760px"></svg></div><div class="cap" aria-live="polite"></div>`;
  const svg = host.querySelector("svg"), cap = host.querySelector(".cap");
  const F = [["flows.csv","parefeu",["parefeu"]],["segments.csv","parefeu",["parefeu","services"]],["hosts.csv","parefeu",["parefeu","attaques","analyse"]],["tests.csv","parefeu",["parefeu","attaques"]],["scenarios.yaml","attaques",["attaques","analyse"]],["runs.csv","attaques",["analyse"]],["events.csv","analyse",["analyse","ids"]],["sla.csv","ids",["analyse"]]];
  const ry = i => 50 + i * 68, fy = i => 40 + i * 53;
  const draw = sel => {
    svg.innerHTML = "";
    T(110, 22, "Producteur", "tl", svg, "middle"); T(500, 22, "Fichier", "tl", svg, "middle"); T(890, 22, "Consommateur", "tl", svg, "middle");
    F.forEach(([f, p, cs], i) => { const on = sel == null || sel === i, op = on ? 1 : .12; const pi = G.ROLES.findIndex(r => r.id === p);
      S("path", {d:`M190 ${ry(pi)} C 300 ${ry(pi)}, 320 ${fy(i)}, 420 ${fy(i)}`, style:`fill:none;stroke:var(${G.ROLES[pi].c});stroke-width:2;opacity:${op}`}, svg);
      cs.forEach(c => { const ci = G.ROLES.findIndex(r => r.id === c); S("path", {d:`M580 ${fy(i)} C 680 ${fy(i)}, 700 ${ry(ci)}, 810 ${ry(ci)}`, style:`fill:none;stroke:var(${G.ROLES[pi].c});stroke-width:2;stroke-dasharray:4 3;opacity:${op}`}, svg); }); });
    G.ROLES.forEach((r, i) => { [[30, "L"], [810, "R"]].forEach(([x]) => { S("rect", {x, y:ry(i) - 16, width:160, height:32, rx:7, style:`fill:var(--surface);stroke:var(${r.c});stroke-width:2`}, svg); T(x + 80, ry(i) + 4, `${r.n} · ${r.court}`, "tn", svg, "middle"); }); });
    F.forEach(([f], i) => { const g = S("g", {class:"clk", "data-f":i, tabindex:"0", role:"button"}, svg); S("rect", {x:420, y:fy(i) - 15, width:160, height:30, rx:6, class:"nd" + (sel === i ? " acc" : "")}, g); T(500, fy(i) + 4, f, "tl", g, "middle"); });
  };
  const show = i => { draw(i); const [f, p, cs] = F[i], c = G.CONTRATS.find(x => x.f.endsWith(f)); cap.innerHTML = `<span class="k">${f}</span><span>Produit par le rôle ${G.ROLES.find(r => r.id === p).n}, lu par ${cs.map(x => "le rôle " + G.ROLES.find(r => r.id === x).n).join(", ")}. Colonnes : <code>${c ? c.cols.join(", ") : ""}</code>. <a href="#contrat-${c ? c.id : ""}">Voir le contrat</a>.</span>`; };
  svg.addEventListener("click", e => { const g = e.target.closest("[data-f]"); if (g) show(+g.getAttribute("data-f")); });
  svg.addEventListener("keydown", e => { if (e.key === "Enter"){ const g = e.target.closest("[data-f]"); if (g) show(+g.getAttribute("data-f")); } });
  draw(null);
  cap.innerHTML = `<span class="k">Lecture</span><span>Trait plein : le rôle produit le fichier. Pointillé : le fichier est lu par un rôle. Cliquez sur un fichier pour isoler ses liens.</span>`;
 }
};
})();
