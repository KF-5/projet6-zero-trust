# -*- coding: utf-8 -*-
"""Projet 6 - panneau de controle des VM du labo (remplace l'interface de Proxmox).

- Pilote UNIQUEMENT les 4 VM du labo, avec l'outil officiel vmrun de VMware Workstation.
- Ecoute UNIQUEMENT sur 127.0.0.1:8080 : on l'atteint par le tunnel SSH (compte p6labo).
- Un compte par membre. Role "membre" : voir, demarrer, prendre un instantane.
  Role "admin" : en plus arreter, redemarrer, suspendre, revenir a un instantane.
- Chaque action est journalisee dans journal.log (date, utilisateur, action, VM, resultat).

Usage (dans le dossier du panneau, avec le Python de .venv) :
  python panneau.py serve
  python panneau.py utilisateur ajouter <nom> [--admin]
  python panneau.py utilisateur supprimer <nom>
  python panneau.py utilisateur liste
"""
import datetime
import getpass
import json
import os
import re
import secrets
import subprocess
import sys
import threading
import time
from functools import wraps

from flask import Flask, abort, flash, redirect, render_template_string, request, session, url_for
from werkzeug.security import check_password_hash, generate_password_hash

DOSSIER = os.path.dirname(os.path.abspath(__file__))
VMRUN = r"C:\Program Files\VMware\VMware Workstation\vmrun.exe"
FICHIER_UTILISATEURS = os.path.join(DOSSIER, "utilisateurs.json")
FICHIER_SECRET = os.path.join(DOSSIER, "secret.key")
FICHIER_JOURNAL = os.path.join(DOSSIER, "journal.log")
ECOUTE, PORT = "127.0.0.1", 8080

# Liste blanche : seules ces VM peuvent etre pilotees (jamais de chemin fourni par l'utilisateur)
VMS = {
    "opnsense":   {"nom": "p6-opnsense",   "role": "Pare-feu (C1)",                "vmx": r"D:\Labo-P6\vm\p6-opnsense\p6-opnsense.vmx",     "vnc": 5901},
    "docker":     {"nom": "p6-docker",     "role": "Services de l'état A (C5, C8)", "vmx": r"D:\Labo-P6\vm\p6-docker\p6-docker.vmx",         "vnc": 5902},
    "collecteur": {"nom": "p6-collecteur", "role": "NTP + journaux (C9)",          "vmx": r"D:\Labo-P6\vm\p6-collecteur\p6-collecteur.vmx", "vnc": 5903},
    "kali":       {"nom": "p6-kali",       "role": "Attaquant externe (C7)",       "vmx": r"D:\Labo-P6\vm\p6-kali\p6-kali.vmx",             "vnc": 5904},
}
ACTIONS = {  # action -> (role minimum, arguments vmrun, libelle)
    "demarrer":   ("membre", ["start", "{vmx}", "nogui"], "Démarrer"),
    "instantane": ("membre", None,                         "Instantané"),
    "arreter":    ("admin",  ["stop", "{vmx}", "soft"],     "Arrêter"),
    "redemarrer": ("admin",  ["reset", "{vmx}", "soft"],    "Redémarrer"),
    "suspendre":  ("admin",  ["suspend", "{vmx}", "soft"],  "Suspendre"),
    "revenir":    ("admin",  None,                         "Retour à un instantané"),
}

app = Flask(__name__)
verrous = {cle: threading.Lock() for cle in VMS}
en_cours = {}          # cle VM -> description de l'operation en cours
echecs_connexion = {}  # nom -> (nombre, heure du dernier echec)


# ---------------------------------------------------------------- utilitaires
def journal(utilisateur, action, vm, resultat):
    ligne = "{} | {} | {} | {} | {}\n".format(
        datetime.datetime.now().isoformat(timespec="seconds"), utilisateur, action, vm, resultat)
    with open(FICHIER_JOURNAL, "a", encoding="utf-8") as f:
        f.write(ligne)


