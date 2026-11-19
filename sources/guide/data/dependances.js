/* Pour chaque étape : peut-on la faire seul, ou faut-il attendre un autre membre ?
   m = mode :
     seul     : indépendant, sur son PC, dès maintenant
     prep     : se prépare dès maintenant sur son PC ; l'installation sur l'hôte attend d'autres étapes
     hote     : sur l'hôte, après les étapes indiquées
     ensemble : sur l'hôte, en séance commune avec d'autres membres
     apres    : sur son PC, mais à partir de données ou de livrables d'autres membres
   n = ce qu'on peut faire seul tout de suite (ou précision) ; avec = rôles présents ;
   attend = étapes d'autres rôles (par défaut : les prérequis de l'étape qui appartiennent à un autre rôle). */
G.DEP = {
 "chef-1":{m:"hote", n:"Ne dépend d'aucun membre, seulement du choix du PC hôte par le groupe. C'est l'étape qui débloque tout le monde."},
 "chef-2":{m:"hote", n:"Enchaîne directement après l'étape 1."},
 "chef-3":{m:"hote", n:"Téléchargez les images ISO dès maintenant pour gagner du temps."},
 "chef-4":{m:"hote", n:"Fait par le rôle 1 le 14-16 novembre, OPNsense compris (p6-opnsense). Les membres 2, 4 et 6 vérifient ce qui a été fait dans leurs premières étapes."},
 "chef-5":{m:"hote"},
 "chef-6":{m:"seul", n:"Le dépôt Git ne dépend pas de l'hôte : créez-le aujourd'hui, tout le groupe en a besoin pour travailler en parallèle.", attend:[]},
 "chef-7":{m:"hote", n:"Se fait deux fois : etat-A, puis etat-B."},
 "chef-8":{m:"seul", n:"Coordination : commence dès aujourd'hui (planning, points d'avancement)."},
 "chef-9":{m:"apres", n:"Captures au fil des étapes ; le résumé chiffré attend les résultats du membre 6."},
 "chef-10":{m:"apres", n:"Attend la contribution de chaque membre (le 28)."},

 "parefeu-1":{m:"hote", n:"En attendant : entraînez-vous sur OPNsense dans VirtualBox sur votre PC."},
 "parefeu-2":{m:"hote"},
 "parefeu-3":{m:"hote", avec:["services"], n:"Le test des VLAN se fait avec un conteneur du membre 4."},
 "parefeu-4":{m:"hote"},
 "parefeu-5":{m:"hote", n:"La journalisation suppose que le collecteur du membre 6 reçoit déjà le syslog."},
 "parefeu-6":{m:"seul", n:"Du pur Python à partir des fichiers de la page Contrats : à faire dès maintenant."},
 "parefeu-7":{m:"ensemble", avec:["services"], n:"test_flux a besoin de tous les conteneurs du membre 4."},
 "parefeu-8":{m:"hote", n:"Interdit avant que l'état A soit figé et la campagne A terminée."},
 "parefeu-9":{m:"ensemble", avec:["services"]},
 "parefeu-10":{m:"apres", n:"Captures au fil des étapes ; texte rédigé seul à la fin."},

 "services-1":{m:"hote", n:"En attendant : construisez tout le docker-compose sur votre PC."},
 "services-2":{m:"hote", avec:["parefeu"], n:"Les VLAN 10 et 20 doivent exister sur OPNsense."},
 "services-3":{m:"prep", n:"Écrivez et testez l'API Flask et PostgreSQL sur votre PC avec Docker dès maintenant ; sur l'hôte, il ne restera qu'à les déployer."},
 "services-4":{m:"prep", n:"Configuration nginx (TLS, reverse proxy) testable sur votre PC dès maintenant."},
 "services-5":{m:"prep", n:"Zone bind9 testable sur votre PC (dig) dès maintenant."},
 "services-6":{m:"hote"},
 "services-7":{m:"prep", n:"capture.sh s'écrit et se teste sur votre PC avec des conteneurs locaux."},
 "services-8":{m:"ensemble", avec:["parefeu"], n:"Bascule en même temps que les VLAN B du membre 2."},
 "services-9":{m:"apres", n:"Captures au fil des étapes ; texte rédigé seul à la fin."},

 "attaques-1":{m:"hote", n:"En attendant : entraînez-vous avec Kali (ou un conteneur nmap/hping3) contre les conteneurs du membre 4 sur votre PC."},
 "attaques-2":{m:"prep", n:"scenarios.yaml s'écrit dès maintenant à partir de la section 10.1 du rapport."},
 "attaques-3":{m:"prep", n:"Écrivez et testez l'orchestrateur sur votre PC contre des conteneurs locaux ; le branchement final attend l'hôte."},
 "attaques-4":{m:"ensemble", avec:["parefeu", "services", "ids", "analyse"], n:"Répétition générale : tous les composants doivent tourner."},
 "attaques-5":{m:"ensemble", avec:["services", "ids", "analyse"], n:"Le membre 4 garde les services en marche, le membre 3 la sonde, le membre 6 surveille la collecte."},
 "attaques-6":{m:"ensemble", avec:["ids", "services", "analyse"]},
 "attaques-7":{m:"apres", n:"Captures au fil des étapes ; texte rédigé seul à la fin."},

 "ids-0":{m:"seul", n:"Mini-laboratoire Docker et Suricata sur votre PC : à faire dès maintenant."},
 "ids-1":{m:"ensemble", avec:["parefeu"], n:"Test netmap avec le membre 2 : OPNsense doit être installé et configuré."},
 "ids-2":{m:"hote"},
 "ids-3":{m:"prep", n:"Le script de la sonde s'écrit et se teste sur votre PC contre un nginx local ; le déploiement attend les postes du membre 4."},
 "ids-4":{m:"prep", n:"Les règles sont prêtes grâce à l'étape 0 ; leur chargement sur OPNsense attend l'état B."},
 "ids-5":{m:"ensemble", avec:["attaques"], n:"Le membre 5 lance les scénarios pendant que vous regardez les alertes."},
 "ids-6":{m:"ensemble", avec:["services", "analyse"], n:"2 h de trafic légitime (sonde, services du membre 4) et calcul des faux positifs par le membre 6."},
 "ids-7":{m:"hote", n:"Les commandes de la panne se préparent sur votre PC ; l'essai se fait sur l'hôte."},
 "ids-8":{m:"apres", n:"Captures au fil des étapes ; le graphique des faux positifs vient du membre 6."},

 "analyse-1":{m:"hote"},
 "analyse-2":{m:"hote", n:"Configuration rsyslog préparable sur votre PC."},
 "analyse-3":{m:"prep", n:"Le parseur s'écrit dès maintenant avec des lignes d'exemple (filterlog, EVE JSON) ; il sera branché sur le vrai collecteur ensuite."},
 "analyse-4":{m:"prep", n:"Outils prêts dès maintenant ; les vraies captures arrivent avec les campagnes."},
 "analyse-5":{m:"prep", n:"La corrélation s'écrit et se teste avec des données fictives (runs.csv, events.csv) dès maintenant ; le vrai calcul attend la campagne A."},
 "analyse-6":{m:"apres", n:"Sur votre PC, à partir des données archivées dans resultats/."},
 "analyse-7":{m:"apres", n:"Sur votre PC, à partir des PCAP archivés."},
 "analyse-8":{m:"apres", n:"Texte rédigé seul à la fin."}
};
G.DEPMODE = {
 seul:     {l:"Indépendant",            d:"Sur ton PC, dès maintenant, sans attendre personne."},
 prep:     {l:"Préparable maintenant",  d:"Le gros du travail se fait dès maintenant sur ton PC ; l'installation sur l'hôte attend d'autres étapes."},
 hote:     {l:"Sur l'hôte",             d:"Se fait sur l'hôte, une fois les étapes ci-dessous terminées."},
 ensemble: {l:"En séance commune",      d:"Se fait sur l'hôte, en même temps que d'autres membres."},
 apres:    {l:"Après les autres",       d:"Sur ton PC, mais à partir de données ou de livrables d'autres membres."}
};
