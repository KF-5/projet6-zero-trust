/* P6 Explorer — lecteur de commandes (un seul module, sans framework, Web Animations API).
   Toute la logique (chemins, NAT, verdicts, décodage des trames) est calculée en C# et arrive en JSON
   (api/etapes/{id}/commandes, api/simulation) ; ce fichier ne fait qu'animer et afficher. */
(function () {
  "use strict";

  const racine = document.getElementById("lecteur");
  if (!racine) return;
  const mode = racine.dataset.mode || "etape";
  const reduit = window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  const svg = racine.querySelector("svg.schema-svg");
  const anim = svg ? svg.querySelector(".calque-anim") : null;
  const NS = "http://www.w3.org/2000/svg";
  const $ = (id) => document.getElementById(id);

  let vitesse = parseFloat(racine.dataset.vitesse || "1") || 1;
  let generation = 0;                       // change à chaque nouvelle commande : les animations en cours s'arrêtent

  // ------------------------------------------------------------------ outils

  const attendre = (ms) => new Promise((ok) => setTimeout(ok, reduit ? 0 : ms / vitesse));
  const enCours = (g) => g === generation;

  function el(nom, attributs, texte) {
    const e = document.createElement(nom);
    for (const [k, v] of Object.entries(attributs || {})) {
      if (v === null || v === undefined || v === false) continue;
      if (k === "classe") e.className = v; else e.setAttribute(k, v);
    }
    if (texte !== undefined && texte !== null) e.textContent = texte;
    return e;
  }

  function elSvg(nom, attributs, texte) {
    const e = document.createElementNS(NS, nom);
    for (const [k, v] of Object.entries(attributs || {})) e.setAttribute(k, v);
    if (texte !== undefined) e.textContent = texte;
    return e;
  }

  function noeud(id) {
    return svg ? svg.querySelector('[data-noeud="' + CSS.escape(id) + '"]') : null;
  }

  function centre(id) {
    const g = noeud(id);
    if (!g) return null;
    const r = g.querySelector("rect");
    return { x: r.x.baseVal.value + r.width.baseVal.value / 2, y: r.y.baseVal.value + r.height.baseVal.value / 2, h: r.height.baseVal.value };
  }

  function pointsLien(a, b) {
    if (!svg) return null;
    let l = svg.querySelector('polyline[data-de="' + CSS.escape(a) + '"][data-vers="' + CSS.escape(b) + '"]');
    let envers = false;
    if (!l) { l = svg.querySelector('polyline[data-de="' + CSS.escape(b) + '"][data-vers="' + CSS.escape(a) + '"]'); envers = true; }
    if (!l) return null;
    const pts = l.getAttribute("points").trim().split(/\s+/).map((p) => { const [x, y] = p.split(",").map(Number); return { x, y }; });
    return envers ? pts.reverse() : pts;
  }

  /** Points à suivre de machine en machine : le câble quand il existe, sinon une ligne droite. */
  function trajet(chemin) {
    const points = [];
    for (let i = 0; i < chemin.length; i++) {
      const c = centre(chemin[i]);
      if (!c) continue;
      points.push(c);
      if (i + 1 < chemin.length) {
        const lien = pointsLien(chemin[i], chemin[i + 1]);
        if (lien) points.push(...lien);
      }
    }
    return points;
  }

  function nettoyer() {
    if (anim) anim.replaceChildren();
    if (svg) svg.querySelectorAll(".noeud.actif, .noeud.alerte").forEach((n) => n.classList.remove("actif", "alerte"));
  }

  function allumer(id) {
    const g = noeud(id);
    if (g) g.classList.add("actif");
  }

  function bulle(id, texte, classe) {
    const c = centre(id);
    if (!c || !anim) return;
    const g = elSvg("g", { class: "bulle " + (classe || "") });
    const t = elSvg("text", { x: c.x, y: c.y - c.h / 2 - 12, "text-anchor": "middle" }, texte.length > 60 ? texte.slice(0, 58) + "…" : texte);
    g.appendChild(t);
    anim.appendChild(g);
    const b = t.getBBox();
    g.insertBefore(elSvg("rect", { x: b.x - 6, y: b.y - 3, width: b.width + 12, height: b.height + 6, rx: 3 }), t);
    if (!reduit) g.animate([{ opacity: 0, transform: "translateY(4px)" }, { opacity: 1, transform: "none" }], { duration: 260, easing: "cubic-bezier(.16,1,.3,1)" });
  }

  function classeProto(p) {
    const n = (p || "").toLowerCase();
    if (n === "https" || n === "http") return "p-" + n;
    if (n === "syslog" || n === "ntp" || n === "dns" || n === "ssh" || n === "icmp") return "p-" + n;
    return "p-autre";
  }

  /** Fait voyager un point le long des points donnés, à vitesse constante. */
  async function voyager(points, classe, g) {
    if (!anim || points.length === 0) return;
    const p = elSvg("circle", { r: 6, class: "paquet " + classe, cx: 0, cy: 0 });
    anim.appendChild(p);
    if (reduit || points.length === 1) { p.setAttribute("transform", "translate(" + points.at(-1).x + " " + points.at(-1).y + ")"); return; }
    const distances = [0];
    for (let i = 1; i < points.length; i++) distances.push(distances[i - 1] + Math.hypot(points[i].x - points[i - 1].x, points[i].y - points[i - 1].y));
    const total = distances.at(-1) || 1;
    const images = points.map((pt, i) => ({ transform: "translate(" + pt.x + "px," + pt.y + "px)", offset: distances[i] / total }));
    const a = p.animate(images, { duration: Math.max(700, total * 3.2) / vitesse, easing: "linear", fill: "forwards" });
    try { await a.finished; } catch { /* animation annulée */ }
    if (!enCours(g)) p.remove();
  }

  function marque(id, verdict, regle) {
    const c = centre(id);
    if (!c || !anim) return;
    const ok = verdict === "Passe";
    const symbole = ok ? "✓" : verdict === "Refuse" ? "↩" : verdict === "Injoignable" ? "∅" : "✕";
    const g = elSvg("g", { class: "verdict-marque v-" + verdict.toLowerCase() });
    g.appendChild(elSvg("circle", { cx: c.x + 34, cy: c.y - c.h / 2 + 2, r: 11 }));
    g.appendChild(elSvg("text", { x: c.x + 34, y: c.y - c.h / 2 + 6.5, "text-anchor": "middle" }, symbole));
    anim.appendChild(g);
    if (regle && !(svg.classList.contains("sans-regles"))) bulle(id, (ok ? "passe · " : "") + regle, "regle v-" + verdict.toLowerCase());
    if (!reduit) g.animate([{ transform: "scale(.4)", opacity: 0 }, { transform: "scale(1)", opacity: 1 }], { duration: 300, easing: "cubic-bezier(.16,1,.3,1)" });
  }

  /** Anime un résultat du moteur : chemin, traduction NAT, verdict, ligne filterlog vers le collecteur. */
  async function animerPaquet(r, g) {
    const chemin = r.chemin.map((s) => s.noeud);
    if (!r.calcule || chemin.length === 0) {
      allumer(r.source);
      if (noeud(r.source)) bulle(r.source, "→ " + r.destination + " (hors du laboratoire simulé)", "info");
      return;
    }
    chemin.forEach(allumer);
    if (r.nat) {
      const i = chemin.indexOf("opnsense");
      if (i > 0) {
        await voyager(trajet(chemin.slice(0, i + 1)), classeProto(r.protocole), g);
        if (!enCours(g)) return;
        bulle("opnsense", "NAT " + r.nat.regle + " : " + r.nat.avant + " → " + r.nat.apres, "nat");
        await attendre(500);
        if (!enCours(g)) return;
        await voyager(trajet(chemin.slice(i)), classeProto(r.protocole), g);
      } else await voyager(trajet(chemin), classeProto(r.protocole), g);
    } else await voyager(trajet(chemin), classeProto(r.protocole), g);
    if (!enCours(g)) return;
    marque(chemin.at(-1), r.verdict, r.regle);
    if (r.filterlog && r.equipement === "opnsense" && !svg.classList.contains("sans-journaux")) {
      await attendre(250);
      if (enCours(g)) await voyager(trajet(["opnsense", "collecteur"]), "p-syslog", g);
    }
  }

  // ------------------------------------------------------------------ panneau d'explication

  function etiquetteVerdict(r) {
    return el("span", { classe: "verdict v-" + r.verdict.toLowerCase() }, r.verdictTexte);
  }

  function afficherPaquet(r, conteneur) {
    const bloc = el("div", { classe: "paquet-resultat" });
    const tete = el("p", { classe: "paquet-tete" });
    tete.append(el("strong", {}, r.source + " → " + r.destination + (r.port ? ":" + r.port : "") + " "), el("span", { classe: "note" }, r.protocole + " "), etiquetteVerdict(r));
    bloc.appendChild(tete);
    bloc.appendChild(el("p", { classe: "paquet-resume" }, r.resume));
    if (r.regle) {
      const p = el("p", { classe: "paquet-regle" });
      p.append("Règle qui décide : ", el("code", {}, r.regle), r.equipement ? " (sur " + r.equipement + ")" : "", r.filterlog ? " · une ligne filterlog part vers le collecteur" : "");
      bloc.appendChild(p);
    }
    if (r.nat) bloc.appendChild(el("p", { classe: "paquet-nat" }, "Traduction (" + r.nat.regle + ") : " + r.nat.avant + " → " + r.nat.apres));
    if (r.chemin.length) {
      const ol = el("ol", { classe: "chemin" });
      for (const s of r.chemin) {
        const li = el("li");
        li.append(el("strong", {}, s.noeud), " ", el("span", { classe: "note" }, [s.entree, s.sortie].filter(Boolean).join(" → ") + (s.vlan ? " · VLAN " + s.vlan : "")), el("br"), s.texte);
        ol.appendChild(li);
      }
      bloc.appendChild(ol);
    }
    bloc.appendChild(el("p", { classe: "paquet-explication" }, r.explication));
    conteneur.appendChild(bloc);
  }

  function afficherTrame(t) {
    const boite = $("decodeur");
    if (!boite) return;
    if (!t) { boite.hidden = true; return; }
    boite.hidden = false;
    $("decodeur-titre").textContent = "Trame octet par octet · " + t.longueur + " octets · " + t.titre;
    const hexa = $("decodeur-hexa"), champs = $("decodeur-champs");
    hexa.replaceChildren(); champs.replaceChildren();
    const coucheDe = new Array(t.longueur).fill("");
    t.couches.forEach((c) => { for (let i = c.debut; i < c.debut + c.longueur && i < t.longueur; i++) coucheDe[i] = c.code; });
    t.couches.forEach((c) => c.champs.forEach((f) => { for (let i = f.debut; i < f.debut + f.longueur && i < t.longueur; i++) if (!coucheDe[i]) coucheDe[i] = c.code; }));
    const octets = [];
    for (let i = 0; i < t.longueur; i++) {
      if (i % 16 === 0) hexa.appendChild(el("span", { classe: "decalage", "aria-hidden": "true" }, i.toString(16).padStart(4, "0")));
      const o = el("span", { classe: "octet c-" + (coucheDe[i] || "app"), "data-i": i }, t.hexa.substr(i * 2, 2));
      octets.push(o);
      hexa.appendChild(o);
    }
    const lignes = [];
    function surligner(d, l, actif) {
      for (let i = d; i < d + l && i < octets.length; i++) octets[i].classList.toggle("surligne", actif);
    }
    for (const c of t.couches) {
      const groupe = el("div", { classe: "couche c-" + c.code });
      groupe.appendChild(el("h4", {}, c.nom + " · octets " + c.debut + "–" + (c.debut + c.longueur - 1)));
      for (const f of c.champs) {
        const ligne = el("div", { classe: "champ-trame" + (f.exemple ? " exemple" : ""), tabindex: "0", "data-d": f.debut, "data-l": f.longueur });
        ligne.append(el("span", { classe: "champ-nom" }, f.nom), el("span", { classe: "champ-valeur" }, f.valeur), el("span", { classe: "champ-explication" }, f.explication));
        const entrer = () => { surligner(f.debut, f.longueur, true); ligne.classList.add("actif"); };
        const sortir = () => { surligner(f.debut, f.longueur, false); ligne.classList.remove("actif"); };
        ligne.addEventListener("mouseenter", entrer); ligne.addEventListener("focus", entrer);
        ligne.addEventListener("mouseleave", sortir); ligne.addEventListener("blur", sortir);
        lignes.push({ f, ligne });
        groupe.appendChild(ligne);
      }
      champs.appendChild(groupe);
    }
    hexa.addEventListener("mouseover", (e) => {
      const i = Number(e.target.dataset && e.target.dataset.i);
      if (Number.isNaN(i)) return;
      lignes.forEach(({ f, ligne }) => {
        const dedans = i >= f.debut && i < f.debut + f.longueur;
        ligne.classList.toggle("actif", dedans);
        surligner(f.debut, f.longueur, dedans);
      });
    });
    hexa.addEventListener("mouseleave", () => lignes.forEach(({ f, ligne }) => { ligne.classList.remove("actif"); surligner(f.debut, f.longueur, false); }));
  }

  function afficherResultat(r) {
    const zone = $("panneau-paquet");
    zone.replaceChildren();
    zone.hidden = false;
    afficherPaquet(r, zone);
    afficherTrame(r.trame);
  }

  // ------------------------------------------------------------------ mode étape : lecteur de commandes

  if (mode === "etape") {
    const ecran = $("terminal-ecran");
    const chronologie = $("chronologie");
    const boutonLecture = racine.querySelector('[data-action="lecture"]');
    const choixVitesse = $("vitesse");
    let commandes = [];
    let index = 0;
    let enLecture = false;
    const connecte = racine.dataset.connecte === "1";
    const modifier = racine.dataset.modifier === "1";
    const jeton = racine.dataset.jeton;

    function majBouton() {
      boutonLecture.textContent = enLecture ? "⏸" : "▶";
      boutonLecture.setAttribute("aria-label", enLecture ? "Pause (barre d'espace)" : "Lecture (barre d'espace)");
    }

    async function taper(c, g) {
      ecran.replaceChildren();
      const invite = el("span", { classe: "invite" }, c.invite + " ");
      const texte = el("span", { classe: "commande" + (c.graphique ? " graphique" : "") });
      ecran.append(invite, texte);
      if (c.niveau > 0) ecran.prepend(el("span", { classe: "sous-commande" }, "↳ dans " + c.parentCode + "\n"));
      const contenu = c.texte;
      if (reduit || c.graphique || contenu.length > 600) texte.textContent = contenu;
      else {
        const pas = Math.max(1, Math.ceil(contenu.length / 90));
        for (let i = 0; i < contenu.length; i += pas) {
          if (!enCours(g)) return;
          texte.textContent = contenu.slice(0, i + pas);
          await attendre(16);
        }
      }
      if (c.sortie && enCours(g)) {
        await attendre(250);
        if (!enCours(g)) return;
        const sortie = el("span", { classe: "sortie" }, "\n" + c.sortie);
        ecran.appendChild(sortie);
        if (!reduit) sortie.animate([{ opacity: 0 }, { opacity: 1 }], { duration: 300 });
      }
      ecran.scrollTop = 0;
    }

    function panneau(c) {
      $("panneau-titre").textContent = c.bloc || "Explication";
      $("panneau-code").textContent = c.code + (c.estTest ? " · test" : "");
      $("panneau-explication").innerHTML = c.explicationHtml || "";   // HTML encodé côté serveur (TexteRiche)
      const guide = $("panneau-guide");
      if (c.texteGuide) {
        guide.hidden = false;
        guide.replaceChildren(el("span", { classe: "note" }, "Dans le guide : "), el("code", {}, c.texteGuide.split("\n")[0]));
      } else guide.hidden = true;
      const liste = $("panneau-effets");
      liste.replaceChildren();
      for (const f of c.effets) liste.appendChild(el("li", { classe: "effet t-" + f.donnees.type }, f.resume));
      $("panneau-paquet").hidden = true;
      $("panneau-paquet").replaceChildren();
      afficherTrame(null);
      const actions = $("panneau-actions");
      actions.replaceChildren();
      if (connecte) {
        const compris = el("button", { type: "button", classe: "btn-lien", "aria-pressed": c.comprise ? "true" : "false" }, c.comprise ? "✓ comprise" : "J'ai compris");
        compris.addEventListener("click", async () => {
          const r = await fetch("/api/progression/" + c.id, { method: "POST", headers: { RequestVerificationToken: jeton } });
          if (r.ok) { c.comprise = !c.comprise; compris.textContent = c.comprise ? "✓ comprise" : "J'ai compris"; compris.setAttribute("aria-pressed", c.comprise ? "true" : "false"); }
        });
        actions.appendChild(compris);
        actions.appendChild(el("a", { href: "/Discussions/Nouvelle?commandeId=" + c.id, classe: "btn-lien" }, "Poser une question"));
      }
      if (c.questionsOuvertes > 0) actions.appendChild(el("a", { href: "/Discussions", classe: "pastille question" }, "? " + c.questionsOuvertes + " ouverte" + (c.questionsOuvertes > 1 ? "s" : "")));
      if (modifier) actions.appendChild(el("a", { href: "/Etapes/Commande/" + c.id, classe: "btn-lien" }, "Modifier"));
    }

    async function effet(f, c, g) {
      const d = f.donnees;
      if (f.noeud) allumer(f.noeud);
      switch (d.type) {
        case "paquet":
          if (f.paquet) { afficherResultat(f.paquet); await animerPaquet(f.paquet, g); }
          break;
        case "journal":
          if (!svg.classList.contains("sans-journaux")) await voyager(trajet([d.de, "collecteur"]), "p-syslog", g);
          break;
        case "alerteIds": {
          const o = noeud("opnsense");
          if (o && !svg.classList.contains("sans-alertes")) { o.classList.add("alerte"); bulle("opnsense", "Suricata : " + d.signature + (d.blocage ? " (bloqué)" : ""), "ids"); }
          break;
        }
        case "etatLien": {
          const l = svg.querySelector('polyline[data-lien="' + CSS.escape(d.lien) + '"]');
          if (l) l.classList.toggle("debranche", !d.branche);
          break;
        }
        case "etatNoeud": {
          const n = noeud(d.noeud);
          if (n) { n.classList.remove("etat-allumee", "etat-eteinte", "etat-suspendue", "etat-creee"); n.classList.add("etat-" + d.etat.toLowerCase()); }
          bulle(d.noeud, f.resume, "info");
          break;
        }
        case "resultatTest":
          ecran.appendChild(el("span", { classe: "resultat-test " + (d.ok ? "ok" : "ko") }, "\n" + (d.ok ? "✓ " : "✗ ") + f.resume));
          break;
        case "changementEtat": {
          const bandeau = $("bandeau-etat");
          bandeau.hidden = false;
          bandeau.replaceChildren(el("span", {}, "Cette commande fait passer le laboratoire à l'état " + d.etat + ". "));
          if (racine.dataset.etat !== d.etat) {
            const form = el("form", { method: "post", action: "/Preferences/Etat", classe: "en-ligne" });
            form.append(el("input", { type: "hidden", name: "etat", value: d.etat }), el("input", { type: "hidden", name: "retour", value: location.pathname + location.search }),
              el("input", { type: "hidden", name: "__RequestVerificationToken", value: jeton }), el("button", { type: "submit", classe: "btn-lien" }, "Afficher l'état " + d.etat));
            bandeau.appendChild(form);
          }
          break;
        }
        case "changementRegle":
          if (!svg.classList.contains("sans-regles")) bulle("opnsense", f.resume, "regle");
          break;
        default:
          if (f.noeud) bulle(f.noeud, f.resume, "info");
      }
      await attendre(d.type === "paquet" ? 350 : 650);
    }

    async function jouer(i, suite) {
      if (!commandes.length) return;
      index = Math.min(Math.max(0, i), commandes.length - 1);
      const g = ++generation;
      const c = commandes[index];
      chronologie.value = index + 1;
      $("terminal-compteur").textContent = (index + 1) + " / " + commandes.length;
      $("terminal-titre").textContent = c.ouNom + (c.compte ? " · " + c.compte : "");
      history.replaceState(null, "", racine.dataset.url + "?commande=" + (index + 1));
      nettoyer();
      $("bandeau-etat").hidden = true;
      allumer(c.ou);
      panneau(c);
      await taper(c, g);
      for (const f of c.effets) {
        if (!enCours(g)) return;
        await effet(f, c, g);
      }
      if (suite && enLecture && enCours(g)) {
        await attendre(900);
        if (enLecture && enCours(g)) {
          if (index + 1 < commandes.length) jouer(index + 1, true);
          else { enLecture = false; majBouton(); }
        }
      }
    }

    function lecture() {
      enLecture = !enLecture;
      majBouton();
      if (enLecture) jouer(index, true);
    }

    racine.querySelectorAll("[data-action]").forEach((b) => b.addEventListener("click", () => {
      const a = b.dataset.action;
      if (a === "lecture") return lecture();
      enLecture = false; majBouton();
      if (a === "suivante") jouer(index + 1, false);
      if (a === "precedente") jouer(index - 1, false);
      if (a === "debut") jouer(0, false);
    }));
    chronologie.addEventListener("input", () => { enLecture = false; majBouton(); jouer(Number(chronologie.value) - 1, false); });
    choixVitesse.value = String(vitesse);
    choixVitesse.addEventListener("change", () => {
      vitesse = parseFloat(choixVitesse.value) || 1;
      const corps = new URLSearchParams({ vitesse: String(vitesse) });
      fetch("/Preferences/Vitesse", { method: "POST", headers: { RequestVerificationToken: jeton, "Content-Type": "application/x-www-form-urlencoded" }, body: corps });
    });
    document.addEventListener("keydown", (e) => {
      if (e.target.closest("input, textarea, select, [contenteditable]")) return;
      if (e.key === " " && e.target.closest("button, a, summary, [tabindex]")) return;
      if (e.key === "ArrowRight") { e.preventDefault(); enLecture = false; majBouton(); jouer(index + 1, false); }
      else if (e.key === "ArrowLeft") { e.preventDefault(); enLecture = false; majBouton(); jouer(index - 1, false); }
      else if (e.key === " ") { e.preventDefault(); lecture(); }
    });

    fetch(racine.dataset.api, { headers: { Accept: "application/json" } })
      .then((r) => r.ok ? r.json() : Promise.reject(r.status))
      .then((liste) => {
        commandes = liste;
        chronologie.max = String(Math.max(1, liste.length));
        const code = racine.dataset.code;
        let depart = (parseInt(racine.dataset.initiale, 10) || 1) - 1;
        if (code) { const trouve = liste.findIndex((c) => c.code === code); if (trouve >= 0) depart = trouve; }
        if (liste.length) { index = Math.min(depart, liste.length - 1); jouer(index, false); }
        else ecran.textContent = "Aucune commande dans cette étape.";
      })
      .catch(() => { ecran.textContent = "Impossible de charger les commandes (API indisponible)."; });
  }

  // ------------------------------------------------------------------ mode « Et si ? »

  if (mode === "simulation") {
    const form = $("simulation-form");
    async function simuler() {
      const donnees = new FormData(form);
      const demande = {
        source: donnees.get("Source"), destination: donnees.get("Destination"), protocole: donnees.get("Protocole"),
        port: donnees.get("Port") ? Number(donnees.get("Port")) : null, etat: donnees.get("Etat"),
        mesures: donnees.get("Mesures") === "true", sshOpnsense: donnees.get("SshOpnsense") === "true", durcissementQ16: donnees.get("DurcissementQ16") === "true",
      };
      const r = await fetch("/api/simulation", { method: "POST", headers: { "Content-Type": "application/json", Accept: "application/json" }, body: JSON.stringify(demande) });
      if (!r.ok) return;
      const resultat = await r.json();
      const g = ++generation;
      nettoyer();
      $("panneau-titre").textContent = "Simulation · état " + demande.etat;
      $("panneau-code").textContent = "";
      $("panneau-explication").textContent = "";
      $("panneau-effets").replaceChildren();
      afficherResultat(resultat);
      await animerPaquet(resultat, g);
    }
    $("simuler").addEventListener("click", simuler);
    form.addEventListener("keydown", (e) => { if (e.key === "Enter" && e.target.tagName === "INPUT" && e.target.name !== "Titre") { e.preventDefault(); simuler(); } });
    const protocole = form.querySelector('[name="Protocole"]');
    const ports = { Http: 80, Https: 443, Dns: 53, Ssh: 22, PostgreSql: 5432, Ntp: 123, Syslog: 514, Iperf3: 5201, Smb: 445, Rdp: 3389 };
    protocole.addEventListener("change", () => { const p = ports[protocole.value]; const champ = form.querySelector('[name="Port"]'); if (p) champ.value = p; if (protocole.value === "Icmp") champ.value = ""; });
    if (new URLSearchParams(location.search).has("source")) simuler();
  }

  // ------------------------------------------------------------------ mode démonstration

  if (mode === "demo") {
    let liste = [];
    let i = 0;
    let enLecture = false;
    const bouton = racine.querySelector('[data-action="lecture"]');
    async function montrer(n, suite) {
      if (!liste.length) return;
      i = (n + liste.length) % liste.length;
      const g = ++generation;
      const s = liste[i];
      document.querySelectorAll("#liste-demo li").forEach((li) => li.classList.toggle("active", Number(li.dataset.id) === s.id));
      $("demo-position").textContent = (i + 1) + " / " + liste.length;
      nettoyer();
      $("panneau-titre").textContent = s.titre || (s.resultat.source + " → " + s.resultat.destination);
      $("panneau-code").textContent = "état " + s.etat;
      $("panneau-explication").textContent = s.etat === "A" ? "Pare-feu périmétrique." : "Zero Trust : microsegmentation.";
      $("panneau-effets").replaceChildren();
      afficherResultat(s.resultat);
      await animerPaquet(s.resultat, g);
      if (suite && enLecture && enCours(g) && i + 1 < liste.length) { await attendre(1800); if (enLecture && enCours(g)) montrer(i + 1, true); }
      else if (suite && i + 1 >= liste.length) { enLecture = false; bouton.textContent = "▶"; }
    }
    racine.querySelectorAll("[data-action]").forEach((b) => b.addEventListener("click", () => {
      const a = b.dataset.action;
      if (a === "lecture") { enLecture = !enLecture; bouton.textContent = enLecture ? "⏸" : "▶"; if (enLecture) montrer(i, true); return; }
      enLecture = false; bouton.textContent = "▶";
      montrer(a === "suivante" ? i + 1 : i - 1, false);
    }));
    fetch("/api/simulation/demo").then((r) => r.json()).then((l) => { liste = l; if (l.length) montrer(0, false); });
  }

  // ------------------------------------------------------------------ calques du schéma

  document.querySelectorAll("[data-calque]").forEach((c) => {
    const appliquer = () => {
      if (!svg) return;
      const nom = c.dataset.calque;
      if (nom === "ip") svg.classList.toggle("avec-ip", c.checked);
      else svg.classList.toggle("sans-" + nom, !c.checked);
    };
    c.addEventListener("change", appliquer);
    appliquer();
  });
})();
