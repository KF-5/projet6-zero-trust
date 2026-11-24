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
    return { x: r.x.baseVal.value + r.width.baseVal.value / 2, y: r.y.baseVal.value + r.height.baseVal.value / 2, w: r.width.baseVal.value, h: r.height.baseVal.value };
  }

  // ------------------------------------------------------------------ caméra et mise au point
  // La caméra anime le viewBox du schéma pour cadrer les machines de la commande (ou du paquet simulé),
  // puis la mise au point estompe tout ce qui ne participe pas.

  const vueComplete = svg ? svg.getAttribute("viewBox").trim().split(/\s+/).map(Number) : null;
  let vueActuelle = vueComplete ? vueComplete.slice() : null;
  let jetonCamera = 0;
  const adoucir = (t) => (t < 0.5 ? 4 * t * t * t : 1 - Math.pow(-2 * t + 2, 3) / 2);
  const cameraActive = () => svg && !svg.classList.contains("sans-camera");
  let boutonVue = null;

  function poserVue(v) {
    vueActuelle = v;
    svg.setAttribute("viewBox", v.map((n) => n.toFixed(1)).join(" "));
    if (boutonVue) boutonVue.hidden = v.every((n, i) => Math.abs(n - vueComplete[i]) < 0.5);
  }

  function deplacerCamera(cible) {
    if (!svg) return Promise.resolve();
    const jeton = ++jetonCamera;
    const depart = vueActuelle.slice();
    if (reduit || depart.every((n, i) => Math.abs(n - cible[i]) < 0.5)) { poserVue(cible); return Promise.resolve(); }
    const duree = Math.max(320, 760 / vitesse);
    return new Promise((ok) => {
      let t0 = null;                                       // horodatage de la première image (même horloge que t)
      let fini = false;
      const terminer = () => { if (!fini) { fini = true; ok(); } };
      function image(t) {
        if (fini) return;
        if (jeton !== jetonCamera) return terminer();
        if (t0 === null) t0 = t;
        const p = Math.min(1, Math.max(0, (t - t0) / duree)), e = adoucir(p);
        poserVue(depart.map((d, i) => d + (cible[i] - d) * e));
        if (p < 1) requestAnimationFrame(image); else terminer();
      }
      requestAnimationFrame(image);
      // Sans images (onglet en arrière-plan, navigateur sans écran) : on pose la vue finale et on continue.
      setTimeout(() => { if (!fini && jeton === jetonCamera) poserVue(cible); terminer(); }, duree + 80);
    });
  }

  const vueEnsemble = () => (vueComplete ? deplacerCamera(vueComplete) : Promise.resolve());

  /** Cadre les machines données (marges pour les bulles au-dessus), sans déformer : même rapport que la vue complète. */
  function cadrer(ids) {
    if (!svg) return Promise.resolve();
    if (!cameraActive()) return vueEnsemble();
    const boites = [...new Set(ids)].map(centre).filter(Boolean);
    if (!boites.length) return vueEnsemble();
    const [fx, fy, fw, fh] = vueComplete;
    let x0 = Math.min(...boites.map((b) => b.x - b.w / 2)) - 80, x1 = Math.max(...boites.map((b) => b.x + b.w / 2)) + 80;
    let y0 = Math.min(...boites.map((b) => b.y - b.h / 2)) - 70, y1 = Math.max(...boites.map((b) => b.y + b.h / 2)) + 45;
    const ratio = fw / fh;
    let w = Math.max(x1 - x0, fw * 0.5), h = Math.max(y1 - y0, fh * 0.5);
    if (w / h > ratio) h = w / ratio; else w = h * ratio;
    if (w >= fw * 0.88) return vueEnsemble();                // presque tout le schéma : pas la peine de zoomer
    const x = Math.min(Math.max((x0 + x1) / 2 - w / 2, fx), fx + fw - w);
    const y = Math.min(Math.max((y0 + y1) / 2 - h / 2, fy), fy + fh - h);
    return deplacerCamera([x, y, w, h]);
  }

  /** Pause avant le zoom (ms, indépendante de la vitesse) : on voit d'abord les machines concernées s'allumer
      dans la vue actuelle, puis la caméra s'approche. */
  const PAUSE_CAMERA = 1200;
  async function cadrerApresPause(ids, g) {
    if (!reduit && cameraActive()) await new Promise((ok) => setTimeout(ok, PAUSE_CAMERA));
    if (enCours(g)) await cadrer(ids);
  }

  function mettreAuPoint(ids) {
    if (!svg) return;
    const actifs = new Set(ids.filter(Boolean));
    svg.classList.toggle("mise-au-point", actifs.size > 0);
    svg.querySelectorAll(".noeud").forEach((n) => n.classList.toggle("implique", actifs.has(n.dataset.noeud)));
    svg.querySelectorAll(".lien").forEach((l) => l.classList.toggle("implique", actifs.has(l.dataset.de) && actifs.has(l.dataset.vers)));
  }

  /** Machines touchées par un résultat du moteur (chemin, collecteur si une ligne filterlog part). */
  function noeudsPaquet(r) {
    if (!r.calcule || !r.chemin.length) return [r.source];
    const ids = r.chemin.map((s) => s.noeud);
    if (aFilterlog(r)) ids.push("collecteur");
    return ids;
  }

  // Une même règle pour l'animation, le tableau et la vue Séquence.
  /** Réponse dessinée : connexion acceptée, sur plusieurs machines, protocole qui répond (pas syslog). */
  function aReponse(r) {
    return r.calcule && r.verdict === "Passe" && r.chemin.length > 1 && !SANS_REPONSE.has((r.protocole || "").toLowerCase()) && !svg.classList.contains("sans-reponses");
  }
  /** Ligne filterlog : OPNsense journalise sa décision vers le collecteur. */
  function aFilterlog(r) {
    return r.filterlog && r.equipement === "opnsense" && !svg.classList.contains("sans-journaux");
  }

  if (svg) {
    const figure = svg.closest("figure");
    if (figure) {
      boutonVue = el("button", { type: "button", classe: "btn-vue", hidden: true, title: "Revenir à tout le schéma (double-clic sur le schéma)" }, "⤢ Vue d'ensemble");
      boutonVue.addEventListener("click", vueEnsemble);
      figure.appendChild(boutonVue);
    }
    svg.addEventListener("dblclick", vueEnsemble);
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

  function elementLien(a, b) {
    return svg.querySelector('polyline[data-de="' + CSS.escape(a) + '"][data-vers="' + CSS.escape(b) + '"]')
      || svg.querySelector('polyline[data-de="' + CSS.escape(b) + '"][data-vers="' + CSS.escape(a) + '"]');
  }

  /** Points à suivre de machine en machine : le câble quand il existe, sinon une ligne droite.
      Le tableau porte aussi .liens : les câbles empruntés, que le paquet allume au passage. */
  function trajet(chemin) {
    const points = [];
    points.liens = [];
    for (let i = 0; i < chemin.length; i++) {
      const c = centre(chemin[i]);
      if (!c) continue;
      points.push(c);
      if (i + 1 < chemin.length) {
        const lien = pointsLien(chemin[i], chemin[i + 1]);
        if (lien) { points.push(...lien); points.liens.push(elementLien(chemin[i], chemin[i + 1])); }
      }
    }
    return points;
  }

  function nettoyer() {
    if (anim) anim.replaceChildren();
    if (svg) {
      svg.querySelectorAll(".noeud.actif, .noeud.alerte").forEach((n) => n.classList.remove("actif", "alerte"));
      svg.querySelectorAll(".lien.parcouru").forEach((l) => { l.classList.remove("parcouru", "en-cours"); l.style.removeProperty("--pcol"); });
      mettreAuPoint([]);
    }
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

  /** Attend la fin d'une animation, ou sa durée si le navigateur ne dessine pas (onglet en arrière-plan). */
  function finie(a, duree) {
    return Promise.race([a.finished.catch(() => {}), new Promise((ok) => setTimeout(ok, duree + 120))]);
  }

  const NOMS_PROTO = { https: "HTTPS", http: "HTTP", dns: "DNS", ssh: "SSH", postgresql: "PostgreSQL", ntp: "NTP", syslog: "syslog", iperf3: "iperf3", smb: "SMB", rdp: "RDP", icmp: "ping" };
  const CHIFFRES = new Set(["https", "ssh"]);
  const SANS_REPONSE = new Set(["syslog"]);                 // syslog en UDP : rien ne revient

  function etiquetteRequete(r) {
    const p = (r.protocole || "").toLowerCase();
    const nom = NOMS_PROTO[p] || (r.transport ? r.transport.toUpperCase() : r.protocole) || "paquet";
    return p === "icmp" ? nom : nom + (r.port ? " :" + r.port : "");
  }

  /** Pastille au-dessus du paquet : protocole et port, cadenas si le contenu est chiffré. */
  function pastille(groupe, texte, couleur, chiffre) {
    const e = elSvg("g", { class: "paquet-etiquette" });
    groupe.appendChild(e);
    const t = elSvg("text", { x: chiffre ? 5 : 0, y: -14, "text-anchor": "middle" }, texte);
    e.appendChild(t);
    const b = t.getBBox();
    const gauche = b.x - (chiffre ? 13 : 5);
    e.insertBefore(elSvg("rect", { x: gauche, y: b.y - 2.5, width: b.x + b.width + 5 - gauche, height: b.height + 5, rx: 3, style: "fill:" + couleur }), t);
    if (chiffre) {
      const x = b.x - 9, y = b.y + b.height / 2 + 2;
      e.appendChild(elSvg("path", { class: "cadenas", d: "M" + (x - 3) + "," + (y - 1) + " h6 v5 h-6 z M" + (x - 2) + "," + (y - 1) + " v-2 a2,2 0 0 1 4,0 v2" }));
    }
  }

  /** Fait voyager un paquet le long des points, à vitesse constante : pastille, traînée, câbles allumés.
      Rend le groupe et sa position d'arrivée (le paquet reste là jusqu'à arriver() ou tomber()). */
  async function voyager(points, classe, g, options) {
    const o = options || {};
    if (!anim || points.length === 0) return null;
    const groupe = elSvg("g", { class: "paquet-groupe" });
    const point = elSvg("circle", { r: 6, class: "paquet " + classe, cx: 0, cy: 0 });
    groupe.appendChild(point);
    anim.appendChild(groupe);
    const couleur = getComputedStyle(point).fill;
    if (o.etiquette && !svg.classList.contains("sans-etiquettes")) pastille(groupe, o.etiquette, couleur, o.chiffre);
    const fin = points.at(-1);
    const liens = (points.liens || []).filter(Boolean);
    liens.forEach((l) => { l.style.setProperty("--pcol", couleur); l.classList.add("parcouru", "en-cours"); });
    if (reduit || points.length === 1) {
      groupe.setAttribute("transform", "translate(" + fin.x + " " + fin.y + ")");
      liens.forEach((l) => l.classList.remove("en-cours"));
      return { groupe, fin, couleur };
    }
    const distances = [0];
    for (let i = 1; i < points.length; i++) distances.push(distances[i - 1] + Math.hypot(points[i].x - points[i - 1].x, points[i].y - points[i - 1].y));
    const total = distances.at(-1) || 1;
    const images = points.map((pt, i) => ({ transform: "translate(" + pt.x + "px," + pt.y + "px)", offset: distances[i] / total }));
    const duree = Math.max(700, total * 3.2) / vitesse;
    const trainee = [4.6, 3.6, 2.6].map((r, k) => {
      const c = elSvg("circle", { r, class: "paquet-trainee", style: "fill:" + couleur + ";opacity:" + (0.5 - k * 0.14) });
      anim.insertBefore(c, groupe);
      c.animate(images, { duration: duree, delay: (k + 1) * 60 / vitesse, easing: "linear", fill: "both" });
      return c;
    });
    const a = groupe.animate(images, { duration: duree, easing: "linear", fill: "forwards" });
    await finie(a, duree);
    await attendre(200);
    trainee.forEach((c) => c.remove());
    liens.forEach((l) => l.classList.remove("en-cours"));
    if (!enCours(g)) { groupe.remove(); return null; }
    groupe.setAttribute("transform", "translate(" + fin.x + " " + fin.y + ")");
    a.cancel();
    return { groupe, fin, couleur };
  }

  /** Onde au pied d'une machine : le paquet est arrivé. */
  function onde(id, couleur) {
    const c = centre(id);
    if (!c || !anim || reduit) return;
    const e = elSvg("ellipse", { class: "onde", cx: c.x, cy: c.y + c.h * 0.2, rx: 14, ry: 7, style: "stroke:" + couleur });
    anim.insertBefore(e, anim.firstChild);
    e.animate([{ transform: "scale(.5)", opacity: 0.9 }, { transform: "scale(3.2)", opacity: 0 }], { duration: 900 / vitesse, easing: "cubic-bezier(.16,1,.3,1)" }).finished.then(() => e.remove(), () => e.remove());
  }

  /** Le paquet entre dans la machine : il rétrécit et disparaît, une onde part. */
  async function arriver(v, id) {
    if (!v) return;
    onde(id, v.couleur);
    if (reduit) { v.groupe.remove(); return; }
    const a = v.groupe.animate([{ opacity: 1 }, { opacity: 0 }], { duration: 260 / vitesse, fill: "forwards" });
    await finie(a, 260 / vitesse);
    v.groupe.remove();
  }

  /** Paquet jeté : il tombe sous une croix rouge. */
  async function tomber(v) {
    if (!v) return;
    const { x, y } = v.fin;
    const croix = elSvg("path", { class: "chute-croix", d: "M-6,-6 L6,6 M6,-6 L-6,6", transform: "translate(" + x + " " + y + ")" });
    anim.appendChild(croix);
    if (reduit) { v.groupe.remove(); return; }
    croix.animate([{ opacity: 0 }, { opacity: 1 }], { duration: 200 / vitesse });
    const a = v.groupe.animate([{ transform: "translate(" + x + "px," + y + "px)", opacity: 1 }, { transform: "translate(" + x + "px," + (y + 26) + "px)", opacity: 0 }],
      { duration: 560 / vitesse, easing: "cubic-bezier(.5,0,.75,0)", fill: "forwards" });
    await finie(a, 560 / vitesse);
    v.groupe.remove();
  }

  /** Inverse un trajet (points et câbles) pour la réponse. */
  function inverse(points) {
    const r = points.slice().reverse();
    r.liens = (points.liens || []).slice().reverse();
    return r;
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
  /** Anime un résultat du moteur : chemin, traduction NAT, verdict, réponse, ligne filterlog vers le collecteur.
      cadrage = true : la caméra cadre d'abord les machines du paquet (modes « Et si ? » et démonstration). */
  async function animerPaquet(r, g, cadrage) {
    const chemin = r.chemin.map((s) => s.noeud);
    if (cadrage) { mettreAuPoint(noeudsPaquet(r)); await cadrerApresPause(noeudsPaquet(r), g); if (!enCours(g)) return; }
    flux.activer(r, "requete");
    if (!r.calcule || chemin.length === 0) {
      allumer(r.source);
      if (noeud(r.source)) bulle(r.source, "→ " + r.destination + " (hors du laboratoire simulé)", "info");
      return;
    }
    chemin.forEach(allumer);
    const proto = (r.protocole || "").toLowerCase();
    const classe = classeProto(r.protocole);
    const chiffre = CHIFFRES.has(proto);
    const etiquette = etiquetteRequete(r);
    let v;
    const i = r.nat ? chemin.indexOf("opnsense") : -1;
    if (i > 0) {
      // Avant la traduction, le paquet vise l'adresse publique ; après, l'adresse interne.
      v = await voyager(trajet(chemin.slice(0, i + 1)), classe, g, { etiquette: "→ " + r.nat.avant, chiffre });
      if (!enCours(g)) return;
      if (v) v.groupe.remove();
      bulle("opnsense", "NAT " + r.nat.regle + " : " + r.nat.avant + " → " + r.nat.apres, "nat");
      await attendre(600);
      if (!enCours(g)) return;
      v = await voyager(trajet(chemin.slice(i)), classe, g, { etiquette: "→ " + r.nat.apres, chiffre });
    } else v = await voyager(trajet(chemin), classe, g, { etiquette, chiffre });
    if (!enCours(g)) return;
    const passe = r.verdict === "Passe";
    if (passe) await arriver(v, chemin.at(-1)); else await tomber(v);
    if (!enCours(g)) return;
    marque(chemin.at(-1), r.verdict, r.regle);
    // Réponse : le pare-feu est à états, la réponse d'une connexion acceptée repasse sans nouvelle règle.
    if (aReponse(r)) {
      await attendre(350);
      if (!enCours(g)) return;
      flux.activer(r, "reponse");
      const retour = await voyager(inverse(trajet(chemin)), "p-reponse", g, { etiquette: proto === "icmp" ? "réponse ping" : "réponse", chiffre });
      if (!enCours(g)) return;
      await arriver(retour, chemin[0]);
    }
    if (aFilterlog(r)) {
      await attendre(250);
      if (!enCours(g)) return;
      flux.activer(r, "filterlog");
      const j = await voyager(trajet(["opnsense", "collecteur"]), "p-syslog", g, { etiquette: "filterlog" });
      if (enCours(g)) await arriver(j, "collecteur");
    }
  }

  // ------------------------------------------------------------------ flux de paquets (tableau) et vue « Séquence »
  // Une ligne par paquet de la commande (ou de la simulation), synchronisée avec l'animation, à la manière d'un
  // captureur de trames ; la vue Séquence montre les mêmes paquets saut par saut, une colonne par machine.
  // Les valeurs (adresses, ports, drapeaux, taille) viennent de la trame calculée par le décodeur, côté serveur.

  function champTrame(t, code, debut) {
    const c = t && t.couches.find((x) => x.code === code);
    const f = c && c.champs.find((x) => x.nom.startsWith(debut));
    return f ? f.valeur : null;
  }
  const premierMot = (v) => (v || "").split(" ")[0];
  function nomNoeud(id) {
    const n = noeud(id);
    return n ? (n.getAttribute("aria-label") || id).split(",")[0] : id;
  }
  function nomProtocole(r) {
    const p = (r.protocole || "").toLowerCase(), t = (r.transport || "").toLowerCase();
    if (p === "icmp") return "ICMP";
    if (!NOMS_PROTO[p] && (p === t || !p)) return (t || "?").toUpperCase();          // protocole générique : TCP, UDP
    return (NOMS_PROTO[p] || r.protocole) + (t ? " / " + t.toUpperCase() : "");
  }

  function infoRequete(r) {
    const t = r.trame, morceaux = [];
    const transport = t && t.couches.find((c) => c.code === "tcp" || c.code === "udp");
    if (transport) {
      const drapeaux = transport.code === "tcp" ? champTrame(t, "tcp", "Drapeaux") : null;
      morceaux.push(premierMot(champTrame(t, transport.code, "Port source")) + " → " + premierMot(champTrame(t, transport.code, "Port destination")) + (drapeaux ? " [" + drapeaux + "]" : ""));
    }
    const app = t && t.couches.find((c) => c.code === "app");
    if (app) morceaux.push(app.nom);
    if (r.nat) morceaux.push("NAT " + r.nat.regle + " → " + r.nat.apres);
    if (!morceaux.length) morceaux.push(r.resume);
    return morceaux.join(" · ");
  }

  /** Lignes d'un résultat du moteur : la requête, puis la réponse et la ligne filterlog quand l'animation les montre. */
  function entreesPaquet(r) {
    const chemin = r.calcule ? r.chemin.map((s) => s.noeud) : [];
    const ipSource = champTrame(r.trame, "ip", "Adresse source");
    const cible = r.destination + (r.port ? ":" + r.port : "");
    const i = r.nat ? chemin.indexOf("opnsense") : -1;
    const sauts = [];
    for (let k = 0; k + 1 < chemin.length; k++)
      sauts.push({ de: chemin[k], vers: chemin[k + 1], etiquette: i > 0 ? "→ " + (k < i ? r.nat.avant : r.nat.apres) : k === 0 ? etiquetteRequete(r) : "" });
    const liste = [{
      objet: r, sorte: "requete", classe: classeProto(r.protocole), resultat: r,
      source: [nomNoeud(r.source), ipSource], destination: [cible, r.arrivee && r.arrivee !== r.destination ? nomNoeud(r.arrivee) : null],
      protocole: nomProtocole(r), info: infoRequete(r), taille: r.trame ? r.trame.longueur + " o" : "—", verdict: r,
      sauts, fin: chemin.length ? { noeud: chemin.at(-1), r } : { noeud: r.source, horsLabo: "→ " + cible + " (hors du laboratoire simulé)" },
    }];
    if (aReponse(r)) {
      const retour = chemin.slice().reverse();
      liste.push({
        objet: r, sorte: "reponse", classe: "p-reponse", resultat: r, deduite: true,
        source: [nomNoeud(chemin.at(-1)), r.nat ? r.nat.apres : cible], destination: [nomNoeud(r.source), ipSource],
        protocole: nomProtocole(r), info: "réponse : la connexion est déjà connue du pare-feu (à états), aucune règle n'est réévaluée", taille: "—", verdict: null,
        sauts: retour.slice(0, -1).map((de, k) => ({ de, vers: retour[k + 1], etiquette: k === 0 ? "réponse" : "" })), fin: { noeud: retour.at(-1) },
      });
    }
    if (aFilterlog(r)) liste.push({
      objet: r, sorte: "filterlog", classe: "p-syslog", source: [nomNoeud("opnsense"), null], destination: [nomNoeud("collecteur"), null],
      protocole: "syslog / UDP", info: "filterlog : " + r.verdictTexte + (r.regle ? " · règle " + r.regle : ""), taille: "—", verdict: null,
      sauts: [{ de: "opnsense", vers: "collecteur", etiquette: "filterlog" }], fin: { noeud: "collecteur" },
    });
    return liste;
  }

  function entreeJournal(f) {
    const d = f.donnees;
    return {
      objet: f, sorte: "journal", classe: "p-syslog", source: [nomNoeud(d.de), null], destination: [nomNoeud("collecteur"), null],
      protocole: "syslog / UDP", info: f.resume, taille: "—", verdict: null,
      sauts: [{ de: d.de, vers: "collecteur", etiquette: "syslog" }], fin: { noeud: "collecteur" },
    };
  }

  const flux = (function () {
    const figure = svg ? svg.closest("figure") : null;
    if (!figure) return { charger() {}, activer() {}, terminer() {} };

    // Onglets Réseau / Séquence, posés sur le cadre du schéma.
    const onglets = el("div", { classe: "vues-lecteur", role: "tablist", "aria-label": "Vue du lecteur" });
    const ongletReseau = el("button", { type: "button", role: "tab", "aria-selected": "true" }, "Réseau");
    const ongletSequence = el("button", { type: "button", role: "tab", "aria-selected": "false" }, "Séquence");
    onglets.append(ongletReseau, ongletSequence);
    figure.prepend(onglets);
    const sequence = el("div", { classe: "sequence", hidden: true, tabindex: "0", "aria-label": "Diagramme de séquence des paquets" });
    svg.after(sequence);
    let enSequence = false;
    function montrerVue(seq) {
      if (seq && !enSequence) sequence.style.height = Math.max(280, svg.getBoundingClientRect().height - 40) + "px";
      enSequence = seq;
      ongletReseau.setAttribute("aria-selected", String(!seq));
      ongletSequence.setAttribute("aria-selected", String(seq));
      svg.style.display = seq ? "none" : "";
      sequence.hidden = !seq;
      figure.classList.toggle("en-sequence", seq);
      if (seq) { dessinerSequence(); majEtats(); }
    }
    ongletReseau.addEventListener("click", () => montrerVue(false));
    ongletSequence.addEventListener("click", () => montrerVue(true));
    if (location.hash === "#sequence") requestAnimationFrame(() => montrerVue(true));   // lien direct vers la vue Séquence

    // Tableau « Flux de paquets », sous le schéma (sous les contrôles quand il y en a).
    const section = el("section", { classe: "flux", "aria-label": "Flux de paquets" });
    const tete = el("div", { classe: "flux-tete" });
    tete.append(el("h3", {}, "Flux de paquets"), el("span", { classe: "flux-note" }, "un paquet par ligne · clique une ligne pour voir sa trame octet par octet"));
    const defilement = el("div", { classe: "flux-defilement" });
    const table = el("table", { classe: "flux-table" });
    const entete = el("tr");
    ["N°", "Source", "Destination", "Protocole", "Info", "Taille", "Verdict"].forEach((t) => entete.appendChild(el("th", { scope: "col" }, t)));
    const thead = el("thead");
    thead.appendChild(entete);
    const corps = el("tbody");
    table.append(thead, corps);
    defilement.appendChild(table);
    const vide = el("p", { classe: "flux-vide" }, "Aucun paquet ne circule pour cette commande.");
    section.append(tete, defilement, vide);
    (racine.querySelector(".controles") || figure).after(section);

    let entrees = [], courant = -1, inspecte = -1;

    function cellule(valeurs) {
      const td = el("td");
      td.appendChild(el("span", {}, valeurs[0] || ""));
      if (valeurs[1]) td.appendChild(el("span", { classe: "flux-adresse" }, valeurs[1]));
      return td;
    }

    function charger(liste) {
      entrees = liste; courant = -1; inspecte = -1;
      corps.replaceChildren();
      vide.hidden = liste.length > 0;
      defilement.hidden = liste.length === 0;
      liste.forEach((e, i) => {
        const tr = el("tr", { classe: "a-venir" + (e.deduite ? " deduite" : ""), tabindex: "0", "data-i": i });
        if (e.deduite) tr.title = "Ligne ajoutée par le lecteur : la réponse d'une connexion acceptée (pare-feu à états).";
        const n = el("td");
        n.appendChild(el("span", { classe: "flux-num " + e.classe }, String(i + 1)));
        const v = el("td");
        if (e.verdict) v.appendChild(etiquetteVerdict(e.verdict)); else v.textContent = "—";
        tr.append(n, cellule(e.source), cellule(e.destination), el("td", {}, e.protocole), el("td", { classe: "flux-info" }, e.info), el("td", { classe: "flux-taille" }, e.taille), v);
        tr.addEventListener("click", () => inspecter(i));
        tr.addEventListener("keydown", (ev) => { if (ev.key === "Enter") inspecter(i); });
        corps.appendChild(tr);
      });
      defilement.scrollTop = 0;
      sequence.scrollTop = 0;
      if (enSequence) dessinerSequence();
    }

    function etats(elt, i) {
      elt.classList.toggle("passe", i < courant);
      elt.classList.toggle("courant", i === courant);
      elt.classList.toggle("a-venir", i > courant);
      elt.classList.toggle("inspecte", i === inspecte);
    }

    function majEtats() {
      [...corps.children].forEach((tr, i) => etats(tr, i));
      sequence.querySelectorAll(".seq-entree").forEach((g) => etats(g, Number(g.dataset.i)));
      const tr = corps.children[courant];
      if (tr) {
        const haut = tr.offsetTop - thead.offsetHeight, bas = tr.offsetTop + tr.offsetHeight;
        if (haut < defilement.scrollTop || bas > defilement.scrollTop + defilement.clientHeight) defilement.scrollTop = Math.max(0, haut - defilement.clientHeight / 3);
      }
      const e = entrees[courant];
      const ye = e && e.y !== undefined ? e.y + 38 : null;     // 38 : hauteur de l'en-tête collé
      if (enSequence && ye !== null && (ye < sequence.scrollTop + 60 || ye > sequence.scrollTop + sequence.clientHeight - 40))
        sequence.scrollTop = Math.max(0, ye - sequence.clientHeight / 3);
    }

    function activer(objet, sorte) {
      const i = entrees.findIndex((e) => e.objet === objet && e.sorte === sorte);
      if (i < 0) return;
      courant = i;
      majEtats();
    }

    function terminer() { courant = entrees.length; majEtats(); }

    function inspecter(i) {
      inspecte = i;
      majEtats();
      if (entrees[i] && entrees[i].resultat) afficherResultat(entrees[i].resultat);
    }

    // Diagramme de séquence : une colonne par machine (dans l'ordre où elles apparaissent), une flèche par saut.
    function dessinerSequence() {
      sequence.replaceChildren();
      if (!entrees.length) { sequence.appendChild(el("p", { classe: "flux-vide" }, "Aucun paquet ne circule pour cette commande.")); return; }
      const colonnes = [];
      const ajouter = (id) => { if (id && !colonnes.includes(id)) colonnes.push(id); };
      entrees.forEach((e) => { e.sauts.forEach((s) => { ajouter(s.de); ajouter(s.vers); }); ajouter(e.fin.noeud); });
      const marge = 44, dispo = (sequence.clientWidth || figure.clientWidth || 800) - 16;
      const pas = Math.max(118, (dispo - marge) / colonnes.length);
      const X = (id) => marge + pas * (colonnes.indexOf(id) + 0.5);
      const largeur = Math.max(dispo, marge + pas * colonnes.length);
      const largeurTexte = (t, taille) => t.length * taille * 0.6;     // police à chasse fixe : 0,6 em par caractère

      // En-tête (noms des machines) à part, collé en haut quand on fait défiler.
      const entete = elSvg("svg", { class: "seq-entete", width: largeur, height: 38, viewBox: "0 0 " + largeur + " 38", "aria-hidden": "true" });
      colonnes.forEach((id) => {
        const x = X(id);
        const zone = ((noeud(id) && noeud(id).getAttribute("class").match(/\bz-(\w+)/)) || [])[1] || "exterieur";
        const g = elSvg("g", { class: "seq-machine z-" + zone });
        const nom = nomNoeud(id), l = largeurTexte(nom, 11) + 16;
        g.append(elSvg("rect", { x: x - l / 2, y: 6, width: l, height: 24, rx: 3 }), elSvg("text", { x, y: 22, "text-anchor": "middle" }, nom));
        entete.appendChild(g);
      });
      sequence.appendChild(entete);

      // Positions verticales : 34 par saut (ou pour la boucle d'un paquet resté sur sa machine), plus le verdict d'une requête.
      let y = 22;
      entrees.forEach((e) => {
        e.y = y;
        y += Math.max(1, e.sauts.length) * 34 + (e.sorte === "requete" ? 14 : 0) + 10;
      });
      const hauteur = y + 6;
      const dessin = elSvg("svg", { class: "seq-svg", width: largeur, height: hauteur, viewBox: "0 0 " + largeur + " " + hauteur, role: "img", "aria-label": "Séquence des paquets entre les machines" });

      colonnes.forEach((id) => dessin.appendChild(elSvg("line", { class: "seq-ligne", x1: X(id), y1: 0, x2: X(id), y2: hauteur })));

      entrees.forEach((e, i) => {
        const g = elSvg("g", { class: "seq-entree a-venir " + e.classe, "data-i": i, tabindex: "0" });
        g.appendChild(elSvg("title", {}, (i + 1) + ". " + e.protocole + " · " + e.info));
        g.append(elSvg("circle", { class: "seq-num", cx: 18, cy: e.y, r: 9 }), elSvg("text", { class: "seq-num-texte", x: 18, y: e.y + 3.5, "text-anchor": "middle" }, String(i + 1)));
        let yy = e.y;
        e.sauts.forEach((s) => {
          const x1 = X(s.de), x2 = X(s.vers), sens = Math.sign(x2 - x1) || 1;
          g.appendChild(elSvg("line", { class: "seq-fleche", x1, y1: yy, x2: x2 - sens * 7, y2: yy }));
          g.appendChild(elSvg("polygon", { class: "seq-pointe", points: x2 + "," + yy + " " + (x2 - sens * 9) + "," + (yy - 4.5) + " " + (x2 - sens * 9) + "," + (yy + 4.5) }));
          if (s.etiquette) {
            const l = largeurTexte(s.etiquette, 10) + 10, xm = (x1 + x2) / 2;
            g.append(elSvg("rect", { class: "seq-etiquette", x: xm - l / 2, y: yy - 19, width: l, height: 14, rx: 3 }),
              elSvg("text", { class: "seq-etiquette-texte", x: xm, y: yy - 8.5, "text-anchor": "middle" }, s.etiquette));
          }
          yy += 34;
        });
        const xf = X(e.fin.noeud), yf = e.sauts.length ? yy - 34 : yy;
        const boucle = !e.sauts.length;                   // paquet resté sur sa machine (127.0.0.1, tunnel local…)
        if (boucle) {
          g.appendChild(elSvg("path", { class: "seq-fleche", fill: "none", d: "M" + xf + "," + (yf - 7) + " h26 v14 h-19" }));
          g.appendChild(elSvg("polygon", { class: "seq-pointe", points: xf + "," + (yf + 7) + " " + (xf + 9) + "," + (yf + 2.5) + " " + (xf + 9) + "," + (yf + 11.5) }));
        }
        const xt = boucle ? xf + 34 : xf;
        if (e.fin.horsLabo) g.appendChild(elSvg("text", { class: "seq-verdict", x: xt, y: yf + 4 }, e.fin.horsLabo));
        else if (e.fin.r) {
          const r = e.fin.r, ok = r.verdict === "Passe";
          if (!ok && !boucle) g.appendChild(elSvg("path", { class: "seq-croix", d: "M" + (xf - 6) + "," + (yf - 6) + " L" + (xf + 6) + "," + (yf + 6) + " M" + (xf + 6) + "," + (yf - 6) + " L" + (xf - 6) + "," + (yf + 6) }));
          const texte = (ok ? "✓ " : "✕ ") + r.verdictTexte + (r.regle ? " · " + r.regle : "");
          g.appendChild(boucle
            ? elSvg("text", { class: "seq-verdict v-" + r.verdict.toLowerCase(), x: xt, y: yf + 4 }, etiquetteRequete(r) + " · " + texte)
            : elSvg("text", { class: "seq-verdict v-" + r.verdict.toLowerCase(), x: xf, y: yf + 18, "text-anchor": "middle" }, texte));
        }
        g.addEventListener("click", () => inspecter(i));
        g.addEventListener("keydown", (ev) => { if (ev.key === "Enter") inspecter(i); });
        dessin.appendChild(g);
      });
      sequence.appendChild(dessin);
    }

    return { charger, activer, terminer };
  })();

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
          if (!svg.classList.contains("sans-journaux")) {
            flux.activer(f, "journal");
            const j = await voyager(trajet([d.de, "collecteur"]), "p-syslog", g, { etiquette: "syslog" });
            if (enCours(g)) await arriver(j, "collecteur");
          }
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

    /** Toutes les machines qu'une commande touche : là où elle s'exécute, chemins des paquets, collecteur, liens… */
    function noeudsCommande(c) {
      const ids = [c.ou];
      for (const f of c.effets) {
        const d = f.donnees;
        if (f.noeud) ids.push(f.noeud);
        if (d.type === "paquet" && f.paquet) ids.push(...noeudsPaquet(f.paquet));
        else if (d.type === "journal" && !svg.classList.contains("sans-journaux")) ids.push(d.de, "collecteur");
        else if (d.type === "alerteIds" || d.type === "changementRegle") ids.push("opnsense");
        else if (d.type === "etatNoeud") ids.push(d.noeud);
        else if (d.type === "etatLien") {
          const l = svg.querySelector('polyline[data-lien="' + CSS.escape(d.lien) + '"]');
          if (l) ids.push(l.dataset.de, l.dataset.vers);
        }
      }
      return ids.filter(Boolean);
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
      const impliques = noeudsCommande(c);
      mettreAuPoint(impliques);
      const camera = cadrerApresPause(impliques, g);       // pause, puis la caméra glisse pendant que la commande s'écrit
      panneau(c);
      flux.charger(c.effets.flatMap((f) =>
        f.donnees.type === "paquet" && f.paquet ? entreesPaquet(f.paquet)
          : f.donnees.type === "journal" && !svg.classList.contains("sans-journaux") ? [entreeJournal(f)] : []));
      await taper(c, g);
      await camera;                                        // les paquets ne partent qu'une fois le zoom fini
      for (const f of c.effets) {
        if (!enCours(g)) return;
        await effet(f, c, g);
      }
      if (enCours(g)) flux.terminer();
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
      flux.charger(entreesPaquet(resultat));
      await animerPaquet(resultat, g, true);
      if (enCours(g)) flux.terminer();
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
      flux.charger(entreesPaquet(s.resultat));
      await animerPaquet(s.resultat, g, true);
      if (enCours(g)) flux.terminer();
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
      if (nom === "camera" && !c.checked) vueEnsemble();
    };
    c.addEventListener("change", appliquer);
    appliquer();
  });
})();