def charger_utilisateurs():
    if not os.path.exists(FICHIER_UTILISATEURS):
        return {}
    with open(FICHIER_UTILISATEURS, encoding="utf-8") as f:
        return json.load(f)


def enregistrer_utilisateurs(u):
    tmp = FICHIER_UTILISATEURS + ".tmp"
    with open(tmp, "w", encoding="utf-8") as f:
        json.dump(u, f, indent=2, ensure_ascii=False)
    os.replace(tmp, FICHIER_UTILISATEURS)


def vmrun(*args, timeout=180):
    """Appelle vmrun sans shell, avec une liste d'arguments. Renvoie (ok, texte)."""
    try:
        r = subprocess.run([VMRUN, "-T", "ws", *args], capture_output=True, text=True,
                           timeout=timeout, creationflags=getattr(subprocess, "CREATE_NO_WINDOW", 0))
        sortie = (r.stdout + r.stderr).strip()
        return r.returncode == 0, sortie
    except subprocess.TimeoutExpired:
        return False, "delai depasse"
    except OSError as e:
        return False, "vmrun introuvable : {}".format(e)


def vm_en_marche():
    ok, sortie = vmrun("list", timeout=30)
    if not ok:
        return set()
    return {l.strip().lower() for l in sortie.splitlines()[1:] if l.strip()}


def instantanes(vmx):
    ok, sortie = vmrun("listSnapshots", vmx, timeout=30)
    if not ok:
        return []
    return [l.strip() for l in sortie.splitlines()[1:] if l.strip()]


# Cache partage : la page interroge le panneau toutes les 3 s ; on evite de relancer vmrun a chaque fois
_cache = {"marche": (0.0, set()), "snaps": {}}
_cache_verrou = threading.Lock()
DUREE_MARCHE, DUREE_SNAPS = 2.5, 60.0


def etat_vms():
    """Renvoie (VM en marche, instantanes par VM), en reutilisant les resultats recents."""
    maintenant = time.monotonic()
    with _cache_verrou:
        quand, marche = _cache["marche"]
        if maintenant - quand > DUREE_MARCHE:
            marche = vm_en_marche()
            _cache["marche"] = (time.monotonic(), marche)
        snaps = {}
        for cle, vm in VMS.items():
            quand, liste = _cache["snaps"].get(cle, (0.0, None))
            if liste is None or maintenant - quand > DUREE_SNAPS:
                liste = instantanes(vm["vmx"])
                _cache["snaps"][cle] = (time.monotonic(), liste)
            snaps[cle] = liste
    return marche, snaps


def invalider_cache(cle=None):
    with _cache_verrou:
        _cache["marche"] = (0.0, set())
        if cle:
            _cache["snaps"].pop(cle, None)


def nom_instantane_valide(nom):
    return bool(re.fullmatch(r"[A-Za-z0-9 _.\-]{1,60}", nom or ""))


def secret_cle():
    if not os.path.exists(FICHIER_SECRET):
        with open(FICHIER_SECRET, "w") as f:
            f.write(secrets.token_hex(32))
    with open(FICHIER_SECRET) as f:
        return f.read().strip()


# ---------------------------------------------------------------- securite web
def connexion_requise(fonction):
    @wraps(fonction)
    def enveloppe(*a, **k):
        if "utilisateur" not in session:
            return redirect(url_for("connexion"))
        if session["utilisateur"] not in charger_utilisateurs():
            session.clear()
            return redirect(url_for("connexion"))
        return fonction(*a, **k)
    return enveloppe


@app.before_request
def verifier_csrf():
    if request.method == "POST":
        jeton = session.get("csrf")
        if not jeton or not secrets.compare_digest(jeton, request.form.get("csrf", "")):
            abort(400)


@app.after_request
def entetes(r):
    r.headers["X-Frame-Options"] = "DENY"
    r.headers["X-Content-Type-Options"] = "nosniff"
    r.headers["Referrer-Policy"] = "no-referrer"
    r.headers["Cache-Control"] = "no-store"
    return r


def jeton_csrf():
    if "csrf" not in session:
        session["csrf"] = secrets.token_hex(16)
    return session["csrf"]


