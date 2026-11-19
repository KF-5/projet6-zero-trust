/* Schémas du rôle 6 : pipeline des journaux, fenêtre de corrélation et TTD, maquette des graphiques A vs B. */
(function(){
"use strict";
const {S, T, D, $$} = G;

/* ---------- pipeline syslog → parseur → events.csv → corrélation ---------- */
const BOX = [["opn",90,"OPNsense","filterlog, eve.json"],["sys",270,"rsyslog","collecteur :514"],["file",450,"fichier brut","/var/log/remote/"],["parse",630,"parse.py","C9"],["ev",810,"events.csv","schéma commun"]];
G.schemas["pipeline-logs"] = {
 q:"Comment une ligne écrite par pf devient-elle une ligne de events.csv, puis un indicateur ?",
 vb:[1000, 260], minw:800,
 draw(p){
  BOX.forEach(([k, x, a, b]) => D.node(p, x, 90, 150, 50, a, b, k === "parse" || k === "ev" ? "acc" : "", k));
  for (let i = 0; i < BOX.length - 1; i++) D.arrow(p, BOX[i][1] + 75, 90, BOX[i + 1][1] - 77, 90);
  D.node(p, 810, 200, 150, 44, "correle.py (C11)", "+ runs.csv, sla.csv", "acc", "cor"); D.arrow(p, 810, 115, 810, 176);
  D.node(p, 560, 200, 170, 44, "indicateurs.csv", "détection, TTD, FP/h…", "", "ind"); D.arrow(p, 735, 200, 647, 200);
  T(90, 30, "UDP ou TCP 514 sur MGMT (VMnet9)", "ts", p);
 },
 steps(){
  const h = ["étape","représentation de l'événement"];
  const raw = "82,,,fae559338f65e11c53669fc3642c93c2,vlan0.11,match,block,in,4,0x0,,64,23817,0,DF,6,tcp,60,10.10.0.12,10.10.0.70,49822,5432,0,S,…";
  return [
   {cap:"pf bloque le SYN de client-03 (S-05, état B) et écrit une ligne filterlog : une suite de champs séparés par des virgules (module 5).", hl:["opn"], panel:{title:"Ce qu'on a en main", head:h, rows:[["pf (OPNsense)", raw]], mark:0}},
   {cap:"OPNsense l'envoie en syslog vers 10.99.0.10:514, par le réseau MGMT. En UDP, un message peut se perdre pendant un flood (écart E13) : on relira aussi les fichiers d'OPNsense après chaque campagne.", path:[[165,90],[195,90]], pkt:"mgmt", hl:["sys"], panel:{title:"Ce qu'on a en main", head:h, rows:[["syslog (réseau)", "<134>1 2026-11-25T10:03:02.420+01:00 opnsense filterlog 412 - - " + raw]], mark:0}},
   {cap:"rsyslog l'écrit dans un fichier, avec un horodatage complet (date, heure à la microseconde, fuseau). Le fichier brut est conservé : c'est la preuve.", path:[[345,90],[375,90]], pkt:"acc", hl:["file"], panel:{title:"Ce qu'on a en main", head:h, rows:[["fichier brut", "2026-11-25T10:03:02.420000+01:00 opnsense filterlog[412]: " + raw]], mark:0}},
   {cap:"parse.py reconnaît filterlog, lit les champs par position (action = 7e, IP source = 19e…), convertit l'heure en <b>UTC</b> et écrit une ligne au schéma commun (section 7.9).", path:[[525,90],[555,90]], pkt:"acc", hl:["parse","ev"], panel:{title:"Ce qu'on a en main", head:["ts_utc","source","src_ip","dst_ip","dst_port","proto","action","sid","message"], rows:[["2026-11-25T09:03:02.420Z","pf","10.10.0.12","10.10.0.70","5432","tcp","block","","règle 82 (fae5…)"]], mark:0}},
   {cap:"correle.py rapproche l'exécution B-S05-03 de runs.csv des événements du même attaquant dans [t0, t1 + 10 s]. Seules les alertes Suricata comptent comme détection : ici l'alerte 9000020 du même instant. La ligne pf reste une preuve pour le rapport ; « bloqué » se juge sur l'effet réel (sortie de psql, PCAP côté base).", path:[[810,115],[810,176],[735,200],[647,200]], pkt:"acc", hl:["cor","ind"], panel:{title:"Ce qu'on a en main", head:["scenario","etat","detecte","ttd_ms","bloque"], rows:[["S-05","B","oui","418","oui"]], mark:0}}
  ];
 },
 fin:() => "Une ligne brute devient une ligne de events.csv (UTC, champs communs), puis une ligne de résultat par exécution. Chaque étape est rejouable à partir des fichiers bruts archivés."
};

/* ---------- fenêtre [t0, t1 + 10 s] et TTD ---------- */
const RUNS = [{id:"B-S03-01", t0:20, t1:35},{id:"B-S03-02", t0:95, t1:110},{id:"B-S03-03", t0:170, t1:185}];
const EV = [
 {t:27.0, src:"10.20.0.10", dst:"10.20.0.70", sid:9000010, k:"atk", m:"SQLi relayée par nginx (em1)"},
 {t:21.4, src:"203.0.113.66", dst:"203.0.113.1", sid:2013028, k:"atk", m:"nikto vu sur em0"},
 {t:100.5, src:"10.20.0.10", dst:"10.20.0.70", sid:9000010, k:"atk", m:"SQLi relayée"},
 {t:104.0, src:"203.0.113.10", dst:"10.20.0.10", sid:2221010, k:"fp", m:"alerte sur la sonde client-ext"},
 {t:60.0, src:"10.10.0.10", dst:"10.20.0.40", sid:2027758, k:"fp", m:"alerte sur le DNS de client-01"},
 {t:193.0, src:"10.20.0.10", dst:"10.20.0.70", sid:9000010, k:"atk", m:"SQLi relayée, tardive"}
];
G.schemas["fenetre-ttd"] = {
 q:"Quelles alertes comptent pour une exécution, comment se calcule le temps de détection, et qu'est-ce qui fausse le résultat ?",
 custom:true,
 mount(host){
  host.innerHTML = `<div style="padding:12px 14px;display:flex;flex-wrap:wrap;gap:10px 16px;align-items:center;font-size:14px"><label>DELTA <input type="range" data-x="d" min="0" max="20" value="10"> <span class="mono" data-x="dv">10 s</span></label><label>Décalage d'horloge d'OPNsense <input type="range" data-x="k" min="-8" max="8" value="0"> <span class="mono" data-x="kv">0 s</span></label><label><input type="checkbox" data-x="c"> Filtrer aussi sur la cible (pseudo-code d'origine, écart E08)</label></div><div class="sv"><svg viewBox="0 0 900 210" role="img" aria-label="Fenêtres et alertes" style="min-width:700px"></svg></div><div class="side" style="display:grid"><div class="tbl" data-x="t"></div></div><div class="cap" data-x="cap" aria-live="polite"></div><p class="ts" style="padding:0 14px 12px;font-size:12px">Trois exécutions de S-03 en état B, données d'exemple. Attaquant 203.0.113.66 ; relais déclaré pour S-03 : nginx (10.20.0.10) → API (10.20.0.70).</p>`;
  const q = s => host.querySelector(`[data-x="${s}"]`), svg = host.querySelector("svg");
  function run(){
   const D0 = +q("d").value, sk = +q("k").value, cible = q("c").checked;
   q("dv").textContent = D0 + " s"; q("kv").textContent = (sk > 0 ? "+" : "") + sk + " s";
   const ev = EV.map(e => ({...e, ts:e.t + sk}));
   const rel = e => [e.src, e.dst].includes("203.0.113.66") || (e.src === "10.20.0.10" && e.dst === "10.20.0.70") || (cible && [e.src, e.dst].some(x => ["203.0.113.1","10.20.0.10"].includes(x)));
   const win = e => RUNS.find(r => e.ts >= r.t0 && e.ts <= r.t1 + D0);
   const res = RUNS.map(r => { const al = ev.filter(e => e.ts >= r.t0 && e.ts <= r.t1 + D0 && rel(e)); return {r, ttd:al.length ? Math.min(...al.map(e => e.ts)) - r.t0 : null, n:al.length}; });
   const fp = ev.filter(e => !win(e)).length;
   svg.innerHTML = ""; const X = t => 30 + t * 4.2;
   S("line", {x1:30, y1:170, x2:870, y2:170, style:"stroke:var(--line)"}, svg);
   for (let t = 0; t <= 200; t += 20){ S("line", {x1:X(t), y1:170, x2:X(t), y2:175, style:"stroke:var(--muted)"}, svg); T(X(t), 190, String(t), "ts", svg, "middle"); }
   T(870, 205, "secondes", "ts", svg, "end");
   RUNS.forEach(r => { S("rect", {x:X(r.t0), y:30, width:(r.t1 - r.t0) * 4.2, height:130, style:"fill:var(--bad);fill-opacity:.12"}, svg); S("rect", {x:X(r.t1), y:30, width:D0 * 4.2, height:130, style:"fill:var(--warn);fill-opacity:.14"}, svg); T(X(r.t0) + 3, 44, r.id, "ts", svg); });
   ev.forEach((e, i) => { const w = win(e), c = w && rel(e), y = 60 + (i % 4) * 26; S("circle", {cx:X(e.ts), cy:y, r:6, style:`fill:${c ? "var(--ok)" : (w ? "var(--muted)" : "var(--ids)")};stroke:var(--surface);stroke-width:2`}, svg); T(X(e.ts) + 9, y + 4, String(e.sid), "ts", svg); });
   T(30, 20, "rouge : [t0, t1] · ambre : + DELTA · vert : alerte retenue · gris : dans la fenêtre mais écartée · orange : compte comme faux positif", "ts", svg);
   q("t").innerHTML = `<table><thead><tr><th>exécution</th><th>alertes retenues</th><th>détecté</th><th>TTD</th></tr></thead><tbody>${res.map(x => `<tr><td>${x.r.id}</td><td>${x.n}</td><td>${x.n ? "oui" : "non"}</td><td>${x.ttd == null ? "—" : Math.round(x.ttd * 1000) + " ms"}</td></tr>`).join("")}<tr><td colspan="4">faux positifs comptés (hors fenêtres) : <b>${fp}</b> · vrais faux positifs dans les données : 2</td></tr></tbody></table>`;
   const notes = [];
   if (cible && ev.some(e => e.k === "fp" && win(e) && rel(e))) notes.push(`<b class="p-bad">Détection gonflée :</b> une alerte provoquée par la sonde légitime vers nginx est retenue, parce que le filtre accepte la cible.`);
   if (sk) notes.push(`<b class="p-warn">Horloge décalée de ${sk} s :</b> tous les TTD sont faux${sk < 0 ? " ; une alerte peut même sortir de la fenêtre" : ""}. Contrôler NTP avant chaque campagne (étape 1).`);
   if (ev.some(e => e.k === "atk" && !win(e))) notes.push(`L'alerte tardive du 3e run tombe hors fenêtre : elle est comptée comme <b>faux positif</b> et le run est « non détecté ». Avec DELTA = ${D0} s, c'est la règle du rapport ; signalez ces cas.`);
   q("cap").innerHTML = `<span class="k">Lecture</span><span>${notes.join(" ") || "Réglage cohérent : seules les alertes de l'attaquant (ou du relais déclaré) sont retenues."}</span>`;
  }
  host.addEventListener("input", run); run();
 }
};

/* ---------- maquette des graphiques A vs B ---------- */
G.schemas["graphes-maquette"] = {
 q:"À quoi ressembleront les graphiques du rapport final, et comment les lire ?",
 custom:true,
 mount(host){
  host.innerHTML = `<div style="padding:10px 14px"><span class="pill p-warn">MAQUETTE · valeurs fictives : à remplacer par resultats/synthese/</span></div><div class="sv"><svg viewBox="0 0 960 300" role="img" aria-label="Maquette des graphiques" style="min-width:760px"></svg></div><div class="cap"><span class="k">Lecture</span><span>Gauche : taux de blocage par scénario, A (gris) contre B (couleur). Droite : disponibilité de la sonde pendant S-02 et latence médiane / p95 en A, B sans IPS, B avec IPS. Un graphique par question du sujet, axes à zéro, valeurs écrites au-dessus des barres, N = 10 rappelé dans le titre.</span></div>`;
  const svg = host.querySelector("svg");
  const SC = ["S-01","S-02","S-03","S-04","S-05","S-06"], A = [0,0.2,0,0,0,0], B = [0.9,1,1,1,1,0];
  const X0 = 50, Y0 = 250, H = 180, bw = 22;
  T(X0, 24, "Taux de blocage par scénario (N = 10)", "tl", svg);
  [0,.5,1].forEach(v => { S("line", {x1:X0, y1:Y0 - v * H, x2:470, y2:Y0 - v * H, style:"stroke:var(--line)"}, svg); T(X0 - 6, Y0 - v * H + 4, `${v * 100} %`, "ts", svg, "end"); });
  SC.forEach((s, i) => { const x = X0 + 14 + i * 68;
   [[A[i],"var(--muted)",0],[B[i],"var(--acc)",1]].forEach(([v, c, j]) => { S("rect", {x:x + j * (bw + 3), y:Y0 - v * H, width:bw, height:Math.max(1, v * H), style:`fill:${c}`}, svg); T(x + j * (bw + 3) + bw / 2, Y0 - v * H - 5, `${Math.round(v * 10)}/10`, "ts", svg, "middle"); });
   T(x + bw, Y0 + 16, s, "tl", svg, "middle"); });
  T(X0 + 300, Y0 + 34, "S-06 : pas d'objectif de blocage (détection seulement)", "ts", svg);
  const X1 = 540; T(X1, 24, "Pendant S-02 : disponibilité de la sonde", "tl", svg);
  [["A",.71,"var(--muted)"],["B",.995,"var(--acc)"]].forEach(([l, v, c], i) => { const y = 50 + i * 34; S("rect", {x:X1 + 30, y, width:v * 340, height:22, style:`fill:${c}`}, svg); T(X1, y + 15, l, "tl", svg); T(X1 + 36 + v * 340, y + 15, `${(v * 100).toFixed(1)} %`, "ts", svg); });
  S("line", {x1:X1 + 30 + .99 * 340, y1:44, x2:X1 + 30 + .99 * 340, y2:116, style:"stroke:var(--bad);stroke-dasharray:4 3"}, svg); T(X1 + 30 + .99 * 340, 128, "99 % (ENF4)", "ts", svg, "middle");
  T(X1, 160, "Latence HTTPS : médiane et p95 (ms)", "tl", svg);
  [["A",42,61],["B sans IPS",43,63],["B avec IPS",47,72]].forEach(([l, m, p], i) => { const y = 176 + i * 34; S("rect", {x:X1 + 90, y, width:m * 3, height:12, style:"fill:var(--acc)"}, svg); S("rect", {x:X1 + 90, y:y + 13, width:p * 3, height:8, style:"fill:var(--acc);fill-opacity:.45"}, svg); T(X1, y + 12, l, "ts", svg); T(X1 + 96 + p * 3, y + 14, `${m} / ${p}`, "ts", svg); });
 }
};
})();
