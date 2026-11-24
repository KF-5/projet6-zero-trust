#!/usr/bin/env bash
# Rejoue chaque capture dans Suricata et compare les sid obtenus aux sid attendus.
cd ~/projet6/ids
declare -A ATTENDU=([s01]="9000001" [s02]="9000003" [s03]="9000010" [s05]="9000020" [s06]="9000030" [legitime]="")
for cap in s01 s02 s03 s05 s06 legitime; do
  [ -f pcap/${cap}.pcap ] || { echo "${cap} : capture absente"; continue; }
  rm -rf out/${cap} && mkdir -p out/${cap}
  suricata -r pcap/${cap}.pcap -S rules/local.rules -k none -l out/${cap} \
    --set vars.address-groups.HOME_NET="[10.0.0.0/8,203.0.113.1/32]" > /dev/null 2>&1
  obtenu=$(jq -r 'select(.event_type=="alert") | .alert.signature_id' out/${cap}/eve.json 2>/dev/null | sort -u | xargs)
  [ "${obtenu}" = "${ATTENDU[${cap}]}" ] && etat="OK" || etat="ÉCART"
  nombre=$(jq -r 'select(.event_type=="alert") | .alert.signature_id' out/${cap}/eve.json 2>/dev/null | sort | uniq -c | awk '{printf "%s×%s ", $2, $1}')
  printf '%-9s attendu [%s]  obtenu [%s]  %s   alertes : %s\n' "${cap}" "${ATTENDU[${cap}]}" "${obtenu}" "${etat}" "${nombre:-0}"
done