app.jinja_env.globals["csrf"] = jeton_csrf


# ---------------------------------------------------------------- pages
GABARIT = """<!doctype html><html lang="fr"><head><meta charset="utf-8">
<meta name="viewport" content="width=device-width,initial-scale=1">
<title>Labo P6 · Panneau</title>
<script>(function(){var t=null;try{t=localStorage.getItem('p6-theme');}catch(e){}
if(t!=='dark'&&t!=='light'){t=(window.matchMedia&&matchMedia('(prefers-color-scheme: dark)').matches)?'dark':'light';}
document.documentElement.setAttribute('data-theme',t);})();</script>
<style>
:root{color-scheme:light;--bg:#f4f6fb;--carte:#fff;--texte:#0f172a;--doux:#64748b;--bord:#e2e8f0;--accent:#2563eb;--accent-f:#1d4ed8;
--vert:#16a34a;--vert-f:#dcfce7;--gris-f:#f1f5f9;--ambre:#b45309;--ambre-f:#fef3c7;--rouge:#dc2626;--rouge-f:#fee2e2;--dang-b:#fecaca;
--entete:rgba(255,255,255,.9);--bouton:#fff;--survol:#f8fafc;--survol-b:#cbd5e1;--avatar-f:#e0e7ff;--avatar-t:#3730a3;
--role-f:#eef2ff;--role-t:#4338ca;--flash-f:#eff6ff;--flash-b:#bfdbfe;--flash-t:#1e3a8a;--lance-f:#dbeafe;--lance-t:#1d4ed8;
--focus:#bfdbfe;--ombre:0 1px 2px rgba(15,23,42,.04),0 6px 20px rgba(15,23,42,.04)}
html[data-theme="dark"]{color-scheme:dark;--bg:#0b1120;--carte:#111827;--texte:#e5e7eb;--doux:#94a3b8;--bord:#263244;--accent:#3b82f6;--accent-f:#2563eb;
--vert:#4ade80;--vert-f:rgba(34,197,94,.14);--gris-f:#1b2536;--ambre:#fbbf24;--ambre-f:rgba(245,158,11,.14);--rouge:#f87171;--rouge-f:rgba(239,68,68,.14);--dang-b:#7f1d1d;
--entete:rgba(13,19,33,.88);--bouton:#0f172a;--survol:#1b2536;--survol-b:#3b4a61;--avatar-f:#312e81;--avatar-t:#c7d2fe;
--role-f:#1e1b4b;--role-t:#a5b4fc;--flash-f:#172554;--flash-b:#1e3a8a;--flash-t:#bfdbfe;--lance-f:#1e3a8a;--lance-t:#bfdbfe;
--focus:#1d4ed8;--ombre:0 1px 2px rgba(0,0,0,.3)}
*{box-sizing:border-box}
body{margin:0;font:15px/1.5 "Segoe UI",system-ui,-apple-system,sans-serif;background:var(--bg);color:var(--texte);transition:background .2s,color .2s}
header{position:sticky;top:0;z-index:5;background:var(--entete);backdrop-filter:blur(8px);
border-bottom:1px solid var(--bord);padding:12px 28px;display:flex;align-items:center;justify-content:space-between;gap:12px}
.marque{display:flex;align-items:center;gap:12px;font-weight:650}
.logo{width:34px;height:34px;border-radius:9px;background:linear-gradient(135deg,#2563eb,#7c3aed);color:#fff;
display:grid;place-items:center;font-size:13px;font-weight:800;letter-spacing:.5px}
.sous{font-size:12px;color:var(--doux);font-weight:400}
.droite{display:flex;align-items:center;gap:10px}
.puce-user{display:flex;align-items:center;gap:10px;font-size:14px}
.avatar{width:30px;height:30px;border-radius:50%;background:var(--avatar-f);color:var(--avatar-t);display:grid;place-items:center;font-weight:700;font-size:13px}
.role{font-size:11px;padding:2px 8px;border-radius:99px;background:var(--role-f);color:var(--role-t);font-weight:600;text-transform:uppercase;letter-spacing:.4px}
.icone{width:36px;height:36px;padding:0;display:grid;place-items:center;font-size:17px;line-height:1}
main{max-width:1180px;margin:0 auto;padding:24px 28px 40px}
.barre-maj{display:flex;justify-content:flex-end;align-items:center;gap:7px;margin:-8px 0 10px;font-size:12px;color:var(--doux)}
.barre-maj i{width:7px;height:7px;border-radius:50%;background:var(--vert)}
.barre-maj.ko i{background:var(--rouge)}
.resume{display:grid;grid-template-columns:repeat(auto-fit,minmax(180px,1fr));gap:14px;margin-bottom:22px}
.tuile{background:var(--carte);border:1px solid var(--bord);border-radius:14px;padding:14px 18px}
.tuile b{display:block;font-size:24px;line-height:1.2}.tuile span{color:var(--doux);font-size:13px}
.grille{display:grid;grid-template-columns:repeat(auto-fit,minmax(470px,1fr));gap:18px}
.carte{background:var(--carte);border:1px solid var(--bord);border-radius:16px;padding:18px 20px;box-shadow:var(--ombre)}
.tete{display:flex;justify-content:space-between;align-items:flex-start;gap:10px}
.tete h2{margin:0;font-size:18px}.tete p{margin:2px 0 0;color:var(--doux);font-size:13px}
.etat{display:inline-flex;align-items:center;gap:7px;padding:4px 11px;border-radius:99px;font-size:12.5px;font-weight:600;white-space:nowrap}
.etat i{width:8px;height:8px;border-radius:50%;background:currentColor}
.on{background:var(--vert-f);color:var(--vert)}.on i{box-shadow:0 0 0 3px rgba(22,163,74,.18)}
.off{background:var(--gris-f);color:var(--doux)}.busy{background:var(--ambre-f);color:var(--ambre)}
.busy i{animation:p 1s infinite}@keyframes p{50%{opacity:.25}}
.meta{display:flex;gap:16px;margin:12px 0 14px;font-size:12.5px;color:var(--doux)}
.meta span{background:var(--gris-f);padding:3px 9px;border-radius:7px}
.bloc{border-top:1px dashed var(--bord);padding-top:12px;margin-top:12px}
.etiq{font-size:11.5px;text-transform:uppercase;letter-spacing:.5px;color:var(--doux);font-weight:600;margin-bottom:7px}
.rang{display:flex;flex-wrap:wrap;gap:8px;align-items:center}
form{display:inline-flex;gap:8px;margin:0}
button,.btn{font:inherit;font-size:13.5px;font-weight:600;border-radius:10px;padding:7px 14px;cursor:pointer;
border:1px solid var(--bord);background:var(--bouton);color:var(--texte);transition:.15s}
button:hover:not(:disabled){border-color:var(--survol-b);background:var(--survol)}
button:disabled{opacity:.4;cursor:not-allowed}
.prim{background:var(--accent);border-color:var(--accent);color:#fff}.prim:hover:not(:disabled){background:var(--accent-f);border-color:var(--accent-f)}
.dang{color:var(--rouge);border-color:var(--dang-b)}.dang:hover:not(:disabled){background:var(--rouge-f)}
input,select{font:inherit;font-size:13.5px;padding:7px 11px;border:1px solid var(--bord);border-radius:10px;background:var(--bouton);color:var(--texte);min-width:0}
input::placeholder{color:var(--doux)}
input:focus,select:focus{outline:2px solid var(--focus);border-color:var(--accent)}
.flash{background:var(--flash-f);border:1px solid var(--flash-b);color:var(--flash-t);padding:10px 14px;border-radius:12px;margin-bottom:16px;font-size:14px}
.journal{margin-top:22px}.journal h3{margin:0 0 10px;font-size:16px}
table{width:100%;border-collapse:collapse;font-size:13px}th{color:var(--doux);font-weight:600;text-align:left}
th,td{padding:8px 10px;border-bottom:1px solid var(--bord)}tr:last-child td{border-bottom:0}
.r{padding:2px 9px;border-radius:99px;font-size:12px;font-weight:600}
.r-ok{background:var(--vert-f);color:var(--vert)}.r-ko{background:var(--rouge-f);color:var(--rouge)}
.r-lance{background:var(--lance-f);color:var(--lance-t)}.r-autre{background:var(--gris-f);color:var(--doux)}
.connexion{max-width:380px;margin:9vh auto}.connexion h2{margin:0 0 4px}.connexion p{color:var(--doux);margin:0 0 18px;font-size:14px}
.connexion label{display:block;font-size:13px;font-weight:600;margin:12px 0 5px}.connexion input{width:100%}
.connexion button{width:100%;margin-top:18px;padding:10px}
footer{color:var(--doux);font-size:12px;text-align:center;margin-top:26px}
@media(max-width:560px){.grille{grid-template-columns:1fr}header{padding:12px 16px}main{padding:16px}}
</style></head><body>
<header><div class="marque"><div class="logo">P6</div><div>Laboratoire Zero Trust<div class="sous">Panneau de contrôle des VM</div></div></div>
<div class="droite"><button type="button" id="theme" class="icone" title="Mode sombre / clair" aria-label="Changer de thème">◐</button>
{% if session.get('utilisateur') %}<div class="puce-user"><div class="avatar">{{ session['utilisateur'][:1]|upper }}</div>
{{ session['utilisateur'] }} <span class="role">{{ session['role'] }}</span>
<form method="post" action="{{ url_for('deconnexion') }}"><input type="hidden" name="csrf" value="{{ csrf() }}">
<button>Déconnexion</button></form></div>{% endif %}</div></header><main>
{% for m in get_flashed_messages() %}<div class="flash">{{ m }}</div>{% endfor %}
{{ contenu|safe }}<footer>Accessible uniquement par le tunnel SSH · chaque action est journalisée</footer></main>
<script>
(function(){
  var racine=document.documentElement, bouton=document.getElementById('theme');
  function icone(){bouton.textContent=racine.getAttribute('data-theme')==='dark'?'☀':'☾';}
  icone();
  bouton.addEventListener('click',function(){
    var t=racine.getAttribute('data-theme')==='dark'?'light':'dark';
    racine.setAttribute('data-theme',t);
    try{localStorage.setItem('p6-theme',t);}catch(e){}
    icone();
  });
})();
{% if vivant %}
(function(){
  var zone=document.getElementById('zone'), barre=document.getElementById('barre-maj'), texte=document.getElementById('maj');
  if(!zone){return;}
  var occupe=false;
  function saisieEnCours(){
    var a=document.activeElement;
    return a&&zone.contains(a)&&(a.tagName==='INPUT'||a.tagName==='SELECT');
  }
  function cleChamp(e){return (e.form?e.form.getAttribute('action'):'')+'|'+e.name;}
  function maj(){
    if(occupe||document.hidden||saisieEnCours()){return;}
    occupe=true;
    fetch('{{ url_for("etat") }}',{credentials:'same-origin',cache:'no-store'}).then(function(r){
      if(r.ok&&!r.redirected&&r.headers.get('X-Fragment')==='1'){return r.text();}
      if(r.ok){location.reload();}
      throw new Error('etat');
    }).then(function(html){
      var valeurs={};
      zone.querySelectorAll('input[name=libelle],select[name=instantane]').forEach(function(e){valeurs[cleChamp(e)]=e.value;});
      zone.innerHTML=html;
      zone.querySelectorAll('input[name=libelle],select[name=instantane]').forEach(function(e){
        var k=cleChamp(e);
        if(!(k in valeurs)){return;}
        if(e.tagName==='SELECT'&&![].some.call(e.options,function(o){return o.value===valeurs[k];})){return;}
        e.value=valeurs[k];
      });
      barre.className='barre-maj';
      texte.textContent='À jour · '+new Date().toLocaleTimeString('fr-FR');
    }).catch(function(){
      barre.className='barre-maj ko';
      texte.textContent='Panneau injoignable (tunnel coupé ?)';
    }).then(function(){occupe=false;});
  }
  setInterval(maj,3000);
  document.addEventListener('visibilitychange',function(){if(!document.hidden){maj();}});
})();
{% endif %}
</script></body></html>"""

