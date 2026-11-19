/* Contribution de chaque rôle au rapport final.
   Plan du rapport final (rôle chef, étape « Assembler le rapport final ») :
   1 Résumé · 2 Conception et écarts · 3 Réalisation (3.1 plateforme, 3.2 pare-feu, 3.3 services, 3.4 IDS/IPS)
   4 Protocole et campagnes · 5 Résultats et analyse · 6 Limites · Annexes A à D.
   Ce fichier ajoute : (1) des rappels « Capture pour le rapport » dans les étapes où la preuve n'existe qu'à ce moment-là ;
   (2) une étape finale « Ma contribution au rapport » par rôle, avec le modèle à remplir. */
(function(){
const R = G.roles;
const cap = (rid, n, list) => { const s = R[rid].steps[n - 1]; s.captures = (s.captures || []).concat(list); };

const REGLES = `<b>Règles communes pour les captures.</b> Format PNG, fenêtre entière et lisible (pas de capture de tout l'écran),
heure visible quand c'est possible. Jamais de mot de passe, de clé ou de jeton à l'écran. Nom de fichier imposé, dans <code>rapport/captures/</code>.
Une sortie de commande se colle <b>en texte</b> (bloc de code) dans le .md, pas en image. Une capture qui n'est pas prise au bon moment est souvent impossible à refaire :
l'état A est écrasé par l'état B le 22 novembre.`;

const VERIF = (rid) => `cd ~/projet6                                   # racine du dépôt Git
f=rapport/${rid}.md
# 1. Chaque image citée existe
grep -o 'captures/[^)]*' "$f" | while read c; do
  [ -f "rapport/$c" ] && echo "OK      $c" || echo "MANQUE  $c"
done
# 2. Plus aucune zone à compléter
echo "Zones à compléter restantes : $(grep -c 'À COMPLÉTER' "$f")"`;

function contrib(rid, o){
  return {
    titre:"Ma contribution au rapport final", quand:o.quand,
    but:`Le fichier <code>rapport/${rid}.md</code> complet, prêt à être collé tel quel dans la section ${o.section} du rapport final, avec toutes ses captures dans <code>rapport/captures/</code>.`,
    pourquoi:`Le rôle chef assemble le rapport final à partir des fichiers <code>rapport/&lt;rôle&gt;.md</code>, livrés le 28 novembre (plan : étape « Assembler le rapport final »). Votre partie : ${o.partie}. Le professeur notera les preuves autant que le travail : chaque affirmation doit renvoyer à une capture, une sortie de commande ou un chiffre mesuré.<br><br>${REGLES}`,
    prereq:{etapes:o.pre, texte:"Les captures se prennent au fil des étapes : chaque étape concernée porte un encadré « Capture pour le rapport ». Cette étape-ci vérifie qu'il ne manque rien et met le texte en forme.", modules:[12]},
    ui:o.captures.map(([f, q, e]) => ({path:`rapport/captures/${f}`, faire:`${q} <span class="muted">(étape ${e})</span>`})),
    config:[{t:`Modèle à copier puis remplir`, file:`rapport/${rid}.md`, code:o.modele, note:"Remplacer chaque « À COMPLÉTER ». Garder les titres : le chef les numérote dans le rapport final."},
            {t:"Questions auxquelles votre texte doit répondre", code:o.questions.map((q, i) => `${i + 1}. ${q}`).join("\n"), note:"Ce sont aussi les questions que le professeur posera en soutenance sur votre partie."}],
    tester:[{t:"Vérifier sa contribution", cmd:VERIF(rid), sortie:`OK      captures/${o.captures[0][0]}\nOK      captures/${o.captures[1][0]}\n…\nZones à compléter restantes : 0`, sens:[["MANQUE","une image citée n'est pas dans le dépôt : la prendre ou corriger le nom"],["Zones à compléter : 0","le texte est complet"]]}],
    pannes:[
     ["Une capture n'a pas été prise et l'état a changé depuis","L'instantané VMware de l'état concerné existe (étape « Figer un état » du rôle chef)","Demander au rôle chef une restauration temporaire sur un clone, prendre la capture, revenir. Sinon, remplacer par la sortie texte archivée et le signaler dans le texte."],
     ["Le texte décrit ce qui était prévu, pas ce qui a été fait","Relire en comparant avec docs/ et les commits","Écrire au passé, ce qui a réellement été fait, et reporter chaque différence dans « Écarts avec la conception »."],
     ["Les chiffres ne correspondent pas à ceux du rôle 6","Chiffres recopiés à la main ou d'une version ancienne","Reprendre uniquement les fichiers de <code>resultats/synthese/</code>, jamais de chiffres recalculés à part."]
    ],
    fini:["Toutes les captures de la liste sont dans rapport/captures/ avec le bon nom","Plus aucun « À COMPLÉTER » dans rapport/" + rid + ".md","Chaque question de la liste a sa réponse dans le texte","Relu par un autre membre du groupe"],
    livrer:`Commit de <code>rapport/${rid}.md</code> et de ses captures le <b>28 novembre au plus tard</b>, puis message au rôle chef : « ${o.section} prête ».`
  };
}

/* ---------- Rôle 1 : chef / plateforme ---------- */
cap("chef", 1, [["chef-01-hote-vmware.png", "VMware › Aide › À propos (version) et la bibliothèque avec les 4 VM ; Gestionnaire des tâches › Performance (processeur, 16 Go de RAM)."]]);
cap("chef", 2, [["chef-02-reseaux.png", "Éditeur de réseau virtuel : VMnet9 « hôte uniquement » sans DHCP ; fenêtre « Segments LAN » avec p6-wan et p6-trunk ; aucune carte en mode Pont."]]);
cap("chef", 3, [["chef-03-vm.png", "Paramètres de p6-opnsense : ses 4 cartes réseau (p6-wan, p6-trunk, VMnet9, NAT débranchée) ; même vue pour p6-docker."]]);
cap("chef", 5, [["chef-05-confinement.png", "Sortie de tests-cloisonnement.sh depuis Kali (19/19). Coller aussi le texte."], ["chef-05-acces-equipe.png", "Le panneau de contrôle et une console VNC ouverts depuis un autre PC, à travers le tunnel."]]);
cap("chef", 7, [["chef-07-snapshots-A.png", "Gestionnaire d'instantanés de VMware (ou panneau) avec etat-A (date visible)."], ["chef-07-snapshots-B.png", "Même vue, avec etat-B, après le 26 novembre."]]);
cap("chef", 8, [["chef-08-ressources.png", "Gestionnaire des tâches de Windows › Performance (processeur, mémoire) pendant une campagne : prouve le dimensionnement de la section 6."]]);

/* ---------- Rôle 2 : pare-feu et segmentation ---------- */
cap("parefeu", 1, [["parefeu-01-version.png", "System › Firmware : version exacte d'OPNsense."]]);
cap("parefeu", 3, [["parefeu-03-vlan-A.png", "Interfaces › Assignments et Interfaces › Other Types › VLAN : VLAN 10 et 20 sur le trunk."]]);
cap("parefeu", 4, [["parefeu-04-nat.png", "Firewall › NAT › Port Forward : FA1, FA2 et FA6."]]);
cap("parefeu", 5, [["parefeu-05-regles-A.png", "Firewall › Rules de chaque interface en état A (une capture par interface si besoin : -wan, -dmz, -lan)."], ["parefeu-05-filterlog-A.png", "Firewall › Log Files › Live View avec au moins une ligne « block »."]]);
cap("parefeu", 7, [["parefeu-07-test-flux-A.png", "Terminal : la dernière ligne de test_flux en état A (100 % conforme). Coller aussi le texte complet en annexe."]]);
cap("parefeu", 8, [["parefeu-08-vlan-B.png", "Les 6 VLAN de l'état B (11 à 13, 21 à 23) et leurs passerelles."], ["parefeu-08-regles-B.png", "Firewall › Rules en état B : F01 à F09 puis F99, avec la journalisation activée."]]);
cap("parefeu", 9, [["parefeu-09-test-flux-B.png", "Terminal : test_flux en état B, 100 % conforme."], ["parefeu-09-filterlog-S05.png", "Live View pendant S-05 en état B : le blocage 10.10.0.12 → 10.10.0.70:5432 par la règle par défaut."]]);

/* ---------- Rôle 3 : IDS/IPS ---------- */
cap("ids", 1, [["ids-01-netmap.png", "Services › Intrusion Detection › Administration : IPS mode et interface du test, plus le bilan du ping (pertes)."]]);
cap("ids", 2, [["ids-02-config.png", "Settings : interfaces WAN et TRUNK, promiscuous, réseaux maison."]]);
cap("ids", 4, [["ids-04-regles.png", "Onglet des règles filtré sur les sid 9000001 à 9000099, toutes chargées."]]);
cap("ids", 5, [["ids-05-alertes.png", "Onglet « Alerts » : au moins une alerte par scénario (S-01, S-02, S-03, S-05)."]]);
cap("ids", 6, [["ids-06-fp-reglage.png", "Graphique des faux positifs par règle avant et après réglage (fourni par le rôle 6 à partir de fp_par_sid.csv)."], ["ids-06-policy-drop.png", "Écran des politiques : familles passées en drop, les autres en alert."]]);
cap("ids", 7, [["ids-07-p01.png", "Pendant P-01 : suricata.log (arrêt puis redémarrage) et la disponibilité mesurée pendant la panne."]]);
R.ids.prep.captures = [["ids-00-tester.png", "Sortie de tester.sh sur ton PC (OK par scénario). Coller aussi le texte."]];

/* ---------- Rôle 4 : services ---------- */
cap("services", 1, [["services-01-versions.png", "docker version et la version de l'image de chaque service."]]);
cap("services", 2, [["services-02-reseaux-A.png", "docker network ls et docker network inspect d'un réseau macvlan de l'état A (parent ens33.10 ou ens33.20, sous-réseau)."]]);
cap("services", 3, [["services-03-sqli-A.png", "Le point vulnérable : la requête d'injection SQL qui renvoie toutes les lignes (preuve que S-03 a une cible réelle)."]]);
cap("services", 4, [["services-04-nginx.png", "La page servie en HTTPS par nginx et son certificat (navigateur ou curl -vk)."]]);
cap("services", 5, [["services-05-dns.png", "dig @10.20.0.40 www.labo.test : réponse correcte."]]);
cap("services", 6, [["services-06-docker-ps.png", "docker ps : tous les conteneurs avec leur état « Up »."]]);
cap("services", 8, [["services-08-reseaux-B.png", "Les réseaux macvlan de l'état B : mêmes adresses d'hôtes, nouveaux masques et passerelles."]]);

/* ---------- Rôle 5 : attaques ---------- */
cap("attaques", 1, [["attaques-01-confinement-kali.png", "Depuis Kali : ip route et l'échec d'une connexion vers Internet."]]);
cap("attaques", 3, [["attaques-03-orchestrateur.png", "L'orchestrateur en cours d'exécution (progression, scénario, itération)."]]);
cap("attaques", 5, [["attaques-05-S01-A.png", "S-01 en état A : nmap découvre les ports publiés, aucune alerte."], ["attaques-05-S05-A.png", "S-05 en état A : la connexion psql de client-03 à la base réussit."], ["attaques-05-fin-A.png", "Fin de campagne A : nombre d'exécutions et dossier de résultats."]]);
cap("attaques", 6, [["attaques-06-S01-B.png", "S-01 en état B : le scan s'interrompt (blocage temporaire)."], ["attaques-06-S05-B.png", "S-05 en état B : délai dépassé, connexion impossible."], ["attaques-06-fin-B.png", "Fin de campagne B et de P-01."]]);

/* ---------- Rôle 6 : analyse ---------- */
cap("analyse", 1, [["analyse-01-ntp.png", "Décalage des horloges des 4 VM juste avant la campagne A (moins de quelques millisecondes)."]]);
cap("analyse", 2, [["analyse-02-rsyslog.png", "Journaux reçus sur le collecteur : une ligne filterlog et une alerte EVE."]]);
cap("analyse", 6, [["analyse-06-detection.png", "Graphique taux de détection A vs B par scénario."], ["analyse-06-blocage.png", "Graphique taux de blocage A vs B."], ["analyse-06-latence.png", "Latence et disponibilité : A, B sans IPS, B avec IPS."]]);

/* ---------- Étapes finales ---------- */
R.chef.steps.splice(8, 0, contrib("chef", {
  quand:"au fil des étapes ; texte le 27–28 nov.", section:"3.1 (plateforme), 1 (résumé), 2 (écarts), 6 (limites) et annexe A",
  partie:"la plateforme et le confinement, plus les parties transverses que vous seul pouvez écrire : le résumé, les écarts avec la conception et les limites",
  pre:["chef-8", "analyse-6"],
  captures:[["chef-01-hote-vmware.png", "Hôte VMware", 1], ["chef-02-reseaux.png", "Les 3 réseaux virtuels sans lien physique", 2], ["chef-03-vm.png", "Les 4 VM et le matériel d'OPNsense", 3], ["chef-05-confinement.png", "Preuve de confinement", 5], ["chef-07-snapshots-A.png", "Snapshot etat-A", 7], ["chef-07-snapshots-B.png", "Snapshot etat-B", 7], ["chef-08-ressources.png", "CPU et RAM pendant une campagne", 8]],
  modele:`## 1. Résumé
À COMPLÉTER — 5 à 8 lignes, écrites en dernier : ce qui a été construit, puis les 3 chiffres clés A vs B
(fournis par le rôle 6 : détection, blocage, disponibilité), puis la principale limite.

## 2. Conception et écarts
La conception remise le 16 novembre a été suivie, avec les écarts suivants (détail : ECARTS.md) :
| Écart | Ce qui était prévu | Ce qui a été fait | Pourquoi |
| --- | --- | --- | --- |
| À COMPLÉTER | | | |

## 3.1 Plateforme
### Réalisation
À COMPLÉTER — VMware Workstation Pro (version) sur Windows au lieu de Proxmox (écart E31), 4 VM, 3 réseaux virtuels, réseau MGMT filtré, accès de l'équipe par Tailscale et tunnel SSH. Comparer au dimensionnement prévu (section 6).
![Hôte VMware](captures/chef-01-hote-vmware.png)
![Réseaux virtuels](captures/chef-02-reseaux.png)
![Machines virtuelles](captures/chef-03-vm.png)
![Ressources pendant une campagne](captures/chef-08-ressources.png)
### Reproductibilité
À COMPLÉTER — snapshots et tags Git etat-A / etat-B.
![Snapshot etat-A](captures/chef-07-snapshots-A.png)
![Snapshot etat-B](captures/chef-07-snapshots-B.png)
### Difficultés et solutions
À COMPLÉTER

## 6. Limites
À COMPLÉTER — reprendre les limites assumées de la conception (section 11.3) et ajouter celles constatées.

## Annexe A. Preuve de confinement
![Confinement](captures/chef-05-confinement.png)
\`\`\`
À COMPLÉTER — coller docs/preuve-confinement.txt
\`\`\``,
  questions:["Quelles ressources (CPU, RAM) ont réellement été consommées, par rapport aux 11 Go prévus ?","Comment a-t-on prouvé qu'aucun trafic d'attaque ne pouvait sortir du laboratoire ?","Quels écarts avec la conception, et pourquoi chacun était-il nécessaire ?","Comment un état peut-il être reconstruit à l'identique (snapshots, tags) ?","Quels incidents de plateforme ont coûté du temps, et qu'en retenir ?"]
}));
const asm = R.chef.steps[9];
asm.prereq = Object.assign({}, asm.prereq, {etapes:["chef-9", "parefeu-10", "services-9", "ids-8", "attaques-7", "analyse-8"]});

R.parefeu.steps.push(contrib("parefeu", {
  quand:"au fil des étapes ; texte le 26–28 nov.", section:"3.2 (pare-feu et segmentation) et annexe B (test_flux)",
  partie:"le passage de 3 zones à 6 segments, la matrice appliquée et la preuve qu'elle est respectée",
  pre:["parefeu-9"],
  captures:[["parefeu-01-version.png", "Version d'OPNsense", 1], ["parefeu-03-vlan-A.png", "VLAN de l'état A", 3], ["parefeu-04-nat.png", "Redirections NAT", 4], ["parefeu-05-regles-A.png", "Règles de l'état A", 5], ["parefeu-05-filterlog-A.png", "Journal de filtrage", 5], ["parefeu-07-test-flux-A.png", "test_flux état A", 7], ["parefeu-08-vlan-B.png", "VLAN de l'état B", 8], ["parefeu-08-regles-B.png", "Règles de l'état B", 8], ["parefeu-09-test-flux-B.png", "test_flux état B", 9], ["parefeu-09-filterlog-S05.png", "S-05 bloqué en état B", 9]],
  modele:`## 3.2 Pare-feu et segmentation
### État A : trois zones
À COMPLÉTER — OPNsense (version), VLAN 10 et 20, NAT FA1/FA2/FA6, règles FA1 à FA5.
![VLAN état A](captures/parefeu-03-vlan-A.png)
![NAT](captures/parefeu-04-nat.png)
![Règles état A](captures/parefeu-05-regles-A.png)
### État B : six segments au moindre privilège
À COMPLÉTER — VLAN 11 à 13 et 21 à 23, règles F01 à F09 et F99. Expliquer en 3 lignes pourquoi les adresses d'hôtes n'ont pas changé.
![VLAN état B](captures/parefeu-08-vlan-B.png)
![Règles état B](captures/parefeu-08-regles-B.png)
### Moteur de politique (C2) et preuve de conformité
À COMPLÉTER — flows.csv comme source unique, nombre de règles générées, nombre de tests dans tests.csv.
| État | Règles | Tests | Conformes au 1er passage | Conformes au final |
| --- | --- | --- | --- | --- |
| A | À COMPLÉTER | | | 100 % |
| B | À COMPLÉTER | | | 100 % |
![test_flux A](captures/parefeu-07-test-flux-A.png)
![test_flux B](captures/parefeu-09-test-flux-B.png)
### Ce que voit le pare-feu
![Journal](captures/parefeu-05-filterlog-A.png)
![S-05 bloqué en état B](captures/parefeu-09-filterlog-S05.png)
### Difficultés et solutions
À COMPLÉTER

## Annexe B. Sorties complètes de test_flux
\`\`\`
À COMPLÉTER — resultats/A/conformite.txt puis resultats/B/conformite.txt
\`\`\``,
  questions:["Combien de règles en état A et en état B, et pourquoi B en a-t-il davantage ?","Combien de tests test_flux ont échoué au premier passage, et pour quelles causes ?","Quels flux ont dû être ajoutés ou modifiés par rapport à la matrice de conception ?","Pourquoi S-05 est-il invisible pour le pare-feu en état A et bloqué en état B (montrer la ligne de journal) ?","Quel coût de la micro-segmentation : complexité, nombre de règles, risque d'erreur ?"]
}));

R.services.steps.push(contrib("services", {
  quand:"au fil des étapes ; texte le 26–28 nov.", section:"3.3 (services applicatifs)",
  partie:"les services protégés, leur déploiement en conteneurs sur les VLAN, et le passage de l'état A à l'état B sans changer d'adresse",
  pre:["services-8"],
  captures:[["services-01-versions.png", "Versions", 1], ["services-02-reseaux-A.png", "Réseaux macvlan état A", 2], ["services-03-sqli-A.png", "Point vulnérable", 3], ["services-04-nginx.png", "nginx en HTTPS", 4], ["services-05-dns.png", "Réponse DNS", 5], ["services-06-docker-ps.png", "Tous les conteneurs", 6], ["services-08-reseaux-B.png", "Réseaux macvlan état B", 8]],
  modele:`## 3.3 Services applicatifs
### Architecture des services
À COMPLÉTER — nginx (DMZ-WEB), API Flask (DMZ-APP), PostgreSQL (LAN-DB), bind9 (DMZ-DNS), postes et attaquants internes ; chaîne nginx → API → base (F01, F03, F04).
![Conteneurs](captures/services-06-docker-ps.png)
![nginx](captures/services-04-nginx.png)
![DNS](captures/services-05-dns.png)
### Raccordement aux VLAN (macvlan)
À COMPLÉTER — un réseau macvlan par VLAN ; de A à B, seuls le masque et la passerelle changent.
![Réseaux état A](captures/services-02-reseaux-A.png)
![Réseaux état B](captures/services-08-reseaux-B.png)
### Point vulnérable volontaire (S-03)
À COMPLÉTER — ce qu'il fait, pourquoi il existe, comment il reste confiné au laboratoire.
![Injection SQL réussie](captures/services-03-sqli-A.png)
### Difficultés et solutions
À COMPLÉTER`,
  questions:["Comment chaque service garde-t-il son adresse entre l'état A et l'état B ?","Quels problèmes macvlan avez-vous rencontrés (par exemple l'hôte qui ne joint pas ses conteneurs) et comment les avez-vous résolus ?","Pourquoi un point d'entrée volontairement vulnérable, et pourquoi ne présente-t-il pas de risque ici ?","Où se trouvent les journaux des services et comment rejoignent-ils le rôle 6 ?"]
}));

R.ids.steps.push(contrib("ids", {
  quand:"au fil des étapes ; texte le 26–28 nov.", section:"3.4 (IDS/IPS) et annexe C (jeu de règles)",
  partie:"l'inspection en ligne : la décision netmap, les règles, le réglage des faux positifs, le passage en IPS et la panne",
  pre:["ids-7", "analyse-6"],
  captures:[["ids-00-tester.png", "Tests hors ligne", 0], ["ids-01-netmap.png", "Test netmap", 1], ["ids-02-config.png", "Configuration", 2], ["ids-04-regles.png", "Règles locales chargées", 4], ["ids-05-alertes.png", "Alertes par scénario", 5], ["ids-06-fp-reglage.png", "Faux positifs avant et après réglage", 6], ["ids-06-policy-drop.png", "Politique drop / alert", 6], ["ids-07-p01.png", "Panne P-01", 7]],
  modele:`## 3.4 IDS/IPS (Suricata)
### Décision de plateforme
À COMPLÉTER — Suricata (version), netmap sur les cartes em de VMware : retenu ou repli (Divert, IDS + pf), avec le résultat chiffré du test.
![Test netmap](captures/ids-01-netmap.png)
![Configuration](captures/ids-02-config.png)
### Règles
À COMPLÉTER — ET Open + règles locales (sid 9000001 à 9000099) ; une ligne par règle locale : scénario visé, principe (seuil, motif).
![Tests hors ligne](captures/ids-00-tester.png)
![Règles chargées](captures/ids-04-regles.png)
![Alertes](captures/ids-05-alertes.png)
### Réglage puis passage en IPS
| Mesure | Avant réglage | Après réglage |
| --- | --- | --- |
| Faux positifs par heure | À COMPLÉTER | |
| Règles ajustées ou désactivées | | |
![Faux positifs](captures/ids-06-fp-reglage.png)
![Politique](captures/ids-06-policy-drop.png)
### Scénario de panne P-01
À COMPLÉTER — comportement réellement observé (trafic transmis ou bloqué), disponibilité pendant la panne, temps de reprise.
![Panne](captures/ids-07-p01.png)
### Difficultés et solutions
À COMPLÉTER

## Annexe C. Jeu de règles
Hash (ids/VERSION.txt) : À COMPLÉTER
\`\`\`
À COMPLÉTER — contenu de ids/local.rules et ids/threshold.config
\`\`\``,
  questions:["netmap a-t-il fonctionné sur les cartes em de VMware ? Sinon, quel repli et quelle conséquence sur les mesures ?","Combien de faux positifs avant et après réglage, et quelles règles avez-vous ajustées et pourquoi ?","Quelles familles sont passées en drop, lesquelles sont restées en alert, et pourquoi ?","Quel coût de l'IPS en latence et en débit (B sans IPS contre B avec IPS) ?","Que se passe-t-il réellement quand Suricata s'arrête, et en combien de temps le service revient-il ?","Qu'est-ce que Suricata ne peut pas voir (trafic chiffré, intra-segment) ?"]
}));

R.attaques.steps.push(contrib("attaques", {
  quand:"au fil des étapes ; texte le 27–28 nov.", section:"4 (protocole et campagnes)",
  partie:"le protocole expérimental et le déroulement réel des deux campagnes : c'est ce qui rend les résultats du rôle 6 crédibles",
  pre:["attaques-6"],
  captures:[["attaques-01-confinement-kali.png", "Confinement de Kali", 1], ["attaques-03-orchestrateur.png", "Orchestrateur en marche", 3], ["attaques-05-S01-A.png", "S-01 en état A", 5], ["attaques-05-S05-A.png", "S-05 réussi en état A", 5], ["attaques-05-fin-A.png", "Fin de campagne A", 5], ["attaques-06-S01-B.png", "S-01 en état B", 6], ["attaques-06-S05-B.png", "S-05 bloqué en état B", 6], ["attaques-06-fin-B.png", "Fin de campagne B", 6]],
  modele:`## 4. Protocole et campagnes
### Protocole
À COMPLÉTER — scénarios (scenarios.yaml), N, ordre aléatoire et graine, pause et réinitialisation entre exécutions, trafic légitime en fond.
![Orchestrateur](captures/attaques-03-orchestrateur.png)
### Confinement
![Kali sans accès à Internet](captures/attaques-01-confinement-kali.png)
### Déroulement réel
| Campagne | Dates | Exécutions prévues | Valides | Relancées | Durée |
| --- | --- | --- | --- | --- | --- |
| A | À COMPLÉTER | 60 | | | |
| B | À COMPLÉTER | 60 | | | |
| P-01 | À COMPLÉTER | 5 | | | |
### Ce que voit l'attaquant
À COMPLÉTER — pour S-01 et S-05, la sortie de l'outil en A puis en B.
![S-01 état A](captures/attaques-05-S01-A.png)
![S-05 état A](captures/attaques-05-S05-A.png)
![S-01 état B](captures/attaques-06-S01-B.png)
![S-05 état B](captures/attaques-06-S05-B.png)
![Fin A](captures/attaques-05-fin-A.png)
![Fin B](captures/attaques-06-fin-B.png)
### Difficultés et solutions
À COMPLÉTER`,
  questions:["Combien d'exécutions valides et invalides dans chaque campagne, et pourquoi certaines ont-elles été relancées ?","Qu'est-ce qui garantit que A et B ont été mesurés dans les mêmes conditions ?","Pour chaque scénario, l'objectif de l'attaque a-t-il été atteint en A, puis en B ?","Comment le confinement des attaques a-t-il été vérifié ?"]
}));

R.analyse.steps.push(contrib("analyse", {
  quand:"27–28 nov.", section:"5 (résultats et analyse) et annexe D (captures commentées)",
  partie:"les résultats chiffrés : c'est la section qui répond à la question du projet, et le résumé du chef en dépend",
  pre:["analyse-7"],
  captures:[["analyse-01-ntp.png", "Synchronisation des horloges", 1], ["analyse-02-rsyslog.png", "Journaux reçus", 2], ["analyse-06-detection.png", "Détection A vs B", 6], ["analyse-06-blocage.png", "Blocage A vs B", 6], ["analyse-06-latence.png", "Latence et disponibilité", 6]],
  modele:`## 5. Résultats et analyse
### Fiabilité des mesures
À COMPLÉTER — horloges synchronisées (décalage mesuré), journaux complets, fenêtre de corrélation [t0, t1 + 10 s].
![NTP](captures/analyse-01-ntp.png)
![Journaux](captures/analyse-02-rsyslog.png)
### Indicateurs par scénario
| Scénario | Détection A | Détection B | Blocage A | Blocage B | TTD médian B (ms) |
| --- | --- | --- | --- | --- | --- |
| S-01 à S-06 | À COMPLÉTER (depuis resultats/synthese/) | | | | |
![Détection](captures/analyse-06-detection.png)
![Blocage](captures/analyse-06-blocage.png)
### Coût de la protection
À COMPLÉTER — faux positifs par heure, disponibilité, latence : A, B sans IPS, B avec IPS.
![Latence et disponibilité](captures/analyse-06-latence.png)
### Hypothèses de la conception : confirmées ou infirmées
| Scénario | Attendu (section 10.1) | Mesuré | Verdict |
| --- | --- | --- | --- |
| À COMPLÉTER | | | |
### Panne P-01
À COMPLÉTER — disponibilité et détection avant, pendant et après la panne.
### Biais et limites des mesures
À COMPLÉTER

## Annexe D. Captures Wireshark commentées
À COMPLÉTER — reprendre rapport/captures/ de l'étape 7 (4 à 6 captures, une légende de 2 à 3 lignes chacune).`,
  questions:["Pour chaque scénario, l'hypothèse « attendu » de la conception est-elle confirmée ? Avec quel chiffre ?","Quel résultat vous a surpris, et comment l'expliquez-vous ?","Quel coût de la protection : faux positifs, disponibilité, latence ?","Quelles limites de la mesure (N = 10, fenêtre de 10 s, horloges, scénarios connus à l'avance) ?","Que montre P-01 sur le comportement en panne ?"]
}));
})();
