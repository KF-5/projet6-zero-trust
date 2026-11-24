| Interface | Ordre | Flux | Action | Proto | Source | Destination | Ports | Journal | Active |
|---|---|---|---|---|---|---|---|---|---|
| WAN | 1 | FA6 | pass | tcp | 203.0.113.10 | 10.20.0.10 | 5201 | oui | NON (seulement pendant les mesures iperf3) |
| WAN | 2 | FA1 | pass | tcp | any | 10.20.0.10 | 80, 443 | oui | oui |
| WAN | 3 | FA2 | pass | tcp/udp | any | 10.20.0.40 | 53 | oui | oui |
| WAN | 4 | FA5 | block | any | any | any | any | oui | oui |
| DMZ | 1 | FA3 | pass | tcp | 10.20.0.0/24 | 10.10.0.70 | 5432 | oui | oui |
| DMZ | 2 | FA5 | block | any | any | any | any | oui | oui |
| LAN | 1 | FA4 | pass | any | 10.10.0.0/24 | any | any | oui | oui |
| LAN | 2 | FA5 | block | any | any | any | any | oui | oui |
