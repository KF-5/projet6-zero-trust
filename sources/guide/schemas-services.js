/* Schémas du rôle 4 : macvlan, adresse d'un conteneur, chaîne nginx → API → PostgreSQL. */
(function(){
"use strict";
const {S, T, D} = G;

/* ---------- macvlan : parent, sous-interfaces, trafic interne ---------- */
G.schemas["macvlan"] = {
 q:"Pourquoi client-03 joint-il PostgreSQL sans passer par OPNsense en état A, et pourquoi une capture sur ens33 ne le voit-elle pas ?",
 vb:[1000, 330], minw:780, ab:true,
 draw(p, ctx){
  const A = ctx.st === "A";
  D.zone(p, 20, 30, 700, 270, "lan", "VM Hôte Docker (Debian 13, Docker 29)");
  D.node(p, 110, 270, 150, 34, "pile IP de l'hôte", "", "", "host");
  S("line", {x1:760, y1:170, x2:960, y2:170, class:"wr s-trunk", "stroke-width":5}, p); T(860, 160, "p6-trunk → OPNsense em1", "ts f-trunk", p, "middle");
  D.node(p, 660, 170, 70, 40, "ens33", "", "", "par");
  if (A){
   S("rect", {x:300, y:80, width:200, height:180, rx:10, style:"fill:var(--lan);fill-opacity:.08;stroke:var(--lan);stroke-dasharray:5 4"}, p);
   T(310, 98, "macvlan « lan » (mode bridge)", "ts f-lan", p);
   D.wire(p, [[500,170],[625,170]], "s-lan"); D.node(p, 560, 170, 90, 34, "ens33.10", "", "", "sub");
   D.node(p, 400, 130, 150, 40, "client-03", "10.10.0.12/24", "", "c03"); D.node(p, 400, 220, 150, 40, "PostgreSQL", "10.10.0.70/24", "", "pg");
  } else {
   S("rect", {x:300, y:50, width:200, height:110, rx:10, style:"fill:var(--lan);fill-opacity:.08;stroke:var(--lan);stroke-dasharray:5 4"}, p);
   T(310, 68, "macvlan « lan-users »", "ts f-lan", p);
   S("rect", {x:300, y:175, width:200, height:100, rx:10, style:"fill:var(--lan);fill-opacity:.08;stroke:var(--lan);stroke-dasharray:5 4"}, p);
   T(310, 193, "macvlan « lan-db »", "ts f-lan", p);
   D.wire(p, [[500,105],[560,105],[625,165]], "s-lan"); D.wire(p, [[500,225],[560,225],[625,175]], "s-lan");
   D.node(p, 560, 105, 80, 30, "ens33.11", "", "", "sub"); D.node(p, 560, 225, 80, 30, "ens33.12", "", "", "sub2");
   D.node(p, 400, 105, 150, 40, "client-03", "10.10.0.12/26", "", "c03"); D.node(p, 400, 225, 150, 40, "PostgreSQL", "10.10.0.70/27", "", "pg");
  }
 },
 steps(ctx){
  const A = ctx.st === "A";
  if (A) return [
   {cap:"client-03 et PostgreSQL sont deux interfaces macvlan sur la même sous-interface <code>ens33.10</code> (ens33 du rapport : ens33 sur p6-docker). En mode bridge, le noyau de l'hôte Docker les relie <b>directement</b> : la trame ne sort pas par ens33.", path:[[400,150],[400,200]], pkt:"bad", hl:["c03","pg"], pills:[[540,110,"bad","jamais sur ens33, jamais sur p6-trunk"]]},
   {cap:"Conséquence 1 : OPNsense ne voit rien (aucune règle, aucun journal). Conséquence 2 : <code>tcpdump -i ens33</code> sur l'hôte ne voit rien non plus. Il faut capturer <b>dans l'espace réseau du conteneur</b> (section 7.10, étape 7).", hl:["par"], pills:[[660,215,"mute","rien ne passe ici"]]},
   {cap:"Limite connue de macvlan : l'hôte Docker lui-même ne peut pas joindre ses conteneurs macvlan par ces interfaces. Ce n'est pas gênant ici : on administre l'hôte par MGMT et on entre dans les conteneurs avec <code>docker exec</code> ou <code>nsenter</code>.", path:[[185,270],[300,250]], pkt:"warn", hl:["host"], pills:[[230,236,"warn","pas de chemin hôte ↔ macvlan"]]}
  ];
  return [
   {cap:"En état B, client-03 est sur <code>ens33.11</code> (LAN-USERS) et PostgreSQL sur <code>ens33.12</code> (LAN-DB). Ce sont deux réseaux macvlan différents : aucun lien direct entre eux.", hl:["c03","pg"]},
   {cap:"client-03 calcule que 10.10.0.70 est hors de son /26 et envoie à sa passerelle 10.10.0.1. La trame sort par <code>ens33.11</code>, reçoit l'étiquette 11 et part sur le trunk vers OPNsense.", path:[[475,105],[560,105],[625,165],[960,170]], pkt:"trunk", hl:["sub","par"], pills:[[860,190,"acc","VLAN 11 → OPNsense"]]},
   {cap:"OPNsense applique F99 : le paquet n'atteindra jamais <code>ens33.12</code>. S'il était autorisé, il reviendrait étiqueté 12 et entrerait par <code>ens33.12</code>. Une capture sur ens33 verrait les deux passages.", path:[[960,178],[700,178]], pkt:"bad", pills:[[800,210,"bad","F99 : rien ne revient"]]}
  ];
 },
 fin:ctx => ctx.st === "A" ? "En état A, deux conteneurs du même VLAN se parlent dans le noyau de l'hôte Docker : ni OPNsense ni une capture sur ens33 ne voient leur trafic." : "En état B, chaque segment a sa sous-interface : tout trafic entre segments sort par ens33, étiqueté, et passe par OPNsense.",
 legend:[["var(--lan)","réseau macvlan"],["var(--trunk)","trunk"],["var(--bad)","flux interdit ou invisible"]]
};

/* ---------- un conteneur reçoit son adresse ---------- */
G.schemas["macvlan-addr"] = {
 q:"Que se passe-t-il exactement quand on lance client-01 sur le réseau macvlan lan-users ?",
 vb:[1000, 300], minw:760,
 draw(p){
  D.zone(p, 20, 30, 620, 250, "lan", "VM Hôte Docker");
  D.node(p, 590, 160, 70, 40, "ens33", "", "", "par");
  S("line", {x1:625, y1:160, x2:980, y2:160, class:"wr s-trunk", "stroke-width":5}, p);
  D.node(p, 880, 160, 150, 50, "OPNsense", "10.10.0.1 (VLAN 11)", "acc", "opn");
  S("rect", {x:60, y:70, width:300, height:180, rx:10, style:"fill:none;stroke:var(--muted);stroke-dasharray:4 3"}, p); T(70, 88, "espace réseau (netns) du conteneur", "ts", p);
 },
 steps(){
  const sub = p => { D.node(p, 470, 160, 90, 34, "ens33.11", "", "", "sub"); D.wire(p, [[515,160],[555,160]], "s-lan"); };
  const ctr = (p, ip) => { D.node(p, 210, 160, 190, 60, "client-01 · eth0", ip ? "10.10.0.10/26" : "(pas encore d'adresse)", "", "c01"); D.wire(p, [[305,160],[425,160]], "s-lan"); sub(p); };
  const h = ["où","commande","résultat"];
  return [
   {cap:"<code>docker network create -d macvlan -o parent=ens33.11 …</code> : Docker crée la sous-interface VLAN <code>ens33.11</code> sur ens33 (ens33 du rapport), puisque le nom du parent contient un point. Elle étiquettera tout ce qui sort.", fx:sub, hl:["par"], panel:{title:"Vérifications", head:h, rows:[["hôte","ip -d link show ens33.11","ens33.11@ens33 … vlan protocol 802.1Q id 11"]], mark:0}},
   {cap:"<code>docker run --network lan-users --ip 10.10.0.10 …</code> : Docker crée une interface macvlan avec <b>sa propre adresse MAC</b> (choisie par Docker ; son format dépend de la version), la place dans l'espace réseau du conteneur et la renomme <code>eth0</code>.", fx:p => ctr(p, false), hl:["c01"], panel:{title:"Vérifications", head:h, rows:[["hôte","ip -d link show ens33.11","… vlan id 11"],["conteneur","ip -br link","eth0@if7  UP  02:42:0a:0a:00:0a"]], mark:1}},
   {cap:"Docker lui attribue l'adresse fixe <code>10.10.0.10/26</code> et la route par défaut vers la passerelle déclarée au réseau : <code>10.10.0.1</code>.", fx:p => ctr(p, true), hl:["c01"], panel:{title:"Vérifications", head:h, rows:[["hôte","ip -d link show ens33.11","… vlan id 11"],["conteneur","ip -br link","eth0@if7  UP  02:42:0a:0a:00:0a"],["conteneur","ip route","default via 10.10.0.1 dev eth0 · 10.10.0.0/26 dev eth0"]], mark:2}},
   {cap:"Premier paquet vers l'extérieur du segment : le conteneur demande en ARP la MAC de 10.10.0.1. La requête sort étiquetée 11 ; OPNsense répond. Le conteneur est désormais une vraie machine du segment LAN-USERS.", fx:p => ctr(p, true), path:[[305,150],[555,150],[805,150]], pkt:"trunk", hl:["opn"], panel:{title:"Vérifications", head:h, rows:[["hôte","ip -d link show ens33.11","… vlan id 11"],["conteneur","ip -br link","eth0@if7  UP  02:42:0a:0a:00:0a"],["conteneur","ip route","default via 10.10.0.1 dev eth0 · 10.10.0.0/26 dev eth0"],["conteneur","ip neigh","10.10.0.1 dev eth0 lladdr bc:24:11:5e:00:01 REACHABLE"]], mark:3}}
  ];
 },
 fin:() => "client-01 a sa MAC, son adresse 10.10.0.10/26 et sa passerelle 10.10.0.1. Pour OPNsense, c'est une machine ordinaire du VLAN 11 : la matrice peut s'appliquer à elle."
};

/* ---------- chaîne nginx → API → PostgreSQL ---------- */
G.schemas["chaine-app"] = {
 q:"Combien de fois une requête de client-ext traverse-t-elle pf, et quelles règles l'autorisent en état A et en état B ?",
 vb:[1000, 652], minw:780, ab:true, ab0:"B",
 draw(p, ctx){ G.drawArch(p, ctx.st, {}); },
 steps(ctx){
  const A = G.ARCH, P = A.P, B = ctx.st === "B", FW = [380, 238];
  const out = k => B ? [P.pf, P.surT, P.if1, P.bus, ...A.down(k, "B")] : [P.pf, P.if1, P.bus, ...A.down(k, "A")];
  const inn = k => B ? [...A.up(k, "B"), P.bus, P.if1, P.surT, P.pf] : [...A.up(k, "A"), P.bus, P.if1, P.pf];
  const s = [
   {cap:`client-ext demande <code>https://203.0.113.1/api/produits</code>. ${B ? "Suricata inspecte sur em0 (contenu chiffré). " : ""}pf applique la redirection puis <b>${B ? "F01" : "FA1"}</b>.`, path:[P.ce, [238,190], [238,275], P.if0, ...(B ? [P.surW] : []), P.pf], pkt:"wan", pills:[[FW[0], FW[1], "ok", (B ? "F01" : "FA1") + " + NAT"]]},
   {cap:"Le paquet sort vers nginx (10.20.0.10). nginx déchiffre le TLS et lit le chemin <code>/api/produits</code>.", path:out("nginx"), pkt:"dmz", pills:[[A.above("nginx")[0], A.above("nginx")[1], "ok", "nginx : TLS terminé"]]}
  ];
  if (B) s.push({cap:"nginx relaie en HTTP clair vers l'API (10.20.0.70:5000). L'API est dans DMZ-APP : passage par la passerelle, Suricata lit la requête en clair, pf applique <b>F03</b>.", path:[...inn("nginx")], pkt:"dmz", pills:[[FW[0], FW[1], "ok", "F03"]]}, {cap:"Le paquet ressort sur le VLAN 23 jusqu'à l'API.", path:out("api"), pkt:"dmz"});
  else s.push({cap:"nginx relaie vers l'API. Même /24, même VLAN 20 : la trame va directement de conteneur à conteneur, <b>sans passer par pf</b>. Aucune règle, aucun journal.", path:A.direct("nginx", "api", "A"), pkt:"dmz", pills:[[(A.H.nginx.x + A.H.api.x) / 2, A.laneY("nginx", "A") + 20, "mute", "invisible pour pf"]]});
  s.push({cap:`L'API ouvre une connexion vers PostgreSQL (10.10.0.70:5432) : changement de zone, donc passage par pf : <b>${B ? "F04" : "FA3"}</b>${B ? ", seule source autorisée" : ", qui autorise toute la DMZ"}.`, path:inn("api"), pkt:"lan", pills:[[FW[0], FW[1], "ok", B ? "F04" : "FA3"]]},
   {cap:"La requête SQL arrive à PostgreSQL. Les réponses reviennent par les mêmes états, sans relire de règle.", path:out("pg"), pkt:"lan", pills:[[A.above("pg")[0], A.above("pg")[1], "ok", "SELECT … FROM produits"]]});
  return s;
 },
 fin:ctx => ctx.st === "B" ? "État B : trois frontières, trois règles distinctes (F01, F03, F04). Aucune confiance n'est héritée d'un saut à l'autre (section 8.1)." : "État A : deux passages par pf (FA1, FA3). Le saut nginx → API reste interne à la DMZ et invisible.",
 legend:[["var(--wan)","WAN"],["var(--dmz)","DMZ"],["var(--lan)","LAN"],["var(--trunk)","trunk"]]
};
})();