PAGE_CONNEXION = """<div class="carte connexion"><h2>Connexion</h2><p>Panneau de contrôle du labo P6</p>
<form method="post" style="display:block"><input type="hidden" name="csrf" value="{{ csrf() }}">
<label for="nom">Nom d'utilisateur</label><input id="nom" name="nom" autocomplete="username" required autofocus>
<label for="mdp">Mot de passe</label><input id="mdp" name="mdp" type="password" autocomplete="current-password" required>
<button class="prim">Se connecter</button></form></div>"""

PAGE_ACCUEIL = """
<div class="resume">
<div class="tuile"><b>{{ vms|length }}</b><span>machines virtuelles</span></div>
<div class="tuile"><b style="color:var(--vert)">{{ nb_marche }}</b><span>en marche</span></div>
<div class="tuile"><b>{{ nb_snaps }}</b><span>instantanés au total</span></div>
<div class="tuile"><b>{{ 'admin' if admin else 'membre' }}</b><span>ton rôle</span></div></div>
<div class="grille">
{% for cle, vm in vms.items() %}{% set allumee = vm.vmx.lower() in marche %}{% set occupee = cle in occupees %}
<div class="carte"><div class="tete"><div><h2>{{ vm.nom }}</h2><p>{{ vm.role }}</p></div>
{% if occupee %}<span class="etat busy"><i></i>{{ occupees[cle] }}…</span>
{% elif allumee %}<span class="etat on"><i></i>En marche</span>
{% else %}<span class="etat off"><i></i>Arrêtée</span>{% endif %}</div>
<div class="meta"><span>Console VNC · port {{ vm.vnc }}</span><span>{{ snaps[cle]|length }} instantané(s)</span></div>
<div class="etiq">Alimentation</div><div class="rang">
{% for action, role_min, libelle in boutons %}{% if role_min == 'membre' or admin %}
<form method="post" action="{{ url_for('agir', cle=cle, action=action) }}" {% if role_min=='admin' %}onsubmit="return confirm('{{ libelle }} {{ vm.nom }} ?')"{% endif %}>
<input type="hidden" name="csrf" value="{{ csrf() }}">
<button class="{{ 'prim' if action=='demarrer' else 'dang' }}" {% if occupee or (action=='demarrer' and allumee) or (action!='demarrer' and not allumee) %}disabled{% endif %}>{{ libelle }}</button></form>
{% endif %}{% endfor %}</div>
<div class="bloc"><div class="etiq">Instantanés</div><div class="rang">
<form method="post" action="{{ url_for('agir', cle=cle, action='instantane') }}"><input type="hidden" name="csrf" value="{{ csrf() }}">
<input name="libelle" placeholder="nom (optionnel)" maxlength="30" pattern="[A-Za-z0-9 _.\\-]*" size="16">
<button {% if occupee %}disabled{% endif %}>Prendre un instantané</button></form></div>
{% if admin %}<div class="rang" style="margin-top:8px"><form method="post" action="{{ url_for('agir', cle=cle, action='revenir') }}"
onsubmit="return confirm('Revenir à cet instantané ? L\\'état actuel de {{ vm.nom }} sera perdu.')">
<input type="hidden" name="csrf" value="{{ csrf() }}"><select name="instantane">
{% for s in snaps[cle]|reverse %}<option>{{ s }}</option>{% endfor %}</select>
<button class="dang" {% if occupee or not snaps[cle] %}disabled{% endif %}>Revenir</button></form></div>
{% else %}<div class="sous" style="margin-top:6px">{{ snaps[cle]|reverse|join(' · ') or 'aucun instantané' }}</div>{% endif %}
</div></div>{% endfor %}</div>
<div class="carte journal"><h3>Dernières actions</h3><table>
<tr><th>Date</th><th>Utilisateur</th><th>Action</th><th>VM</th><th>Résultat</th></tr>
{% for l in dernieres %}<tr><td>{{ l[0]|replace('T',' ') }}</td><td>{{ l[1] }}</td><td>{{ l[2] }}</td><td>{{ l[3] }}</td>
<td>{% set r = l[4] if l|length > 4 else '' %}<span class="r {{ 'r-ok' if r=='ok' else ('r-lance' if r=='lance' else ('r-ko' if r.startswith('ECHEC') or r.startswith('REFUSE') else 'r-autre')) }}">{{ r }}</span></td></tr>
{% else %}<tr><td colspan="5" class="sous">Aucune action pour l'instant.</td></tr>{% endfor %}</table></div>"""


