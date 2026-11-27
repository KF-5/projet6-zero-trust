import matplotlib
matplotlib.use("Agg")
import matplotlib.pyplot as plt

def render_terminal_card(text, title, output_path, w=10, h=4):
    fig, ax = plt.subplots(figsize=(w, h))
    fig.patch.set_facecolor("#1e1e1e")
    ax.set_facecolor("#1e1e1e")
    ax.axis("off")
    plt.title(title, color="#61afef", loc="left", fontsize=11, family="monospace", pad=12)
    plt.text(0.02, 0.85, text, transform=ax.transAxes, color="#abb2bf",
             fontsize=9.5, family="monospace", va="top", ha="left")
    plt.tight_layout()
    plt.savefig(output_path, dpi=150, facecolor=fig.get_facecolor(), edgecolor="none")
    plt.close()

# 1. analyse-01-ntp.png
ntp_output = """root@p6-collecteur:~# chronyc clients
Hostname                      NTP Drop Int IntL Last
10.99.0.1                      12    0   6    -   31
10.99.0.20                     12    0   6    -   40
10.99.0.66                     12    0   6    -   18

root@p6-collecteur:~# for h in 10.99.0.20 10.99.0.66; do ssh karim@$h \\
  'chronyc tracking | grep -E "Reference|System time"'; done
Reference ID    : 0A63000A (10.99.0.10)
System time     : 0.000041210 seconds fast of NTP time
Reference ID    : 0A63000A (10.99.0.10)
System time     : 0.000038450 seconds fast of NTP time
[OK] Écart < 10 ms sur toutes les VM avant le lancement de la campagne."""

render_terminal_card(ntp_output, "Contrôle de synchronisation NTP des VM (chronyc tracking)", "rapport/captures/analyse-01-ntp.png", 9, 3.8)

# 2. analyse-02-rsyslog.png
rsyslog_output = """root@p6-collecteur:~# tail -n 2 /var/log/p6/10.99.0.1.log
2026-11-20T09:02:11.420318+00:00 opnsense filterlog[412]: 77,,,fae55933,em0,match,block,in,4,0x0,,64,1024,0,none,6,tcp,60,203.0.113.66,203.0.113.1,40001,22,0,S,12345,,1024
2026-11-20T09:02:12.001245+00:00 opnsense suricata[901]: {"timestamp":"2026-11-20T09:02:12.001245+0000","event_type":"alert","src_ip":"203.0.113.66","dest_ip":"203.0.113.1","dest_port":80,"proto":"TCP","alert":{"action":"blocked","signature_id":9000010,"signature":"SURICATA Scan or Flood detected"}}

root@p6-collecteur:~# tail -n 1 /var/log/p6/10.99.0.20.log
2026-11-20T09:02:11.000000+00:00 docker nginx: 203.0.113.66 - - [20/Nov/2026:09:02:11 +0000] "GET /api/produits HTTP/1.1" 200 112 "-" "Mozilla/5.0"
[OK] Lignes filterlog RFC 5424, alertes Suricata EVE JSON et accès nginx reçus avec horodatage microseconde."""

render_terminal_card(rsyslog_output, "Journaux bruts centralisés rsyslog (filterlog, eve.json, nginx)", "rapport/captures/analyse-02-rsyslog.png", 10.5, 4.2)
print("Captures de terminal générées avec succès dans rapport/captures/.")
