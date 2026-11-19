/* Schémas du rôle 3 : position de netmap, seuil, faux positifs du réglage, panne P-01. */
(function(){
"use strict";
const {S, T, D, $$} = G;

/* ---------- netmap entre la carte et la pile ---------- */
G.schemas["netmap-position"] = {
 q:"Où Suricata prend-il les paquets, et pourquoi un paquet rejeté par Suricata n'apparaît-il jamais dans filterlog ?",
 vb:[1000, 330], minw:800, ab:true, ab0:"B", abLabels:["IDS (copie)","IPS en ligne (netmap)"],
 draw(p, ctx){
  const B = ctx.st === "B";
  S("rect", {x:150, y:30, width:830, height:270, rx:12, class:"nd acc"}, p); T(166, 52, "VM OPNsense (FreeBSD)", "tb", p);
  if (B){ S("rect", {x:280, y:70, width:170, height:160, rx:10, style:"fill:var(--ids);fill-opacity:.08;stroke:var(--ids);stroke-dasharray:5 4"}, p); T(290, 88, "netmap", "tl f-ids", p); }
  /* traits d'abord : les boîtes passent par-dessus */
  D.wire(p, [[125,150],[170,150]]); D.wire(p, [[600,150],[610,150]]); D.wire(p, [[730,150],[760,150]]);
  if (B){ D.wire(p, [[260,150],[290,150]], "s-ids"); D.wire(p, [[440,150],[470,150]], "s-ids"); }
  else { D.wire(p, [[260,150],[470,150]]); S("path", {d:"M275 150 V200 H365 V176", class:"wr s-ids", "stroke-dasharray":"4 3"}, p); T(279, 214, "copie", "ts f-ids", p); }
  D.wire(p, [[670,173],[670,242]]); D.wire(p, [[365,176],[365,242]], "s-ids");
  D.node(p, 70, 150, 110, 46, "câble virtuel", "p6-wan (WAN)", "", "wire");
  D.node(p, 215, 150, 90, 46, "em0", "carte (pilote)", "", "nic");
  D.node(p, 365, 150, 150, 52, "Suricata", B ? "décide : passe / drop" : "reçoit une copie", "ids", "sur");
  D.node(p, 535, 150, 130, 46, "pile IP du noyau", "", "", "stack");
  const g = S("g", {"data-k":"pf"}, p); S("rect", {x:610, y:127, width:120, height:46, rx:7, class:"c-acc", "fill-opacity":1}, g); T(670, 149, "pf", "tb f-on", g, "middle"); T(670, 165, "état, NAT, règles", "ts", g, "middle").setAttribute("style", "fill:var(--on-acc);stroke:none");
  D.node(p, 830, 150, 140, 46, "em1 → trunk", B ? "Suricata 2e passage" : "", "", "out");
  D.node(p, 670, 260, 150, 36, "filterlog", "", "", "log"); D.node(p, 365, 260, 140, 36, "EVE JSON", "", "", "eve");
 },
 steps(ctx){
  const B = ctx.st === "B";
  if (!B) return [
   {cap:"En mode IDS (capture pcap), le paquet va directement de em0 à la pile IP. Suricata reçoit une <b>copie</b> : il peut alerter, jamais bloquer.", path:[[125,150],[470,150]], pkt:"wan", hl:["nic","stack"]},
   {cap:"pf traite le paquet original : état, redirection, règles. S'il le bloque, une ligne part dans filterlog.", path:[[600,150],[670,150],[670,242]], pkt:"acc", hl:["pf","log"], pills:[[670,105,"acc","pf décide"]]},
   {cap:"Pendant ce temps, Suricata analyse sa copie et écrit éventuellement une alerte EVE. Le paquet est déjà passé : c'est la phase de réglage du rapport (alert seulement).", path:[[275,150],[275,200],[365,200],[365,242]], pkt:"ids", hl:["sur","eve"], pills:[[365,105,"ids","alerte possible, aucun blocage"]]}
  ];
  return [
   {cap:"Le paquet arrive sur em0. Avec <b>netmap</b>, l'anneau de réception de la carte est détourné : le paquet ne monte pas vers la pile, il est remis à Suricata.", path:[[125,150],[290,150]], pkt:"wan", hl:["nic"]},
   {cap:"Suricata décode (Ethernet, VLAN, IP, TCP/UDP), suit le flux, compare aux signatures et aux seuils. Verdict : passe, ou drop.", path:[[290,150],[365,150]], pkt:"ids", hl:["sur"], pills:[[365,105,"ids","inspection en ligne"]]},
   {cap:"Si le verdict est <b>drop</b>, le paquet n'est pas réinjecté : il disparaît ici. pf ne l'a jamais vu, donc <b>aucune ligne filterlog</b>. La trace est dans EVE (alerte « blocked ») et dans le compteur de rejets de stats.log : avec un seuil, une seule alerte peut couvrir de nombreux paquets rejetés.", path:[[365,176],[365,242]], pkt:"bad", hl:["sur","eve"], pills:[[365,105,"bad","drop : pf ne le verra jamais"]]},
   {cap:"Si le verdict est <b>passe</b>, Suricata réinjecte le paquet vers la pile IP, puis pf applique état, NAT et règles (section 9.1).", path:[[440,150],[670,150]], pkt:"acc", hl:["stack","pf"], pills:[[670,105,"ok","pf : règles de em0"]]},
   {cap:"Le paquet routé sort par em1, où Suricata est aussi actif : <b>seconde inspection</b>, cette fois avec les adresses internes (après NAT). Ce coût entre dans la latence mesurée.", path:[[730,150],[830,150]], pkt:"trunk", hl:["out"], pills:[[830,105,"ids","2e passage (em1)"]]}
  ];
 },
 fin:ctx => ctx.st === "B" ? "En IPS netmap, Suricata est avant pf pour le trafic entrant : un drop ne laisse aucune trace dans filterlog. À vérifier au test J1 sur les cartes em (VMware) ; repli : mode Divert ou IDS seul (section 5.3)." : "En IDS, Suricata analyse une copie : il voit tout, alerte, mais ne retarde ni ne bloque rien.",
 legend:[["var(--ids)","Suricata / netmap"],["var(--acc)","pf"],["var(--bad)","paquet rejeté"]]
};

/* ---------- simulateur de seuil ---------- */
const PAT = {
 nmap:["nmap -sS (S-01) : ~100 SYN/s pendant 10 s, une source", () => { const a = []; for (let i = 0; i < 1000; i++) a.push({t:i * .01, s:"203.0.113.66"}); return a; }],
 lent:["nmap lent (-T2) : 1 SYN toutes les 0,4 s", () => { const a = []; for (let t = 0; t < 10; t += .4) a.push({t, s:"203.0.113.66"}); return a; }],
 legit:["client-ext légitime : 1 connexion par seconde", () => { const a = []; for (let t = .2; t < 10; t += 1) a.push({t, s:"203.0.113.10"}); return a; }],
 rafale:["navigateur : rafale de 30 connexions", () => { const a = []; for (let i = 0; i < 30; i++) a.push({t:1 + i * .03, s:"203.0.113.10"}); return a; }],
 dist:["scan distribué : 10 sources × 8 SYN en 4 s", () => { const a = []; for (let s = 0; s < 10; s++) for (let i = 0; i < 8; i++) a.push({t:.5 + i * .5 + s * .04, s:"203.0.113." + (100 + s)}); return a.sort((x, y) => x.t - y.t); }]
};
G.schemas["seuil-sim"] = {
 q:"Avec « 20 SYN en 5 s par source », quel trafic déclenche une alerte, quels paquets sont rejetés, et que se passe-t-il pour un vrai client ?",
 custom:true,
 mount(host){
  host.innerHTML = `<div style="padding:12px 14px;display:grid;gap:10px"><div style="display:flex;flex-wrap:wrap;gap:8px;align-items:center"><label class="ts">Trafic <select data-x="pat">${Object.keys(PAT).map(k => `<option value="${k}">${PAT[k][0]}</option>`).join("")}</select></label><label class="ts">Mécanisme <select data-x="mec"><option value="thr">threshold type both (alerte, sid 9000001)</option><option value="rf" selected>rate_filter new_action drop (blocage temporaire, sid 9000002)</option><option value="df">detection_filter + drop</option></select></label></div><div style="display:flex;flex-wrap:wrap;gap:8px;align-items:center"><label class="ts">count <input type="number" data-x="n" value="20" min="1" max="200" style="width:70px"></label><label class="ts">seconds <input type="number" data-x="sec" value="5" min="1" max="60" style="width:70px"></label><label class="ts">timeout (rate_filter) <input type="number" data-x="to" value="20" min="1" max="120" style="width:70px"></label><label class="ts">compter <select data-x="tr"><option>by_src</option><option>by_dst</option></select></label></div></div><div class="sv"><svg viewBox="0 0 900 220" role="img" aria-label="Chronologie des SYN" style="min-width:700px"></svg></div><div class="side" style="display:grid"><div class="tbl" data-x="kpi"></div></div><div class="cap" data-x="cap" aria-live="polite"></div><p class="ts" style="padding:0 14px 12px;font-size:12px">Modèle simplifié : fenêtre ouverte au premier paquet compté, remise à zéro après « seconds ». Le comportement exact dépend de la version de Suricata : à vérifier par une exécution de S-01 en mode IDS.</p>`;
  const q = s => host.querySelector(`[data-x="${s}"]`), svg = host.querySelector("svg");
  function run(){
   const pk = q("pat").value, mec = q("mec").value, N = Math.max(1, +q("n").value || 20), sec = Math.max(1, +q("sec").value || 5), to = Math.max(1, +q("to").value || 20), tr = q("tr").value;
   const C = {}, blockedUntil = {}, res = [];
   PAT[pk][1]().forEach(p => {
    const key = tr === "by_src" ? p.s : "203.0.113.1";
    let c = C[key]; if (!c || p.t - c.start > sec) c = C[key] = {start:p.t, n:0, fired:false};
    c.n++; let alert = false, drop = false;
    if (mec === "thr"){ if (c.n >= N && !c.fired){ alert = true; c.fired = true; } }
    else if (mec === "df"){ if (c.n > N){ alert = true; drop = true; } }
    else { if (blockedUntil[key] && p.t < blockedUntil[key]) drop = true; else if (c.n > N){ drop = true; alert = !c.fired; c.fired = true; blockedUntil[key] = p.t + to; } }
    res.push({t:p.t, s:p.s, alert, drop});
   });
   svg.innerHTML = "";
   const X = t => 40 + t * 82, srcs = [...new Set(res.map(r => r.s))], Y = s => srcs.length > 1 ? 40 + srcs.indexOf(s) * 100 / (srcs.length - 1) : 95;
   S("line", {x1:40, y1:150, x2:860, y2:150, style:"stroke:var(--line)"}, svg);
   for (let t = 0; t <= 10; t++){ S("line", {x1:X(t), y1:150, x2:X(t), y2:156, style:"stroke:var(--muted)"}, svg); T(X(t), 172, t + " s", "ts", svg, "middle"); }
   for (let w = 0; w < 10; w += sec) S("rect", {x:X(w), y:30, width:Math.min(sec, 10 - w) * 82, height:120, style:`fill:var(--ids);fill-opacity:${(w / sec) % 2 ? .03 : .07}`}, svg);
   T(40, 20, "trait = 1 SYN · rouge = rejeté · losange = alerte EVE", "ts", svg);
   let nd = 0, na = 0;
   res.forEach(r => { if (r.drop) nd++; if (r.alert) na++; S("line", {x1:X(r.t), y1:Y(r.s) - 8, x2:X(r.t), y2:Y(r.s) + 8, style:`stroke:${r.drop ? "var(--bad)" : "var(--muted)"};stroke-width:${r.drop ? 1.6 : 1}`}, svg); if (r.alert) S("path", {d:`M${X(r.t)} ${Y(r.s) - 18} l5 5 l-5 5 l-5 -5 z`, style:"fill:var(--ids)"}, svg); });
   const legit = pk === "legit" || pk === "rafale";
   q("kpi").innerHTML = `<table><thead><tr><th>SYN envoyés</th><th>alertes EVE</th><th>SYN rejetés</th><th>SYN arrivés à pf</th></tr></thead><tbody><tr><td>${res.length}</td><td>${na}</td><td>${nd}</td><td>${res.length - nd}</td></tr></tbody></table>`;
   let c = "";
   if (legit && (na || nd)) c = `<b class="p-bad">Faux positif.</b> Trafic légitime ${nd ? `et ${nd} connexions de vrais clients rejetées : la disponibilité baisse` : "signalé à tort"}. Relevez count, ou limitez la règle aux ports non publiés.`;
   else if (legit) c = `<b class="p-ok">Aucune alerte sur ce trafic légitime.</b>`;
   else if (!na && !nd) c = `<b class="p-bad">Faux négatif.</b> ${pk === "dist" && tr === "by_src" ? "Chaque source reste sous le seuil : essayez by_dst." : "L'attaque reste sous le seuil : un scan lent n'est pas vu."}`;
   else { const f = res.find(r => r.alert || r.drop); c = `<b class="p-ok">Détecté</b> à t = ${f.t.toFixed(2)} s (c'est le temps de détection que calculera le rôle 6). ${nd ? `${res.length - nd} SYN sont passés avant le seuil${mec === "rf" ? " ; ensuite la source est rejetée pendant " + to + " s" : ""}.` : "Mécanisme d'alerte seulement : rien n'est rejeté."}`; }
   if (mec === "rf" && to >= 30) c += ` <b class="p-warn">Le timeout (${to} s) dépasse la pause de 30 s entre deux exécutions : l'exécution suivante démarrerait bloquée.</b>`;
   q("cap").innerHTML = `<span class="k">Lecture</span><span>${c}</span>`;
  }
  host.addEventListener("input", run); host.addEventListener("change", run); run();
 }
};

/* ---------- faux positifs pendant la boucle de réglage ---------- */
const FAM = [["Scan SYN local (9000001)",[0,0,0,0]],["SQLi local (9000010)",[0,0,0,0]],["Fragments local (9000030)",[3,1.5,.5,.5]],["ET SCAN (générique)",[6,4,3,3]],["ET POLICY (générique)",[9,7,7,7]]];
G.schemas["fp-tuning"] = {
 q:"Après chaque passe de réglage, quelles familles respectent « au plus 1 faux positif par heure » et peuvent passer en drop ?",
 custom:true,
 mount(host){
  host.innerHTML = `<div class="sv"><svg viewBox="0 0 900 260" role="img" aria-label="Faux positifs par heure selon les passes de réglage" style="min-width:700px"></svg></div><div class="side" style="display:grid;gap:6px"><p class="eyebrow">Passer en drop (après la 4e passe)</p><div data-x="ck" style="display:flex;flex-wrap:wrap;gap:6px 16px"></div></div><div class="cap" data-x="cap" aria-live="polite"></div><p class="ts" style="padding:0 14px 12px;font-size:12px">Valeurs d'exemple, pour comprendre la lecture. Les vraies viennent de fp_par_sid.csv (rôle 6) après chaque passe de 2 h de trafic légitime seul.</p>`;
  const svg = host.querySelector("svg"), cols = ["var(--r1)","var(--r2)","var(--ids)","var(--r5)","var(--r6)"], sel = {0:true, 1:true, 2:true};
  host.querySelector('[data-x="ck"]').innerHTML = FAM.map((f, i) => `<label style="font-size:14px;display:flex;gap:6px;align-items:center"><input type="checkbox" data-i="${i}" ${sel[i] ? "checked" : ""}><span style="color:${cols[i]};font-weight:600">${f[0]}</span></label>`).join("");
  function draw(){
   svg.innerHTML = "";
   const X = i => 90 + i * 240, Y = v => 220 - v * 18;
   for (let v = 0; v <= 10; v += 2){ S("line", {x1:80, y1:Y(v), x2:820, y2:Y(v), style:"stroke:var(--line);stroke-width:1"}, svg); T(72, Y(v) + 4, String(v), "ts", svg, "end"); }
   T(20, 24, "FP / h", "tl", svg);
   S("line", {x1:80, y1:Y(1), x2:820, y2:Y(1), style:"stroke:var(--bad);stroke-width:2;stroke-dasharray:6 4"}, svg); T(824, Y(1) + 4, "1 / h", "tl", svg).setAttribute("style", "fill:var(--bad)");
   [0,1,2,3].forEach(i => T(X(i), 244, `passe ${i + 1}`, "tl", svg, "middle"));
   FAM.forEach((f, k) => { const pts = f[1].map((v, i) => [X(i), Y(v) + (k - 2) * 1.5]); S("polyline", {points:pts.map(p => p.join(",")).join(" "), style:`fill:none;stroke:${cols[k]};stroke-width:${sel[k] ? 3 : 1.5};opacity:${sel[k] ? 1 : .55}`}, svg); pts.forEach(p => S("circle", {cx:p[0], cy:p[1], r:4, style:`fill:${cols[k]}`}, svg)); });
   const tot = FAM.reduce((a, f, k) => a + (sel[k] ? f[1][3] : 0), 0), bad = FAM.filter((f, k) => sel[k] && f[1][3] > 1).map(f => f[0]);
   host.querySelector('[data-x="cap"]').innerHTML = `<span class="k">Familles en drop</span><span>Total : <b class="${tot <= 1 ? "p-ok" : "p-bad"}">${tot} FP/h</b> (critère : ≤ 1). ${bad.length ? "À laisser en alert : " + bad.join(", ") + "." : "Toutes les familles cochées respectent le critère."} Les familles laissées en alert continuent de compter dans l'indicateur « faux positifs » : signalez-les dans le rapport.</span>`;
  }
  host.addEventListener("change", e => { const c = e.target.closest("[data-i]"); if (c){ sel[c.dataset.i] = c.checked; draw(); } });
  draw();
 }
};

/* ---------- panne P-01 ---------- */
G.schemas["p01-panne"] = {
 q:"Que mesure-t-on pendant P-01, et à quoi ressemblerait chacune des deux hypothèses du rapport ?",
 vb:[1000, 290], minw:800, ab:true, abLabels:["Hypothèse 1 : passe sans inspection","Hypothèse 2 : plus rien ne passe"],
 draw(p){
  const X = s => 80 + s * 14.5;
  S("line", {x1:80, y1:200, x2:X(60), y2:200, style:"stroke:var(--muted)"}, p);
  for (let s = 0; s <= 60; s += 10){ S("line", {x1:X(s), y1:200, x2:X(s), y2:206, style:"stroke:var(--muted)"}, p); T(X(s), 222, `t0 + ${s} s`, "ts", p, "middle"); }
  S("rect", {x:X(10), y:40, width:X(40) - X(10), height:160, style:"fill:var(--warn);fill-opacity:.1"}, p); T(X(25), 36, "Suricata arrêté", "tl", p, "middle");
  T(10, 120, "sonde", "tl", p); T(10, 136, "1 req/s", "ts", p);
  T(80, 254, "Chaque barre = une requête de la sonde : haute et verte si servie, basse et rouge si échec. Illustration : le vrai résultat est mesuré, pas supposé (section 8.5).", "ts", p);
 },
 steps(ctx){
  const X = s => 80 + s * 14.5, H1 = ctx.st === "A";
  const ok = s => s < 10 ? true : s < 40 ? (H1 ? (s % 3 !== 0) : false) : s < 43 ? false : true;
  const bars = upto => p => { for (let s = 0; s < upto; s++){ const o = ok(s); S("rect", {x:X(s) + 2, y:o ? 90 : 160, width:10, height:o ? 110 : 40, rx:2, style:`fill:var(--${o ? "ok" : "bad"})`}, p); } };
  const h = ["période","requêtes servies","alertes EVE"], av = (a, b) => { let n = 0; for (let s = a; s < b; s++) if (ok(s)) n++; return `${n} / ${b - a}`; };
  return [
   {cap:"De t0 à t0 + 10 s : le SYN flood est en cours, Suricata le rejette. La sonde (client-ext) est servie normalement.", fx:bars(10), panel:{title:"Mesures (exemple)", head:h, rows:[["avant la panne", av(0,10), "oui"]], mark:0}},
   {cap:"t0 + 10 s : l'orchestrateur arrête Suricata sur OPNsense. Avec netmap, c'est le chemin même des paquets qui est en jeu.", fx:p => { bars(10)(p); S("line", {x1:X(10), y1:40, x2:X(10), y2:200, style:"stroke:var(--fg);stroke-width:2"}, p); }, panel:{title:"Mesures (exemple)", head:h, rows:[["avant la panne", av(0,10), "oui"]]}},
   {cap:H1 ? "Hypothèse 1 : l'interface laisse passer sans inspection. Le flood atteint pf et nginx comme en état A : la sonde est servie par intermittence." : "Hypothèse 2 : l'interface ne transmet plus rien. Le flood ne fait plus de dégâts… mais le service légitime est coupé : disponibilité nulle.", fx:bars(40), panel:{title:"Mesures (exemple)", head:h, rows:[["avant la panne", av(0,10), "oui"],["pendant la panne", av(10,40), "non"]], mark:1}},
   {cap:"t0 + 40 s : redémarrage. Suricata recharge ses règles (quelques secondes), puis rejette de nouveau le flood. Le <b>temps de reprise</b> est le délai jusqu'à la première requête de la sonde de nouveau servie.", fx:bars(60), panel:{title:"Mesures (exemple)", head:h, rows:[["avant la panne", av(0,10), "oui"],["pendant la panne", av(10,40), "non"],["après le redémarrage", av(40,60), "oui"],["temps de reprise", "≈ 3 s (exemple)", ""]], mark:3}}
  ];
 },
 fin:ctx => ctx.st === "A" ? "Hypothèse 1 : la sécurité tombe (plus d'inspection), le service tient en partie. C'est un fonctionnement « ouvert en cas de panne »." : "Hypothèse 2 : le service tombe avec l'IPS. C'est un fonctionnement « fermé en cas de panne ». Le rapport mesure lequel des deux se produit sur netmap avec les cartes em de VMware."
};
})();