def rendre(contenu, vivant=False, **ctx):
    return render_template_string(GABARIT, contenu=render_template_string(contenu, **ctx), vivant=vivant)


@app.route("/connexion", methods=["GET", "POST"])
def connexion():
    if request.method == "POST":
        nom = request.form.get("nom", "").strip().lower()
        nb, dernier = echecs_connexion.get(nom, (0, 0))
        if nb >= 5 and time.time() - dernier < 300:
            flash("Trop d'essais : reessaie dans 5 minutes.")
            return rendre(PAGE_CONNEXION)
        u = charger_utilisateurs().get(nom)
        if u and check_password_hash(u["hash"], request.form.get("mdp", "")):
            echecs_connexion.pop(nom, None)
            session.clear()
            session["utilisateur"], session["role"] = nom, u["role"]
            journal(nom, "connexion", "-", "ok")
            return redirect(url_for("accueil"))
        echecs_connexion[nom] = (nb + 1, time.time())
        journal(nom or "?", "connexion", "-", "ECHEC")
        time.sleep(1)
        flash("Nom ou mot de passe incorrect.")
    return rendre(PAGE_CONNEXION)


@app.route("/deconnexion", methods=["POST"])
def deconnexion():
    session.clear()
    return redirect(url_for("connexion"))


def contexte_accueil():
    lignes = []
    if os.path.exists(FICHIER_JOURNAL):
        with open(FICHIER_JOURNAL, encoding="utf-8") as f:
            lignes = [l.rstrip("\n").split(" | ") for l in f.readlines()[-12:]][::-1]
    boutons = [(a, r, l) for a, (r, _m, l) in ACTIONS.items() if a not in ("instantane", "revenir")]
    marche, snaps = etat_vms()
    return dict(vms=VMS, boutons=boutons, marche=marche,
                occupees=dict(en_cours), admin=session.get("role") == "admin",
                snaps=snaps, dernieres=lignes,
                nb_marche=sum(1 for v in VMS.values() if v["vmx"].lower() in marche),
                nb_snaps=sum(len(x) for x in snaps.values()))


