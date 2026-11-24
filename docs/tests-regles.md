# Tests hors ligne des règles locales (rôle 3, étape 6)

Date : 24 novembre 2026. Poste : PC du rôle 3, WSL2 + Ubuntu 24.04.5, **Suricata 8.0.8** (même branche 8.0 que Suricata sur OPNsense 26.7), jq 1.7, nmap 7.94, tcpdump 4.99.4, Docker 29.1.3 + Compose 2.40.3.

## Principe

Suricata analyse un fichier de capture (`-r`) avec le même moteur que le trafic en direct. Un mini-laboratoire Docker (`ids/labo/`) reproduit les adresses du vrai laboratoire (203.0.113.1, 10.20.0.70, 10.10.0.70 ; attaquant en 203.0.113.66, 10.20.0.80 et 10.10.0.12) sur trois réseaux isolés d'Internet (`internal: true`). `ids/capturer.sh` enregistre une capture par scénario ; `ids/tester.sh` rejoue chaque capture avec les seules règles locales (`HOME_NET = [10.0.0.0/8, 203.0.113.1/32]`) et compare les sid obtenus aux sid attendus.

## Résultat final

```
s01       attendu [9000001]  obtenu [9000001]  OK   alertes : 9000001×1
s02       attendu [9000003]  obtenu [9000003]  OK   alertes : 9000003×3
s03       attendu [9000010]  obtenu [9000010]  OK   alertes : 9000010×1
s05       attendu [9000020]  obtenu [9000020]  OK   alertes : 9000020×1
s06       attendu [9000030]  obtenu [9000030]  OK   alertes : 9000030×1
legitime  attendu []  obtenu []  OK   alertes : 0
```

| Capture | Attaque (depuis le conteneur attaquant) | Lecture |
| --- | --- | --- |
| s01 | `nmap -sS -Pn -p 1-1024 203.0.113.1` | une alerte de scan (seuil 20 SYN en 5 s, une alerte par fenêtre) |
| s02 | `hping3 -S --flood -p 443 203.0.113.1` pendant 3 s (capture de 55 Mo, non versionnée) | une alerte par seconde de flood (seuil 100 SYN/s) |
| s03 | `curl "http://10.20.0.70:5000/api/produit?id=1'%20OR%20'1'='1"` | injection reconnue dans l'URL décodée |
| s05 | `nmap -sS -Pn -p 5432 10.10.0.70` depuis 10.10.0.12 | un poste du LAN vise la base |
| s06 | SYN vers 443 découpé en fragments de 8 octets (capture **synthétique**, voir plus bas) | petits fragments détectés |
| legitime | 1 requête HTTPS par seconde vers 203.0.113.1:443 pendant 2 min (comme la sonde C8) | **aucune alerte** |

## Écarts trouvés et corrigés

1. **Règles « compteurs » du blocage temporaire (9000002 et 9000004).** Dans leur version du guide (`threshold:type limit, count 1, seconds 20`), chacune levait une alerte dès un seul SYN. Premier passage : s01 déclenchait aussi 9000004 (un SYN du scan vers 443). Conséquence dans le vrai laboratoire : la sonde (1 requête HTTPS/s vers 443) aurait produit une alerte toutes les 20 s, soit environ **180 faux positifs par heure** (critère : au plus 1). La capture légitime initiale ne le montrait pas, car elle utilisait HTTP sur le port 80 ; elle a été refaite en HTTPS vers 443.
   - Correction : `noalert` sur 9000002 (rev 3) et 9000004 (rev 2). Elles comptent toujours chaque SYN pour le `rate_filter` de `threshold.config`, sans écrire d'alerte ; les alertes viennent de 9000001 et 9000003.
   - Variantes écartées, testées en IPS simulé (`suricata --simulate-ips`, Suricata 7.0.3) : `detection_filter` dans la règle (une entrée de `threshold.config` le remplace : 6 alertes en 2 min de trafic légitime ; sans `threshold.config` : 2 900 alertes pour un flood).
   - Vérification de la solution retenue en IPS simulé : flood de 3 000 SYN → 2 900 paquets bloqués, 1 alerte ; scan 1-1024 → 1 001 bloqués, 1 alerte ; 120 SYN légitimes → 0 bloqué, 0 alerte. **À confirmer sur OPNsense** (étape 12) : le blocage étant silencieux, il se lit dans `stats.log` (compteur `ips.blocked`) et par l'échec de l'attaque.
2. **S-06 (fragments).** Dans le mini-laboratoire, `nmap -f` arrive recollé sur le pont Docker (`tcpdump` montre un SYN entier) : la règle n'a rien à voir. La capture `pcap/s06.pcap` est donc **synthétique** (scapy : SYN vers 443 en 3 fragments de 8, 8 et 4 octets, comme `nmap -f`). Elle déclenche 9000030. **À valider sur le laboratoire**, de Kali vers em0.

## Limites

- Le mini-laboratoire n'a ni NAT ni TLS : nginx ne répond pas en HTTPS, mais les SYN vers 443 partent, et ce sont eux que les règles regardent.
- Seules les règles locales sont testées ; ET Open sera mesuré sur 2 h de trafic légitime réel (étape 11).
