/* Schémas du rôle 5 : chronologie d'une campagne, comparateur A / B par scénario. */
(function(){
"use strict";
const {S, T, D, $$} = G;

/* ---------- chronologie d'une exécution ---------- */
const LANES = [["orch","C6 orchestrateur (collecteur)"],["cap","C10 capture (hôte Docker, Kali)"],["atk","C7 attaquant (Kali ou conteneur)"],["sonde","C8 sonde (en continu)"],["fw","OPNsense : états pf, Suricata"]];
const LY = i => 60 + i * 46, X = s => 230 + s * 9.2;   /* s = secondes depuis le début de l'exécution */
G.schemas["campagne-chrono"] = {
 q:"Que fait l'orchestrateur, dans quel ordre, pour qu'une exécution soit mesurable et n'influence pas la suivante ?",
 vb:[1000, 330], minw:800,
 draw(p){
  LANES.forEach(([k, l], i) => { T(10, LY(i) + 4, l, "tl", p); S("line", {x1:225, y1:LY(i), x2:985, y2:LY(i), style:"stroke:var(--line);stroke-dasharray:2 4"}, p); });
  for (let s = 0; s <= 80; s += 10){ S("line", {x1:X(s), y1:280, x2:X(s), y2:286, style:"stroke:var(--muted)"}, p); T(X(s), 300, `${s} s`, "ts", p, "middle"); }
  S("line", {x1:X(0), y1:280, x2:X(82), y2:280, style:"stroke:var(--muted)"}, p);
  S("rect", {x:X(0), y:LY(3) - 7, width:X(82) - X(0), height:14, rx:4, style:"fill:var(--ok);fill-opacity:.25"}, p); T(X(41), LY(3) + 4, "HTTPS 1/s + DNS toutes les 5 s → sla.csv", "tl", p, "middle");
 },
 steps(){
  const bar = (lane, a, b, cls, txt) => p => { const w = X(b) - X(a); S("rect", {x:X(a), y:LY(lane) - 9, width:w, height:18, rx:4, style:`fill:var(--${cls});fill-opacity:.85`}, p);
    if (w >= txt.length * 6.4 + 12) T(X(a) + 6, LY(lane) + 4, txt, "ts", p).setAttribute("style", "fill:var(--surface);stroke:none");
    else T(X(b) + 5, LY(lane) + 4, txt, "tl", p); };   /* barre trop étroite : étiquette à droite */
  const mark = (s, t) => p => { S("line", {x1:X(s), y1:40, x2:X(s), y2:280, style:"stroke:var(--fg);stroke-width:2"}, p); T(X(s), 34, t, "tl", p, "middle"); };
  const all = (...f) => p => f.forEach(x => x(p));
  const h = ["run_id","scenario","etat","iteration","t0_utc","t1_utc","src_ip","cible_ip","code_retour"];
  const row = ["A-S05-03","S-05","A","3","2026-11-20T09:14:02.120Z","2026-11-20T09:14:09.731Z","10.10.0.12","10.10.0.70","0"];
  const f1 = bar(1, 0, 12, "trunk", "tcpdump postgres");
  const f2 = all(f1, mark(1, "t0"));
  const f3 = all(f2, bar(2, 1, 9, "bad", "nmap + psql"));
  const f4 = all(f3, mark(9, "t1"));
  const f5 = all(f4, bar(1, 0, 11, "trunk", "tcpdump postgres"));
  const f6 = all(f5, bar(4, 12, 15, "acc", "pfctl -k"));
  const f7 = all(f6, bar(0, 15, 45, "muted", "pause 30 s"));
  const f8 = all(f7, bar(0, 46, 50, "acc", "scénario suivant"), bar(1, 45, 70, "trunk", "capture run suivant"));
  return [
   {cap:"L'orchestrateur démarre la capture <b>avant</b> l'attaque (<code>capture.sh start A-S05-03 postgres</code>) : le PCAP contiendra le tout début de l'échange.", fx:f1},
   {cap:"Il note <b>t0</b> avec l'horloge du collecteur, la référence NTP du laboratoire. C'est l'origine du temps de détection.", fx:f2},
   {cap:"Il lance la commande du scénario par SSH (ici <code>docker exec client-03 …</code> sur l'hôte Docker), garde sa sortie dans <code>sorties/A-S05-03.txt</code> et son code de retour.", fx:f3},
   {cap:"Fin de la commande : il note <b>t1</b>. La fenêtre de corrélation du rôle 6 sera [t0, t1 + 10 s].", fx:f4},
   {cap:"Il arrête la capture (<code>capture.sh stop</code>, signal INT pour que tcpdump ferme proprement le fichier) et rapatrie le PCAP.", fx:f5, panel:{title:"runs.csv (une ligne par exécution)", head:h, rows:[row], mark:0}},
   {cap:"Réinitialisation : <code>pfctl -k 10.10.0.12</code> sur OPNsense supprime les états de l'attaquant. Sans cela, l'exécution suivante profiterait d'une connexion déjà ouverte.", fx:f6, panel:{title:"runs.csv", head:h, rows:[row]}},
   {cap:"Pause de 30 s : les blocages temporaires de Suricata (<code>rate_filter … timeout 20</code>) expirent et le trafic se calme. La sonde continue de mesurer pendant tout ce temps.", fx:f7, panel:{title:"runs.csv", head:h, rows:[row]}},
   {cap:"Scénario suivant, dans l'ordre tiré au hasard avec la <b>même graine</b> en A et en B (section 10.3). 6 scénarios × 10 exécutions ≈ 1 h 30.", fx:f8, panel:{title:"runs.csv", head:h, rows:[row,["A-S05-04","S-05","A","4","…","…","10.10.0.12","10.10.0.70","…"]], mark:1}}
  ];
 },
 fin:() => "Une exécution : capture, t0, attaque, t1, arrêt de la capture, ligne runs.csv, réinitialisation, pause. Seul l'orchestrateur sait quand une attaque a lieu : tous les indicateurs en dépendent (section 8.4)."
};

/* ---------- comparateur A / B ---------- */
const SC = {
 "S-01":{t:"Scan SYN", m:"Kali", c:"nmap -sS -Pn -p 1-1024 203.0.113.1", obj:"au moins un port « open » dans la sortie nmap", A:["Ports publiés découverts, pas d'alerte","pf transmet les SYN vers 80, 443, 53 (FA1, FA2) ; les autres sont bloqués en silence.","nmap : 53, 80, 443 open ; filterlog : un block par port non publié ; EVE : rien"], B:["Alerte de seuil, source bloquée","Suricata (em0) : plus de 20 SYN en 5 s depuis 203.0.113.66 → alerte sid 9000001 ; rate_filter : drop temporaire.","EVE : alerte + paquets rejetés ; nmap : la plupart des ports filtered ; un port publié peut apparaître s'il est sondé avant le seuil (écart E06)"]},
 "S-02":{t:"SYN flood sur le web", m:"Kali", c:"timeout 30 hping3 -S --flood -p 443 203.0.113.1", obj:"disponibilité de la sonde < 99 % pendant [t0, t1]", A:["Disponibilité dégradée, pas d'alerte","FA1 autorise chaque SYN : un état pf par SYN, file de nginx sous pression.","sla.csv : échecs ou délais ; à mesurer : le RST de Kali et les SYN cookies peuvent atténuer (écart E12)"], B:["Alerte + drop, disponibilité maintenue","Suricata rejette les SYN au-delà du seuil SYN flood, avant pf.","EVE : alertes « blocked » ; sla.csv : ≥ 99 % attendu"]},
 "S-03":{t:"Scan web + injection SQL", m:"Kali", c:"nikto -h https://203.0.113.1 -maxtime 60s -nointeractive ; curl -sk -G https://203.0.113.1/api/produit --data-urlencode \"id=1' OR '1'='1\"", obj:"la réponse de curl contient « secret »", A:["Injection réussie, pas d'alerte","HTTPS autorisé par FA1 ; saut nginx → API interne à la DMZ ; FA3 laisse l'API joindre la base.","curl : 3 lignes avec « secret » ; nginx : 200 ; EVE : rien"], B:["Alertes de signature, requêtes rejetées","Sur em1, la requête nginx → API est en clair : la règle SQLi sur /api/ la rejette.","curl : 502 ou délai ; EVE : sid 9000010 « blocked » ; nikto presque invisible (écart E11)"]},
 "S-04":{t:"Pivot DMZ → base", m:"dmz-pivot", c:"nmap -sT -Pn -p 5432 10.10.0.70 ; psql -h 10.10.0.70 -U lecteur -d catalogue -c 'select 1'", obj:"psql se connecte (code 0)", A:["Port ouvert (FA3)","dmz-pivot fait partie de « DMZ net » : FA3 l'autorise.","nmap : 5432 open ; psql : 1 ligne ; filterlog : pass FA3"], B:["Refusé (F04 limité à l'API)","pf (DMZ-WEB) : aucune règle vers LAN-DB ; F99.","nmap : filtered ; psql : délai ; filterlog : block F99 ; EVE : sid 9000021 si ajouté (écart E07)"]},
 "S-05":{t:"Mouvement latéral LAN → base", m:"client-03", c:"nmap -sT -Pn -p 22,5432 10.10.0.64/27 ; psql -h 10.10.0.70 -U lecteur -d catalogue -c 'select 1'", obj:"psql se connecte (code 0)", A:["Connexion établie, invisible au pare-feu","Même /24, même VLAN : livraison directe dans l'hôte Docker.","psql : 1 ligne ; filterlog : rien ; PCAP postgres : SYN puis SYN-ACK"], B:["Refusé et journalisé","Passage par la passerelle 10.10.0.1 ; Suricata alerte (9000020) ; pf : F99.","psql : délai ; filterlog : block sur LAN-USERS ; PCAP postgres : aucun SYN"]},
 "S-06":{t:"Trafic fragmenté anormal", m:"Kali", c:"nmap -f -sS -Pn -p 443 203.0.113.1 ; hping3 -f -S -p 443 -c 20 203.0.113.1", obj:"pas d'objectif de blocage : on mesure la détection", A:["Non détecté","pf réassemble les fragments puis filtre le paquet complet.","EVE : rien ; Wireshark : ip.frag_offset > 0"], B:["Alerte sur fragments anormaux","Suricata voit les fragments avant pf.","EVE : sid 9000030 (et événements de défragmentation)"]},
 "P-01":{t:"Panne de l'IPS pendant S-02", m:"Kali + collecteur", c:"timeout 60 hping3 -S --flood -p 443 203.0.113.1 ; arrêt de Suricata à t0 + 10 s, redémarrage à t0 + 40 s", obj:"disponibilité et alertes avant / pendant / après ; temps de reprise", A:["Sans objet","Pas d'IPS en état A.","—"], B:["Comportement mesuré (section 8.5)","Deux possibilités : le trafic passe sans inspection, ou l'interface ne transmet plus rien.","sla.csv seconde par seconde ; EVE : silence entre 10 et 40 s ; 5 répétitions"]}
};
G.schemas["comparateur-ab"] = {
 q:"Pour chaque scénario, que doit-on observer en état A et en état B, et comment juger l'objectif atteint ?",
 custom:true,
 mount(host){
  host.innerHTML = `<div style="padding:12px 14px;display:grid;gap:12px"><div class="seg" role="group" aria-label="Scénario" style="flex-wrap:wrap">${Object.keys(SC).map((k, i) => `<button type="button" data-k="${k}" aria-pressed="${i === 4}">${k}</button>`).join("")}</div><div data-x="h"></div><div class="grid2" data-x="ab"></div></div>`;
  const show = k => {
   $$("[data-k]", host).forEach(b => b.setAttribute("aria-pressed", b.dataset.k === k));
   const s = SC[k];
   host.querySelector('[data-x="h"]').innerHTML = `<h3>${k} · ${s.t}</h3><p style="font-size:14.5px">Machine : <b>${s.m}</b> · Objectif atteint si : <b>${s.obj}</b></p><pre style="white-space:pre-wrap">${G.esc(s.c)}</pre>`;
   host.querySelector('[data-x="ab"]').innerHTML = ["A","B"].map(st => `<div class="card" style="display:grid;gap:6px"><p><span class="pill ${st === "A" ? "p-acc" : "p-ids"}">état ${st}</span> <b>${s[st][0]}</b></p><p style="font-size:14.5px">${s[st][1]}</p><p style="font-size:14px" class="muted">${s[st][2]}</p></div>`).join("");
  };
  host.addEventListener("click", e => { const b = e.target.closest("[data-k]"); if (b) show(b.dataset.k); });
  show("S-05");
 }
};
})();