PAGE_VIVANTE = ('<div class="barre-maj" id="barre-maj"><i></i><span id="maj">Mise à jour automatique toutes les 3 s</span></div>'
                '<div id="zone">' + PAGE_ACCUEIL + '</div>')


@app.route("/")
@connexion_requise
def accueil():
    return rendre(PAGE_VIVANTE, vivant=True, **contexte_accueil())


@app.route("/etat")
@connexion_requise
def etat():
    """Fragment HTML de l'etat des VM, relu par la page toutes les 3 s (lecture seule)."""
    r = app.make_response(render_template_string(PAGE_ACCUEIL, **contexte_accueil()))
    r.headers["X-Fragment"] = "1"
    return r


def executer(utilisateur, cle, action, args, description):
    vm = VMS[cle]["nom"]
    try:
        ok, sortie = vmrun(*args, timeout=600)
        journal(utilisateur, action, vm, "ok" if ok else "ECHEC " + sortie[:150])
    finally:
        en_cours.pop(cle, None)
        invalider_cache(cle)
        verrous[cle].release()


@app.route("/vm/<cle>/<action>", methods=["POST"])
@connexion_requise
def agir(cle, action):
    if cle not in VMS or action not in ACTIONS:
        abort(404)
    role_min, modele, libelle = ACTIONS[action]
    if role_min == "admin" and session.get("role") != "admin":
        journal(session["utilisateur"], action, VMS[cle]["nom"], "REFUSE (role)")
        abort(403)
    vmx = VMS[cle]["vmx"]
    if action == "instantane":
        extra = request.form.get("libelle", "").strip()
        if extra and not nom_instantane_valide(extra):
            flash("Nom d'instantane invalide (lettres, chiffres, espace, - _ . seulement).")
            return redirect(url_for("accueil"))
        nom = "{}-{}{}".format(session["utilisateur"], datetime.datetime.now().strftime("%Y%m%d-%H%M"),
                               "-" + extra if extra else "")
        args = ["snapshot", vmx, nom]
    elif action == "revenir":
        nom = request.form.get("instantane", "")
        if nom not in instantanes(vmx):
            flash("Instantane inconnu.")
            return redirect(url_for("accueil"))
        args = ["revertToSnapshot", vmx, nom]
    else:
        args = [a.format(vmx=vmx) for a in modele]
    if not verrous[cle].acquire(blocking=False):
        flash("Une operation est deja en cours sur {}.".format(VMS[cle]["nom"]))
        return redirect(url_for("accueil"))
    en_cours[cle] = libelle
    journal(session["utilisateur"], action, VMS[cle]["nom"], "lance")
    threading.Thread(target=executer, args=(session["utilisateur"], cle, action, args, libelle),
                     daemon=True).start()
    invalider_cache()
    flash("{} : {} lance (l'etat se met a jour tout seul).".format(VMS[cle]["nom"], libelle))
    return redirect(url_for("accueil"))


