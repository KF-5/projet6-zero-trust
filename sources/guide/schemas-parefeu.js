/* Schémas du rôle 2 : étiquette 802.1Q sur le trunk, NAT pas à pas, table d'états, éditeur de matrice. */
(function(){
"use strict";
const {S, T, D, $, $$, esc} = G;

/* ---------- trame sur le trunk : pose et retrait de l'étiquette ---------- */
const MAC = {c01:"02:42:0a:0a:00:0a", opn:"bc:24:11:5e:00:01", ng:"02:42:0a:14:00:0a"};
function frame(p, f){
  /* f = {dst, src, vid (null = pas d'étiquette), ip, port, note} */
  const y = 292; let x = 30;
  T(30, 282, "Trame à cet endroit :", "tl", p);
  const fld = (w, a, b, cls) => { S("rect", {x, y, width:w, height:46, rx:6, class:"nd " + (cls || "")}, p); T(x + 8, y + 18, a, "ts", p); T(x + 8, y + 35, b, "tl", p); x += w + 6; };
  fld(170, "MAC destination", f.dst); fld(170, "MAC source", f.src);
  if (f.vid != null){ S("rect", {x, y, width:120, height:46, rx:6, style:"fill:var(--trunk);fill-opacity:.16;stroke:var(--trunk);stroke-width:2"}, p); T(x + 8, y + 18, "802.1Q (4 octets)", "ts", p); T(x + 8, y + 35, `VID ${f.vid}`, "tl f-trunk", p); x += 126; }
  else { S("rect", {x, y, width:120, height:46, rx:6, style:"fill:none;stroke:var(--line);stroke-dasharray:4 3"}, p); T(x + 8, y + 28, "pas d'étiquette", "ts", p); x += 126; }
  fld(260, "IPv4 (inchangé)", f.ip); fld(150, "TCP", f.port);
  if (f.note) T(30, 362, f.note, "ts", p);
}
G.schemas["trunk-tag"] = {
 q:"Où l'étiquette VLAN est-elle posée, et où est-elle retirée, quand client-01 ouvre le site interne ?",
 vb:[1000, 375], minw:780, ab:true, ab0:"B",
 draw(p, ctx){
  const v1 = ctx.st === "A" ? 10 : 11, v2 = ctx.st === "A" ? 20 : 21;
  D.zone(p, 20, 30, 400, 220, "lan", "VM Hôte Docker", "ens33 = ens33 du rapport (trunk)");
  D.zone(p, 620, 30, 360, 220, "trunk", "VM OPNsense");
  /* traits d'abord : les boîtes passent par-dessus */
  D.wire(p, [[175,80],[235,80]], "s-lan"); D.wire(p, [[175,190],[235,190]], "s-dmz");
  D.wire(p, [[325,80],[352,120]], "s-lan"); D.wire(p, [[325,190],[352,150]], "s-dmz");
  S("line", {x1:408, y1:135, x2:640, y2:135, class:"wr s-trunk", "stroke-width":5}, p); T(524, 125, "p6-trunk · trunk", "ts f-trunk", p, "middle");
  D.wire(p, [[703,135],[722,80]], "s-acc"); D.wire(p, [[703,135],[722,190]], "s-acc"); D.wire(p, [[878,80],[930,115]], "s-acc"); D.wire(p, [[878,190],[930,155]], "s-acc");
  D.node(p, 110, 80, 130, 46, "client-01", "10.10.0.10", "", "c01").querySelector("rect").setAttribute("style", "stroke:var(--lan)");
  D.node(p, 110, 190, 130, 46, "nginx", "10.20.0.10", "", "ng").querySelector("rect").setAttribute("style", "stroke:var(--dmz)");
  D.node(p, 280, 80, 90, 36, "ens33." + v1, "", "", "s1"); D.node(p, 280, 190, 90, 36, "ens33." + v2, "", "", "s2");
  D.node(p, 380, 135, 56, 36, "ens33", "", "", "par");
  D.node(p, 670, 135, 66, 36, "em1", "", "acc", "vt");
  D.node(p, 800, 80, 156, 42, "VLAN " + v1, ctx.st === "A" ? "LAN · 10.10.0.1/24" : "LAN-USERS · 10.10.0.1/26", "acc", "i1");
  D.node(p, 800, 190, 156, 42, "VLAN " + v2, ctx.st === "A" ? "DMZ · 10.20.0.1/24" : "DMZ-WEB · 10.20.0.1/27", "acc", "i2");
  const g = S("g", {"data-k":"pf"}, p); S("rect", {x:900, y:115, width:60, height:40, rx:7, class:"c-acc", "fill-opacity":1}, g); T(930, 140, "pf", "tb f-on", g, "middle");
 },
 steps(ctx){
  const A = ctx.st === "A", v1 = A ? 10 : 11, v2 = A ? 20 : 21, rule = A ? "FA4" : "F05";
  const ip = "10.10.0.10 → 10.20.0.10";
  const f = (dst, src, vid, note) => ({fx:p => frame(p, {dst, src, vid, ip, port:"51544 → 443", note})});
  return [
   Object.assign({cap:`client-01 envoie un SYN vers 10.20.0.10:443. nginx n'est pas dans son réseau : la trame est adressée à la MAC de sa passerelle 10.10.0.1. Le conteneur ne connaît pas les VLAN : <b>la trame part sans étiquette</b>.`, path:[[175,80],[235,80]], pkt:"lan", hl:["c01"]}, f(MAC.opn, MAC.c01, null, "Dans le conteneur, la trame est ordinaire.")),
   Object.assign({cap:`En sortant par la sous-interface <code>ens33.${v1}</code>, le noyau de l'hôte Docker <b>pose l'étiquette VID ${v1}</b> (4 octets insérés après les adresses MAC).`, path:[[235,80],[325,80],[352,120]], pkt:"trunk", hl:["s1","par"]}, f(MAC.opn, MAC.c01, v1, "La trame a grandi de 4 octets. L'adresse IP ne change pas.")),
   Object.assign({cap:`Sur p6-trunk, le segment LAN VMware transporte la trame <b>avec son étiquette</b> jusqu'à em1. C'est ce que montre <code>tcpdump -nei em1 vlan ${v1}</code> sur OPNsense.`, path:[[408,135],[637,135]], pkt:"trunk", hl:["vt"]}, f(MAC.opn, MAC.c01, v1, `Ligne tcpdump : « ethertype 802.1Q (0x8100) … vlan ${v1}, p 0, ethertype IPv4 … 10.10.0.10.51544 > 10.20.0.10.443: Flags [S] »`)),
   Object.assign({cap:`OPNsense lit le VID ${v1}, <b>retire l'étiquette</b> et remet le paquet à la sous-interface VLAN ${v1}. pf évalue les règles de <b>cette</b> interface d'entrée : ${rule} correspond, état créé.`, path:[[703,135],[722,80]], pkt:"acc", hl:["i1","pf"], pills:[[800,48,"ok",`${rule} : pass`]]}, f(MAC.opn, MAC.c01, null, "Dans OPNsense, le paquet est associé à l'interface VLAN, plus à une étiquette.")),
   Object.assign({cap:`OPNsense route vers le réseau 10.20.0.0 et fabrique une <b>nouvelle trame</b> : MAC source = OPNsense, MAC destination = nginx, TTL − 1.`, path:[[878,80],[930,115],[930,155],[878,190]], pkt:"acc", hl:["pf","i2"]}, f(MAC.ng, MAC.opn, null, "Les adresses MAC ont changé, les adresses IP non.")),
   Object.assign({cap:`En sortant par em1, la trame reçoit l'étiquette <b>VID ${v2}</b>, traverse p6-trunk et entre dans l'hôte Docker par ens33.`, path:[[722,190],[703,135],[408,135]], pkt:"trunk", hl:["vt","par"]}, f(MAC.ng, MAC.opn, v2, "Même trunk, autre étiquette : c'est elle qui sépare les segments.")),
   Object.assign({cap:`L'hôte Docker lit le VID ${v2}, <b>retire l'étiquette</b> et remet la trame au réseau macvlan <code>ens33.${v2}</code> : nginx reçoit une trame ordinaire.`, path:[[352,150],[325,190],[175,190]], pkt:"dmz", hl:["s2","ng"]}, f(MAC.ng, MAC.opn, null, "nginx ne voit jamais d'étiquette."))
  ];
 },
 fin:ctx => `L'étiquette n'existe que sur le trunk (p6-trunk) : posée en sortant de ens33.${ctx.st === "A" ? 10 : 11} et de em1, retirée en entrant dans OPNsense et dans l'hôte Docker. pf décide sur l'interface VLAN d'entrée (${ctx.st === "A" ? "FA4" : "F05"}).`,
 legend:[["var(--trunk)","trame étiquetée"],["var(--lan)","côté LAN"],["var(--dmz)","côté DMZ"],["var(--acc)","OPNsense"]]
};

/* ---------- NAT pas à pas ---------- */
G.schemas["nat-steps"] = {
 q:"Que deviennent les adresses d'un paquet de client-ext vers 203.0.113.1:443, à l'aller et au retour ?",
 vb:[1000, 250], minw:760, ab:true, ab0:"B",
 draw(p){
  D.zone(p, 20, 40, 200, 160, "wan", "WAN · p6-wan"); D.zone(p, 780, 40, 200, 160, "dmz", "DMZ-WEB / DMZ");
  D.wire(p, [[170,120],[830,120]]);
  D.node(p, 110, 120, 130, 48, "client-ext", "203.0.113.10", "", "ce");
  D.node(p, 880, 120, 130, 48, "nginx", "10.20.0.10", "", "ng");
  S("rect", {x:300, y:85, width:400, height:70, rx:10, class:"nd acc"}, p);
  T(330, 178, "em0 · 203.0.113.1", "ts", p, "middle"); T(670, 178, "em1 · 10.20.0.1", "ts", p, "middle");
  const g = S("g", {"data-k":"pf"}, p); S("rect", {x:470, y:100, width:60, height:40, rx:7, class:"c-acc", "fill-opacity":1}, g); T(500, 125, "pf", "tb f-on", g, "middle");
  T(500, 210, "OPNsense", "tn", p, "middle");
 },
 steps(ctx){
  const R = ctx.st === "A" ? "FA1" : "F01", h = ["proto","interne","(externe)","client","état"];
  const st1 = [["tcp","10.20.0.10:443","203.0.113.1:443","203.0.113.10:50218","en ouverture"]], st2 = [["tcp","10.20.0.10:443","203.0.113.1:443","203.0.113.10:50218","établi"]];
  return [
   {cap:"client-ext envoie le SYN à <code>203.0.113.10:50218 → 203.0.113.1:443</code> : la seule adresse publique qu'il connaît.", path:[[175,120],[330,120]], pkt:"wan", hl:["ce"], panel:{title:"Table d'états de pf (vue simplifiée de pfctl -ss)", head:h, rows:[]}},
   {cap:`pf applique d'abord la <b>redirection</b> (Destination NAT) : la destination devient <code>10.20.0.10:443</code>. Puis il lit les règles WAN, qui visent l'adresse <b>interne</b> : ${R} correspond. Il crée un état qui garde les deux versions de l'adresse.${ctx.st === "B" ? " (En état B, Suricata a inspecté le paquet sur em0 juste avant.)" : ""}`, path:[[330,120],[470,120]], pkt:"acc", hl:["pf"], pills:[[500,70,"acc",`rdr puis ${R} : pass`]], panel:{title:"Table d'états de pf", head:h, rows:st1, mark:0}},
   {cap:"Le paquet sort sur em1 vers nginx : <code>203.0.113.10:50218 → 10.20.0.10:443</code>. nginx voit la vraie adresse du client (pas de NAT de source).", path:[[530,120],[815,120]], pkt:"dmz", hl:["ng"], panel:{title:"Table d'états de pf", head:h, rows:st1}},
   {cap:"nginx répond depuis <code>10.20.0.10:443</code> vers <code>203.0.113.10:50218</code>, par sa passerelle. pf trouve l'état : <b>aucune règle relue</b>.", path:[[815,128],[530,128]], pkt:"dmz", hl:["pf"], pills:[[500,70,"ok","état trouvé"]], panel:{title:"Table d'états de pf", head:h, rows:st1, mark:0}},
   {cap:"pf <b>retraduit la source</b> : <code>10.20.0.10:443 → 203.0.113.1:443</code>. client-ext reçoit une réponse de l'adresse qu'il a appelée ; la poignée de main se termine.", path:[[470,128],[175,128]], pkt:"wan", hl:["ce"], panel:{title:"Table d'états de pf", head:h, rows:st2, mark:0}}
  ];
 },
 fin:ctx => `Une redirection, une règle (${ctx.st === "A" ? "FA1" : "F01"}) qui vise l'adresse interne, un état qui mémorise les deux adresses. Le NAT ne protège rien : ce sont les règles qui décident.`
};

/* ---------- table d'états pendant une poignée de main ---------- */
G.schemas["state-table"] = {
 q:"Pourquoi pf ne relit-il les règles qu'au premier paquet, et pourquoi la connexion client-03 → base ne crée-t-elle aucun état ?",
 vb:[1000, 240], minw:760, ab:true, ab0:"B",
 draw(p, ctx){
  D.node(p, 120, 90, 140, 48, "client-01", "10.10.0.10", "", "c01");
  D.node(p, 880, 90, 140, 48, "nginx", "10.20.0.10", "", "ng");
  D.node(p, 120, 190, 140, 48, "client-03", "10.10.0.12", "", "c03");
  D.node(p, 880, 190, 140, 48, "PostgreSQL", "10.10.0.70", "", "pg");
  const g = S("g", {"data-k":"pf"}, p); S("rect", {x:440, y:60, width:120, height:160, rx:10, class:"nd acc"}, g); T(500, 84, "OPNsense", "tn", g, "middle"); S("rect", {x:470, y:120, width:60, height:40, rx:7, class:"c-acc", "fill-opacity":1}, g); T(500, 145, "pf", "tb f-on", g, "middle");
  D.wire(p, [[190,90],[440,90]]); D.wire(p, [[560,90],[810,90]]);
  if (ctx.st === "B"){ D.wire(p, [[190,190],[440,190]]); D.wire(p, [[560,190],[810,190]]); }
  else { D.wire(p, [[190,214],[810,214]], "s-lan"); T(500, 232, "état A : même VLAN, chemin direct", "ts f-lan", p, "middle"); }
 },
 steps(ctx){
  const A = ctx.st === "A", R = A ? "FA4" : "F05", h = ["proto","client","serveur","état (simplifié)","créé par"];
  const r = s => [["tcp","10.10.0.10:51544","10.20.0.10:443",s,R]];
  const s = [
   {cap:`SYN de client-01. Aucun état pour ce 5-uplet : pf lit les règles de l'interface d'entrée (${A ? "LAN" : "LAN-USERS"}), la première qui correspond est <b>${R}</b>. Il crée un état.`, path:[[190,90],[500,90],[500,120]], pkt:"lan", hl:["pf"], pills:[[500,40,"ok",`${R} : pass + état`]], panel:{title:"Table d'états", head:h, rows:r("ouverture (SYN vu)"), mark:0}},
   {cap:"Le SYN repart vers nginx, qui répond SYN-ACK. Ce paquet arrive par l'interface DMZ, où <b>aucune règle</b> n'autorise un flux vers le LAN : il passe quand même, car il appartient à l'état.", path:[[810,98],[560,98]], pkt:"dmz", hl:["pf"], pills:[[500,40,"ok","état trouvé : pas de règle relue"]], panel:{title:"Table d'états", head:h, rows:r("SYN-ACK vu"), mark:0}},
   {cap:"ACK de client-01 : la poignée de main est terminée. Tous les paquets suivants de cette connexion, dans les deux sens, passent par l'état.", path:[[190,90],[440,90]], pkt:"lan", panel:{title:"Table d'états", head:h, rows:r("établi"), mark:0}},
   {cap:"FIN de part et d'autre : l'état passe en fermeture, puis expire après un court délai. Le prochain SYN créera un nouvel état… si une règle l'autorise encore.", path:[[190,90],[440,90]], pkt:"lan", panel:{title:"Table d'états", head:h, rows:r("fermeture, puis expiré"), mark:0}}
  ];
  if (A) s.push({cap:"client-03 → PostgreSQL:5432 en état A : même /24, même VLAN. Le paquet <b>ne traverse jamais pf</b> : ni règle, ni état, ni journal.", path:[[190,214],[810,214]], pkt:"bad", pills:[[500,184,"mute","pf ne voit rien"]], panel:{title:"Table d'états", head:h, rows:r("expiré"), mark:null}});
  else s.push({cap:"client-03 → PostgreSQL:5432 en état B : pf lit les règles de LAN-USERS (F05, F06), aucune ne correspond. Refus par défaut F99 : <b>pas d'état</b>, une ligne filterlog.", path:[[190,190],[470,190]], pkt:"bad", hl:["pf"], pills:[[500,40,"bad","F99 : block + log, aucun état"]], panel:{title:"Table d'états", head:h, rows:r("expiré"), mark:null}});
  return s;
 },
 fin:ctx => ctx.st === "A" ? "Une règle n'est évaluée qu'au premier paquet ; l'état fait le reste. En état A, le mouvement latéral n'entre même pas dans pf." : "Une règle n'est évaluée qu'au premier paquet ; l'état fait le reste. Un paquet refusé ne crée aucun état : il laisse seulement une ligne filterlog.",
 legend:[["var(--ok)","pass + état"],["var(--bad)","bloqué"],["var(--muted)","hors de pf"]]
};

/* ---------- éditeur de matrice (repris du module 5) ---------- */
const HX = {kali:["Kali","WAN","WAN"], ce:["client-ext","WAN","WAN"], nginx:["nginx","DMZ","DMZ-WEB"], pivot:["dmz-pivot","DMZ","DMZ-WEB"], bind9:["bind9","DMZ","DMZ-DNS"], api:["api","DMZ","DMZ-APP"], c01:["client-01","LAN","LAN-USERS"], c03:["client-03","LAN","LAN-USERS"], pg:["postgresql","LAN","LAN-DB"], admin:["admin","LAN","LAN-ADMIN"], opn:["opnsense (console)","FW","FW"]};
const sg = (k, st) => HX[k][st === "A" ? 1 : 2];
const R0 = {
 A:[{id:"FA1",i:"WAN",s:"any",d:["nginx"],p:"tcp",o:[80,443],a:"pass",on:true},{id:"FA2",i:"WAN",s:"any",d:["bind9"],p:"tcp/udp",o:[53],a:"pass",on:true},{id:"FA6",i:"WAN",s:"ce",d:["nginx"],p:"tcp",o:[5201],a:"pass",on:false},{id:"FA3",i:"DMZ",s:"net",d:["pg"],p:"tcp",o:[5432],a:"pass",on:true},{id:"FA4",i:"LAN",s:"net",d:"any",p:"any",o:"any",a:"pass",on:true}],
 B:[{id:"F01",i:"WAN",s:"any",d:["nginx"],p:"tcp",o:[80,443],a:"pass",on:true},{id:"F02",i:"WAN",s:"any",d:["bind9"],p:"tcp/udp",o:[53],a:"pass",on:true},{id:"F09",i:"WAN",s:"ce",d:["nginx"],p:"tcp",o:[5201],a:"pass",on:false},{id:"F03",i:"DMZ-WEB",s:"nginx",d:["api"],p:"tcp",o:[5000],a:"pass",on:true},{id:"F04",i:"DMZ-APP",s:"api",d:["pg"],p:"tcp",o:[5432],a:"pass",on:true},{id:"F05",i:"LAN-USERS",s:"net",d:["nginx"],p:"tcp",o:[443],a:"pass",on:true},{id:"F06",i:"LAN-USERS",s:"net",d:["bind9"],p:"udp",o:[53],a:"pass",on:true},{id:"F07",i:"LAN-ADMIN",s:"admin",d:["nginx","bind9","api","pg"],p:"tcp",o:[22],a:"pass",on:true},{id:"F08",i:"LAN-ADMIN",s:"admin",d:["opn"],p:"tcp",o:[443],a:"pass",on:true}]
};
const FL = [["client-ext → site","ce","nginx","tcp",443,"pass","pass"],["client-01 → site","c01","nginx","tcp",443,"pass","pass"],["client-01 → DNS","c01","bind9","udp",53,"pass","pass"],["nginx → API","nginx","api","tcp",5000,"same","pass"],["API → base","api","pg","tcp",5432,"pass","pass"],["API → base:22 (mauvais port)","api","pg","tcp",22,"block","block"],["admin → base (SSH)","admin","pg","tcp",22,"same","pass"],["admin → console","admin","opn","tcp",443,"pass","pass"],["client-01 → console","c01","opn","tcp",443,"pass","block"],["S-01 Kali → port 22","kali","opn","tcp",22,"block","block"],["S-04 dmz-pivot → base","pivot","pg","tcp",5432,"pass","block"],["S-05 client-03 → base","c03","pg","tcp",5432,"same","block"],["dmz-pivot → API","pivot","api","tcp",5000,"same","block"],["nginx → dmz-pivot","nginx","pivot","tcp",22,"same","same"]];
G.schemas["matrix-editor"] = {
 q:"Si j'ajoute, retire ou déplace une règle, quels flux passent et lesquels sont bloqués ?",
 custom:true,
 mount(host){
  let st = "B"; const RULES = {A:R0.A.map(r => ({...r})), B:R0.B.map(r => ({...r}))}; let nid = 1;
  host.innerHTML = `<div style="padding:12px 14px;display:grid;gap:12px"><div class="sbar" style="border:0;background:none;padding:0"><span class="seg" role="group" aria-label="État"><button type="button" data-st="A" aria-pressed="false">Matrice FA (état A)</button><button type="button" data-st="B" aria-pressed="true">Matrice F (état B)</button></span><button class="btn sm" type="button" data-x="reset">Revenir au rapport</button></div><div class="tbl" data-x="rules"></div><div class="card" style="display:flex;flex-wrap:wrap;gap:6px;align-items:center;font-size:13px" data-x="add"></div><div class="tbl" data-x="flows"></div></div>`;
  const q = s => host.querySelector(`[data-x="${s}"]`);
  const segs = () => st === "A" ? ["WAN","DMZ","LAN"] : ["WAN","DMZ-WEB","DMZ-DNS","DMZ-APP","LAN-USERS","LAN-DB","LAN-ADMIN"];
  function ev(f){ const [, s, d, p, o] = f, ss = sg(s, st), ds = sg(d, st); if (d !== "opn" && ss === ds) return ["same","—"];
    for (const r of RULES[st]){ if (!r.on || r.i !== ss) continue; if (!(r.s === "any" || r.s === "net" || r.s === s)) continue; if (!(r.d === "any" || r.d.includes(d))) continue; if (!(r.p === "any" || r.p === p || (r.p === "tcp/udp" && (p === "tcp" || p === "udp")))) continue; if (!(r.o === "any" || r.o.includes(o))) continue; return [r.a, r.id]; }
    return ["block", st === "A" ? "FA5 (défaut)" : "F99 (défaut)"]; }
  const lab = v => v === "pass" ? `<span class="pill p-ok">pass</span>` : v === "block" ? `<span class="pill p-bad">block + log</span>` : `<span class="pill p-warn">non filtré (même segment)</span>`;
  const nm = x => x === "any" ? "any" : x === "net" ? "net" : HX[x] ? HX[x][0] : x;
  function draw(){
   q("rules").innerHTML = `<table><thead><tr><th>Actif</th><th>Ordre</th><th>ID</th><th>Interface d'entrée</th><th>Source</th><th>Destination</th><th>Service</th><th>Action</th></tr></thead><tbody>${RULES[st].map((r, i) => `<tr style="${r.on ? "" : "opacity:.45"}"><td><input type="checkbox" data-a="on" data-i="${i}" ${r.on ? "checked" : ""} aria-label="Activer ${r.id}"></td><td style="white-space:nowrap"><button class="btn sm" type="button" data-a="up" data-i="${i}" aria-label="Monter ${r.id}">↑</button> <button class="btn sm" type="button" data-a="dn" data-i="${i}" aria-label="Descendre ${r.id}">↓</button>${r.x ? ` <button class="btn sm" type="button" data-a="rm" data-i="${i}" aria-label="Supprimer">×</button>` : ""}</td><td class="mono">${r.id}</td><td>${r.i}</td><td>${r.s === "net" ? r.i + " net" : nm(r.s)}</td><td>${r.d === "any" ? "any" : r.d.map(nm).join(", ")}</td><td class="mono">${r.p} ${r.o === "any" ? "any" : r.o.join(", ")}</td><td>${r.a === "pass" ? `<span class="pill p-ok">pass + log</span>` : `<span class="pill p-bad">block</span>`}</td></tr>`).join("")}<tr style="background:var(--surface-2)"><td>—</td><td>—</td><td class="mono">${st === "A" ? "FA5" : "F99"}</td><td>toutes</td><td>any</td><td>any</td><td class="mono">any</td><td><span class="pill p-bad">block + log</span></td></tr></tbody></table>`;
   const iface = q("add").querySelector("#mx-if") ? q("add").querySelector("#mx-if").value : null;
   q("add").innerHTML = `<b>Ajouter une règle</b><select id="mx-if" aria-label="Interface">${segs().map(s => `<option ${s === iface ? "selected" : ""}>${s}</option>`).join("")}</select><select id="mx-s" aria-label="Source"></select><span>→</span><select id="mx-d" aria-label="Destination"><option value="any">any</option>${Object.keys(HX).filter(k => k !== "kali" && k !== "ce").map(k => `<option value="${k}">${HX[k][0]}</option>`).join("")}</select><select id="mx-p" aria-label="Protocole"><option>tcp</option><option>udp</option><option value="any">tout</option></select><input id="mx-o" type="text" value="22" style="width:70px" aria-label="Ports"><select id="mx-a" aria-label="Action"><option>pass</option><option>block</option></select><select id="mx-pos" aria-label="Position"><option value="top">en haut</option><option value="bottom">en bas</option></select><button class="btn sm pri" type="button" data-a="add">Ajouter</button>`;
   const fillS = () => { const i = q("add").querySelector("#mx-if").value; q("add").querySelector("#mx-s").innerHTML = `<option value="net">${i} net</option><option value="any">any</option>` + Object.keys(HX).filter(k => sg(k, st) === i).map(k => `<option value="${k}">${HX[k][0]}</option>`).join(""); };
   fillS(); q("add").querySelector("#mx-if").addEventListener("change", fillS);
   let diff = 0;
   q("flows").innerHTML = `<table><thead><tr><th>Flux testé</th><th>Segments</th><th>Service</th><th>Verdict</th><th>Règle</th><th>Rapport</th></tr></thead><tbody>${FL.map(f => { const [v, r] = ev(f), x = st === "A" ? f[5] : f[6]; if (v !== x) diff++; return `<tr style="${v !== x ? "background:var(--chg)" : ""}"><td>${f[0]}</td><td class="mono">${sg(f[1], st)} → ${f[2] === "opn" ? "OPNsense" : sg(f[2], st)}</td><td class="mono">${f[3]} ${f[4]}</td><td>${lab(v)}</td><td class="mono">${r}</td><td>${lab(x)}</td></tr>`; }).join("")}</tbody></table><p style="padding:8px 12px;font-size:14px" class="${diff ? "p-warn" : "p-ok"}">${diff ? diff + " flux diffèrent de la matrice du rapport (lignes surlignées) : test_flux échouerait." : "Tous les verdicts correspondent à la matrice du rapport : test_flux serait à 100 %."}</p>`;
  }
  host.addEventListener("click", e => {
   const b = e.target.closest("[data-st],[data-a],[data-x]"); if (!b) return;
   if (b.dataset.st){ st = b.dataset.st; $$("[data-st]", host).forEach(x => x.setAttribute("aria-pressed", x.dataset.st === st)); draw(); return; }
   if (b.dataset.x === "reset"){ RULES[st] = R0[st].map(r => ({...r})); draw(); return; }
   const a = b.dataset.a, i = +b.dataset.i, rs = RULES[st]; if (!a || a === "on") return;
   if (a === "up" && i > 0) [rs[i-1], rs[i]] = [rs[i], rs[i-1]];
   else if (a === "dn" && i < rs.length - 1) [rs[i+1], rs[i]] = [rs[i], rs[i+1]];
   else if (a === "rm") rs.splice(i, 1);
   else if (a === "add"){ const g = id => q("add").querySelector(id).value; const ot = g("#mx-o").trim().toLowerCase(); const o = ot === "any" || !ot ? "any" : ot.split(/[,;\s]+/).map(Number).filter(n => n > 0 && n < 65536);
     const r = {id:"X" + nid++, i:g("#mx-if"), s:g("#mx-s"), d:g("#mx-d") === "any" ? "any" : [g("#mx-d")], p:g("#mx-p"), o:(o === "any" || !o.length) ? "any" : o, a:g("#mx-a"), on:true, x:true};
     if (g("#mx-pos") === "top") rs.unshift(r); else rs.push(r); }
   draw();
  });
  host.addEventListener("change", e => { const c = e.target.closest('[data-a="on"]'); if (c){ RULES[st][+c.dataset.i].on = c.checked; draw(); } });
  draw();
 }
};
})();
