| Interface | Ordre | Flux | Action | Proto | Source | Destination | Ports | Journal | Active |
|---|---|---|---|---|---|---|---|---|---|
| DMZ-WEB | 1 | F03 | pass | tcp | 10.20.0.10 | 10.20.0.70 | 5000 | oui | oui |
| DMZ-WEB | 2 | F99 | block | any | any | any | any | oui | oui |
| DMZ-APP | 1 | F04 | pass | tcp | 10.20.0.70 | 10.10.0.70 | 5432 | oui | oui |
| DMZ-APP | 2 | F99 | block | any | any | any | any | oui | oui |
| LAN-ADMIN | 1 | F07 | pass | tcp | 10.10.0.100 | 10.20.0.10, 10.20.0.40, 10.20.0.70, 10.10.0.70 | 22 | oui | oui |
| LAN-ADMIN | 2 | F08 | pass | tcp | 10.10.0.100 | 10.10.0.97 | 443 | oui | oui |
| LAN-ADMIN | 3 | F99 | block | any | any | any | any | oui | oui |
| WAN | 1 | F09 | pass | tcp | 203.0.113.10 | 10.20.0.10 | 5201 | oui | NON (seulement pendant les mesures iperf3) |
| WAN | 2 | F01 | pass | tcp | any | 10.20.0.10 | 80, 443 | oui | oui |
| WAN | 3 | F02 | pass | tcp/udp | any | 10.20.0.40 | 53 | oui | oui |
| WAN | 4 | F99 | block | any | any | any | any | oui | oui |
| LAN-USERS | 1 | F05 | pass | tcp | 10.10.0.0/26 | 10.20.0.10 | 443 | oui | oui |
| LAN-USERS | 2 | F06 | pass | udp | 10.10.0.0/26 | 10.20.0.40 | 53 | oui | oui |
| LAN-USERS | 3 | F99 | block | any | any | any | any | oui | oui |
| DMZ-DNS | 1 | F99 | block | any | any | any | any | oui | oui |
| LAN-DB | 1 | F99 | block | any | any | any | any | oui | oui |