# ---------------------------------------------------------------- ligne de commande
def cli_utilisateur(args):
    u = charger_utilisateurs()
    if len(args) >= 2 and args[0] == "ajouter":
        nom = args[1].strip().lower()
        if not re.fullmatch(r"[a-z0-9_-]{2,20}", nom):
            sys.exit("Nom invalide (2 a 20 caracteres : lettres minuscules, chiffres, - _).")
        mdp = getpass.getpass("Mot de passe pour {} (12 caracteres minimum) : ".format(nom))
        if len(mdp) < 12:
            sys.exit("Mot de passe trop court.")
        if mdp != getpass.getpass("Confirme le mot de passe : "):
            sys.exit("Les deux saisies different.")
        u[nom] = {"hash": generate_password_hash(mdp), "role": "admin" if "--admin" in args else "membre"}
        enregistrer_utilisateurs(u)
        journal("console", "utilisateur ajoute", nom, u[nom]["role"])
        print("Utilisateur {} enregistre ({}).".format(nom, u[nom]["role"]))
    elif len(args) >= 2 and args[0] == "supprimer":
        if u.pop(args[1].strip().lower(), None) is None:
            sys.exit("Utilisateur inconnu.")
        enregistrer_utilisateurs(u)
        journal("console", "utilisateur supprime", args[1], "ok")
        print("Utilisateur supprime.")
    elif args and args[0] == "liste":
        for nom, info in sorted(u.items()):
            print("  {:<20} {}".format(nom, info["role"]))
    else:
        print(__doc__)


if __name__ == "__main__":
    if len(sys.argv) >= 2 and sys.argv[1] == "utilisateur":
        cli_utilisateur(sys.argv[2:])
    elif len(sys.argv) >= 2 and sys.argv[1] == "serve":
        if not os.path.exists(VMRUN):
            sys.exit("vmrun introuvable : " + VMRUN)
        app.secret_key = secret_cle()
        app.config.update(SESSION_COOKIE_HTTPONLY=True, SESSION_COOKIE_SAMESITE="Strict",
                          PERMANENT_SESSION_LIFETIME=datetime.timedelta(hours=8))
        from waitress import serve
        journal("systeme", "demarrage du panneau", "-", "{}:{}".format(ECOUTE, PORT))
        serve(app, host=ECOUTE, port=PORT, threads=4)
    else:
        print(__doc__)
