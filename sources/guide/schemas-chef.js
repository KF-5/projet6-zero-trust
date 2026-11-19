/* Schémas du rôle 1 : topologie VMware (plateforme réelle), cycle instantané / restauration, contournement MGMT. */
(function(){
"use strict";
const {S, T, D} = G;

/* ---------- topologie de l'hôte VMware (clé historique « proxmox-topo » conservée pour les liens) ---------- */
const BR = {wan:[110,"p6-wan · segment LAN · WAN simulé 203.0.113.0/24 · aucun lien physique","wan"], trunk:[330,"p6-trunk · segment LAN · trunk 802.1Q · aucun lien physique","trunk"], mgmt:[410,"VMnet9 · hôte uniquement · MGMT 10.99.0.0/24 · Windows = 10.99.0.2","mgmt"], phys:[478,"VMnet8 · NAT VMware vers Internet (installations seulement)","muted"]};
const VM = [
 {k:"kali", x:40, w:150, t:"p6-kali", s:"1 vCPU · 1,5 Go", nics:[["eth0","wan",80],["eth1","mgmt",150]], tmp:175},
 {k:"opn", x:220, w:200, t:"p6-opnsense", s:"2 vCPU · 3 Go", nics:[["em0","wan",250],["em1","trunk",320],["em2","mgmt",385]], tmp:410},
 {k:"dock", x:450, w:250, t:"p6-docker", s:"2 vCPU · 2,5 Go", nics:[["ens34","wan",480],["ens33","trunk",570],["ens32","mgmt",660]], tmp:690},
 {k:"coll", x:740, w:150, t:"p6-collecteur", s:"1 vCPU · 1 Go", nics:[["ens32","mgmt",800]], tmp:875}
];
function drawTopo(p, ctx){
  S("rect", {x:10, y:10, width:980, height:500, rx:14, style:"fill:none;stroke:var(--line);stroke-width:1.5;stroke-dasharray:3 4"}, p);
  T(26, 34, "PC hôte · Windows 10 · VMware Workstation Pro · 16 Go de RAM", "tb", p);
  Object.keys(BR).forEach(k => { const [y, l, c] = BR[k]; const g = S("g", {"data-k":"br-" + k}, p); S("line", {x1:30, y1:y, x2:960, y2:y, class:`wr ${c === "muted" ? "" : "s-" + c}`, "stroke-width":k === "trunk" ? 5 : 3.5}, g); T(k === "phys" ? 36 : 955, y - 7, l, `ts ${c === "muted" ? "" : "f-" + c}`, g, k === "phys" ? "start" : "end"); });
  S("rect", {x:880, y:458, width:100, height:40, rx:7, class:"nd"}, p); T(930, 474, "Windows", "ts", p, "middle"); T(930, 488, "hôte", "tl", p, "middle");
  VM.forEach(v => {
    const g = S("g", {"data-k":v.k}, p);
    S("rect", {x:v.x, y:150, width:v.w, height:120, rx:10, class:"nd" + (v.k === "opn" ? " acc" : "")}, g);
    T(v.x + 10, 172, v.t, "tb", g); T(v.x + 10, 188, v.s, "ts", g);
    v.nics.forEach(([n, b, x]) => { const by = BR[b][0], top = by < 150; const y0 = top ? 150 : 270;
      if (!top && by > 330) S("line", {x1:x, y1:322, x2:x, y2:338, style:"stroke:var(--surface);stroke-width:9"}, p);
      S("line", {x1:x, y1:y0, x2:x, y2:by, class:`wr s-${BR[b][2]}`, "stroke-width":2.2}, p);
      S("circle", {cx:x, cy:by, r:4.5, style:"fill:var(--fg)"}, p);
      T(x + 4, top ? 140 : 284, n, "ts", p); });
    if (ctx.st === "A"){ const x = v.tmp; [330, 410].forEach(y => S("line", {x1:x, y1:y - 8, x2:x, y2:y + 8, style:"stroke:var(--surface);stroke-width:9"}, p)); S("line", {x1:x, y1:270, x2:x, y2:478, class:"wr", "stroke-width":2, "stroke-dasharray":"5 4"}, p); S("circle", {cx:x, cy:478, r:4.5, style:"fill:var(--warn)"}, p); T(x - 4, 296, "NAT", "ts", p, "end"); }
  });
  S("line", {x1:960, y1:478, x2:930, y2:478, class:"wr", "stroke-width":3.5}, p);
  T(30, 500, ctx.st === "A" ? "Installations : carte NAT branchée (pointillés) sur VMnet8" : "En service : cartes NAT débranchées (« non connectée au démarrage »), aucune sortie possible", `tl ${ctx.st === "A" ? "" : "f-ok"}`, p);
}
G.schemas["proxmox-topo"] = {
 q:"Comment les 4 VM sont-elles câblées, et pourquoi le laboratoire ne peut-il rien envoyer vers Internet pendant les campagnes ?",
 vb:[1000, 520], minw:760, ab:true, ab0:"B", abLabels:["Installations","En service"],
 draw:drawTopo,
 steps(ctx){
  const s = [
   {cap:"Les VM ne touchent jamais la carte Wi-Fi de Windows : aucune n'est en mode « pont ». Windows n'est relié au laboratoire que par <b>VMnet9</b> (MGMT, 10.99.0.2) et, pendant les installations seulement, par le <b>NAT</b> de VMware (VMnet8). L'équipe arrive sur Windows par Tailscale et des tunnels SSH (étape 5).", hl:["br-phys"], path:[[930,478],[930,458]], pkt:"acc"},
   {cap:"<b>p6-wan</b> est le WAN simulé : Kali (eth0), OPNsense (em0) et l'hôte Docker (ens34, pour client-ext) s'y branchent. C'est un « segment LAN » VMware : il ne relie que des VM, le trafic reste dans le PC.", hl:["br-wan","kali","opn"], path:[[80,150],[80,110],[250,110],[250,150]], pkt:"wan", pills:[[165,110,"bad","SYN de Kali vers 203.0.113.1"]]},
   {cap:"<b>p6-trunk</b> transporte tous les VLAN internes avec leur étiquette 802.1Q, entre l'hôte Docker (ens33) et OPNsense (em1). Un segment LAN VMware laisse passer les trames étiquetées sans rien configurer.", hl:["br-trunk","dock","opn"], path:[[570,270],[570,330],[320,330],[320,270]], pkt:"trunk", pills:[[445,330,"acc","trame étiquetée VLAN 10"]]},
   {cap:"<b>VMnet9</b> est le réseau MGMT : Windows (10.99.0.2) et le collecteur y administrent les VM en SSH, le collecteur y reçoit syslog et donne l'heure. OPNsense ne filtre que ce qui passe par lui : chaque VM a donc son propre filtre SSH (étape 5).", hl:["br-mgmt","coll","dock"], path:[[800,270],[800,410],[660,410],[660,270]], pkt:"mgmt", pills:[[730,410,"ok","SSH depuis 10.99.0.10"]]}
  ];
  if (ctx.st === "A") s.push({cap:"Pendant une installation seulement, la carte <b>NAT</b> de la VM est branchée : <code>apt</code>, <code>docker pull</code>, règles ET Open. On la rebranche dans VMware (Paramètres › Carte réseau NAT › Connecté), puis on la débranche et on décoche « Connecter à la mise sous tension » (écarts E25, E27).", hl:["br-phys","dock"], path:[[690,270],[690,478],[930,478],[930,458]], pkt:"acc", pills:[[810,462,"warn","à débrancher après l'installation"]]});
  else s.push({cap:"En service, un paquet de Kali vers une adresse d'Internet n'a nulle part où aller : p6-wan n'a aucun lien physique et toutes les cartes NAT sont débranchées. Prouvé depuis Kali le 16 et le 17 novembre : <code>tests-cloisonnement.sh</code> = 19/19 (ENF1).", hl:["br-wan","kali"], path:[[80,150],[80,110],[30,110]], pkt:"bad", pills:[[150,110,"bad","aucune sortie"]]});
  return s;
 },
 fin:ctx => ctx.st === "A" ? "Installations : une VM télécharge par sa carte NAT branchée. À débrancher juste après." : "En service : 2 segments LAN sans lien physique + VMnet9, 4 VM, cartes NAT débranchées. Le seul lien avec l'extérieur est Windows (VMnet9), atteint par l'équipe via Tailscale et des tunnels SSH limités.",
 legend:[["var(--wan)","p6-wan WAN"],["var(--trunk)","p6-trunk trunk"],["var(--mgmt)","VMnet9 MGMT"],["var(--muted)","VMnet8 NAT (installations)"]]
};

/* ---------- cycle snapshot / restauration ---------- */
const TL = [[70,"Installation"],[190,"Config. état A"],[310,"test_flux A 100 %"],[430,"snapshot etat-A"],[550,"Campagne A"],[670,"Config. état B"],[790,"snapshot etat-B"],[910,"Campagne B"]];
G.schemas["snapshot-cycle"] = {
 q:"Que contient un état figé, et que perd-on exactement en revenant à un snapshot ?",
 vb:[1000, 330], minw:760,
 draw(p){
  S("line", {x1:40, y1:120, x2:960, y2:120, class:"wr", "stroke-width":2}, p);
  TL.forEach(([x, t], i) => { const snap = /snapshot/.test(t); const g = S("g", {"data-k":"t" + i}, p);
    S(snap ? "rect" : "circle", snap ? {x:x - 12, y:108, width:24, height:24, rx:4, class:"nd acc"} : {cx:x, cy:120, r:8, class:"nd"}, g);
    T(x, 96, t, "tl", g, "middle"); });
  [[430,"etat-A"],[790,"etat-B"]].forEach(([x, n]) => { const g = S("g", {"data-k":"s-" + n}, p); S("rect", {x:x - 85, y:165, width:170, height:96, rx:8, class:"nd acc"}, g); T(x, 184, `snapshot ${n}`, "tn", g, "middle"); T(x - 76, 202, "disques + RAM des 4 VM", "ts", g); T(x - 76, 218, `tag Git ${n} :`, "ts", g); T(x - 76, 232, "config.xml, compose, flows.csv", "ts", g); T(x - 76, 246, "règles Suricata, VERSION.txt", "ts", g); S("line", {x1:x, y1:132, x2:x, y2:165, class:"wr s-acc", "stroke-dasharray":"3 3"}, p); });
  T(40, 300, "VMware garde les instantanés dans un arbre : on peut revenir à l'un ou à l'autre.", "ts", p);
 },
 steps(){
  const P = (a, b) => [[a, 140], [b, 140]];
  return [
   {cap:"On installe, puis on configure l'état A (rôles 2 et 4). Rien n'est encore figé : une erreur ici se corrige à la main.", path:P(70, 310), pkt:"acc"},
   {cap:"test_flux est à 100 % : on fige. Instantané <code>etat-A</code> des 4 VM (<code>vmrun snapshot</code> ou le panneau) et <code>git tag etat-A</code> sur le dépôt. Les deux doivent citer le même commit.", path:P(310, 430), pkt:"ok", hl:["s-etat-A"], pills:[[430,150,"ok","etat-A"]]},
   {cap:"Campagne A (N = 10). Les résultats vont dans <code>resultats/A/&lt;date&gt;/</code>, hors des VM : ils survivent à toute restauration.", path:P(430, 550), pkt:"acc"},
   {cap:"On modifie pour l'état B (VLAN, règles, Suricata). On teste, puis on fige <code>etat-B</code>, enfant de <code>etat-A</code> dans l'arbre.", path:P(550, 790), pkt:"acc", hl:["s-etat-B"], pills:[[790,150,"ok","etat-B"]], keep:true},
   {cap:"Restaurer <code>etat-A</code> (démo, nouvelle mesure) : <code>vmrun revertToSnapshot</code> (ou « Revenir » dans le panneau) remet les disques et la RAM. <b>Tout ce qui n'a pas été figé depuis le dernier snapshot est perdu.</b> etat-B reste disponible.", path:[[910,140],[910,160],[430,160],[430,140]], pkt:"warn", pills:[[670,176,"warn","rollback : modifications non figées perdues"]]}
  ];
 },
 fin:() => "Deux états figés, chacun lié à un tag Git. Les résultats des campagnes sont stockés hors des VM. On passe d'un état à l'autre en quelques minutes, ce qui sécurise la démonstration."
};

/* ---------- contournement MGMT ---------- */
G.schemas["mgmt-bypass"] = {
 q:"Kali peut-elle utiliser le réseau MGMT pour atteindre l'hôte Docker sans passer par OPNsense ?",
 vb:[1000, 300], minw:760, ab:true, abLabels:["Sans filtre local","Avec nftables (étape 5)"],
 draw(p, ctx){
  D.zone(p, 20, 30, 960, 250, "mgmt", "VMnet9 · MGMT 10.99.0.0/24 · un seul domaine L2 (Windows = 10.99.0.2)");
  S("line", {x1:60, y1:200, x2:940, y2:200, class:"wr s-mgmt", "stroke-width":3.5}, p);
  [["kali",140,"Kali","10.99.0.66"],["opn",380,"OPNsense","10.99.0.1"],["dock",620,"Hôte Docker","10.99.0.20"],["coll",860,"Collecteur","10.99.0.10"]].forEach(([k, x, n, ip]) => { D.node(p, x, 110, 170, 56, n, ip, k === "opn" ? "acc" : "", k); S("line", {x1:x, y1:138, x2:x, y2:200, class:"wr s-mgmt", "stroke-width":2}, p); });
  if (ctx.st === "B") ["kali","dock","coll"].forEach(k => { const x = {kali:140, dock:620, coll:860}[k]; S("rect", {x:x - 70, y:150, width:140, height:18, rx:4, style:"fill:var(--ok);fill-opacity:.15;stroke:var(--ok)"}, p); T(x, 163, "nftables p6_ssh", "ts", p, "middle"); });
 },
 steps(ctx){
  const B = ctx.st === "B";
  return [
   {cap:"Kali envoie un SYN vers l'hôte Docker (10.99.0.20, port 22). La trame traverse VMnet9 directement : <b>OPNsense ne la voit jamais</b>, ses règles MGMT ne peuvent rien.", path:[[140,138],[140,200],[620,200],[620,B ? 168 : 138]], pkt:"bad", pills:B ? [[620,232,"ok","nftables : drop (source ≠ 10.99.0.2 / .10)"]] : [[620,232,"bad","SSH ouvert : contournement possible"]], hl:["kali","dock"]},
   {cap:"Le collecteur (10.99.0.10) ouvre une session SSH vers l'hôte Docker. Avec Windows (10.99.0.2), ce sont les deux seuls accès autorisés.", path:[[860,138],[860,200],[620,200],[620,B ? 168 : 138]], pkt:"ok", pills:[[740,232,"ok","SSH depuis 10.99.0.10 : accepté"]], hl:["coll","dock"]},
   {cap:B ? "La même tentative vers le collecteur est jetée par son propre nftables. Preuve : tests 2 et 3 de <code>tests-cloisonnement.sh</code> (19/19), à citer dans le rapport final." : "Sans pare-feu local, Kali joindrait aussi le collecteur (syslog, SSH) et pourrait fausser les journaux.", path:[[140,138],[140,200],[860,200],[860,B ? 168 : 138]], pkt:"bad", pills:B ? [[860,232,"ok","drop"]] : [[860,232,"bad","atteint"]], hl:["kali","coll"]}
  ];
 },
 fin:ctx => ctx.st === "B" ? "Avec nftables sur chaque VM, seuls Windows et le collecteur ouvrent des sessions SSH sur MGMT. Kali ne peut plus s'en servir pour contourner le pare-feu." : "Sans pare-feu local, le réseau MGMT est un raccourci qui contourne OPNsense : c'est l'écart E02."
};
})();
