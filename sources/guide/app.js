/* Rendu des pages à partir des données (data/*.js) et routage par ancres. */
(function(){
"use strict";
const {$, $$, esc} = G;
const main = () => $("#main");
const role = id => G.ROLES.find(r => r.id === id);
const rc = r => `var(${r.c})`;
G.modLink = n => `<a class="chip" href="https://claude.ai/artifact/${G.MOD[n]}" target="_blank" rel="noopener">Module ${n} · ${G.MODT[n]}</a>`;
G.stepLink = sid => { const [r, n] = sid.split("-"); const R = role(r); const st = G.roles[r] && G.roles[r].steps[+n - 1]; return `<a class="chip" href="#role-${sid}">${R ? R.n + "." : ""}${n} ${st ? esc(st.titre) : sid}</a>`; };
const verif = t => `<span class="verif" title="Dépend de la version : vérifiez sur la vôtre">à vérifier sur votre version${t ? " · " + t : ""}</span>`;

/* coloration minimale : commentaires en gris */
function code(src){
  return esc(src).split("\n").map(l => {
    const t = l.trimStart();
    if (/^(#|\/\/|;|--)/.test(t) && !/^#!/.test(t)) return `<span class="cm">${l}</span>`;
    const m = l.match(/^(.*?\S)(\s{2,}(#|\/\/|;|--) .*)$/);
    return m ? `${m[1]}<span class="cm">${m[2]}</span>` : l;
  }).join("\n");
}
G.codeBlock = (c) => `<div class="code"><div class="ct">${c.t ? esc(c.t) : ""}${c.file ? ` <code>${esc(c.file)}</code>` : ""}${c.verif ? " " + verif(c.verif === true ? "" : c.verif) : ""}</div><button class="copy noprint" type="button">Copier</button><pre>${code(c.code)}</pre>${c.note ? `<p class="muted" style="font-size:13.5px">${c.note}</p>` : ""}</div>`;

/* ---------- progression ---------- */
function roleProgress(rid){
  const R = G.roles[rid]; if (!R) return [0, 0];
  let n = 0, k = 0; [[R.prep, -1], ...R.steps.map((s, i) => [s, i])].forEach(([s, i]) => s && (s.fini || []).forEach((_, j) => { n++; if (G.checks[`${rid}-${i+1}-${j}`]) k++; }));
  return [k, n];
}

/* ---------- en-tête ---------- */
function nav(cur){
  $("#nav").innerHTML = `<a href="#accueil" ${cur === "accueil" ? `aria-current="page"` : ""}>Accueil</a>` +
    G.ROLES.map(r => `<a href="#role-${r.id}" ${cur === r.id ? `aria-current="page"` : ""}><span class="dot" style="background:${rc(r)}"></span>${r.n} · ${r.court}</a>`).join("") +
    `<a href="#contrats" ${cur === "contrats" ? `aria-current="page"` : ""}>Contrats</a>`;
}

/* ---------- accueil ---------- */
function home(){
  nav("accueil");
  const V = G.VERSIONS;
  main().innerHTML = `
  <section class="blk">
    <p class="eyebrow">Projet 6 · GL3 INSAT · remise le 30 novembre 2026</p>
    <h1>Guide de réalisation</h1>
    <p class="lead">Six rôles, une seule architecture. Choisissez votre rôle : vous y trouverez, étape par étape, quoi installer, comment configurer, comment tester, et quoi livrer à qui. Les valeurs (adresses, VLAN, flux, identifiants) viennent toutes du rapport de conception.</p>
    <div class="versions">${Object.values(V).map(v => `<span>${v}</span>`).join("")}</div>
    <div class="callout"><p><b>Avant de commencer.</b> Versions vérifiées le 12 novembre 2026. Les menus d'OPNsense 26.7 ont changé (nouvelle page d'affectation des interfaces, nouvelle page de règles, « Destination NAT ») : tout chemin de menu incertain porte la mention ${verif()}. Les écarts entre le rapport et la réalité technique sont listés dans <code>ECARTS.md</code> (30 points, E01 à E30) ; le guide applique les corrections proposées et le signale.</p></div>
    ${G.REEL ? `<div class="callout maj">${G.REEL}</div>` : ""}
  </section>
  <section class="blk"><h2>L'architecture et qui en est responsable</h2><div id="h-archi"></div></section>
  <section class="blk"><h2>Les six rôles</h2><div class="grid3" id="h-roles"></div></section>
  <section class="blk"><h2>Pourquoi cette répartition</h2>
    <p class="lead" style="font-size:16px">Chaque rôle représente 28 à 31 heures de travail, et le chemin critique ne repose jamais sur un seul rôle surchargé. Cinq ajustements par rapport à la répartition initiale :</p>
    <div class="tbl"><table><thead><tr><th>Ajustement</th><th>Raison</th></tr></thead><tbody>${G.REPARTITION.map(([a, b]) => `<tr><td>${a}</td><td>${b}</td></tr>`).join("")}</tbody></table></div>
  </section>
  <section class="blk"><h2>Planning commun du 13 au 30 novembre</h2><div id="h-gantt"></div></section>
  <section class="blk"><h2>Interfaces entre rôles : qui livre quoi à qui</h2>
    <div class="tbl"><table><thead><tr><th>De</th><th>À</th><th>Livrable</th><th>Format / emplacement</th><th>Pour le</th></tr></thead><tbody>${G.INTERFACES.map(([d, a, q, f, w]) => `<tr><td>${who(d)}</td><td>${who(a)}</td><td>${q}</td><td><code>${esc(f)}</code></td><td class="mono">${w}</td></tr>`).join("")}</tbody></table></div>
    <p class="muted" style="font-size:14px">Les formats des fichiers échangés sont fixés sur la page <a href="#contrats">Contrats</a>.</p>
  </section>
  <footer>Sources : rapport de conception (référence), diagrammes conception/uml, cours en 13 modules. Progression enregistrée dans ce navigateur seulement.</footer>`;
  G.mountSchema($("#h-archi"), "home-archi");
  G.mountSchema($("#h-gantt"), "home-gantt");
  $("#h-roles").innerHTML = G.ROLES.map(r => {
    const [k, n] = roleProgress(r.id), R = G.roles[r.id];
    return `<article class="card rolecard" style="--rc:${rc(r)}"><h3><span class="num">${r.n}</span><a href="#role-${r.id}" style="color:inherit">${r.titre}</a></h3>
      <p class="why">${r.mission}</p>
      <div class="chips">${r.comp.map(c => `<span class="chip">${esc(c)}</span>`).join("")}</div>
      <div class="meta"><span>≈ ${r.charge} h</span><span>${R ? R.steps.length + " étapes" : ""}</span><span>${r.versions.map(v => G.VERSIONS[v]).join(" · ")}</span></div>
      <div><p class="eyebrow" style="margin-bottom:4px">Modules à revoir</p><div class="chips">${r.modules.map(G.modLink).join("")}</div></div>
      <div><div class="prog"><div style="width:${n ? 100 * k / n : 0}%"></div></div><p class="ts" style="font-size:12px;margin-top:3px">${k} / ${n} vérifications cochées</p></div>
      <a class="btn pri" href="#role-${r.id}" style="text-align:center;text-decoration:none">Entrer dans l'espace ${r.n}</a></article>`;
  }).join("");
}
function who(s){ if (s === "tous") return "tous"; return s.split(",").map(x => x.trim()).map(x => { const r = role(x); return r ? `<a href="#role-${r.id}" style="color:${rc(r)};font-weight:600">${r.n} · ${r.court}</a>` : x; }).join(", "); }

/* ---------- contrats ---------- */
function contrats(){
  nav("contrats");
  main().innerHTML = `<section class="blk"><p class="eyebrow">Commun à tous les rôles</p><h1>Contrats de données</h1>
    <p class="lead">Les fichiers qui passent d'un rôle à l'autre ont un format fixe. Ne changez pas une colonne sans prévenir tous les consommateurs du fichier.</p></section>
    <section class="blk"><h2>Qui produit quoi, qui le lit</h2><div id="c-flux"></div></section>
    ${G.CONTRATS.map(c => `<section class="blk contract" id="contrat-${c.id}"><h2><code>${esc(c.f)}</code></h2>
      <p>Produit par ${who(c.prod.split(" (")[0])}${c.prod.includes("(") ? " (" + c.prod.split(" (")[1] : ""} · lu par ${c.cons.map(x => { const id = x.split(" (")[0]; const r = role(id); return r ? `<a href="#role-${r.id}" style="color:${rc(r)};font-weight:600">${r.n} · ${r.court}</a>${x.includes("(") ? " (" + x.split(" (")[1] : ""}` : x; }).join(", ")}</p>
      <div class="cols">${c.cols.map(x => `<code>${x}</code>`).join("")}</div>
      <ul>${c.regles.map(x => `<li>${x}</li>`).join("")}</ul>
      ${G.codeBlock({t:"Exemple", code:c.ex})}</section>`).join("")}
    <section class="blk" id="contrat-arbo"><h2>Arborescence des résultats et nommage des captures</h2>
      ${G.codeBlock({t:"Arborescence", code:G.ARBO})}
      <div class="tbl"><table><thead><tr><th>Point de capture</th><th>Où tcpdump écoute</th></tr></thead><tbody>${G.POINTS.map(([p, d]) => `<tr><td class="mono">${p}</td><td>${d}</td></tr>`).join("")}</tbody></table></div>
      <p>Nom d'un PCAP : <code>&lt;run_id&gt;_&lt;point&gt;.pcap</code>, par exemple <code>B-S05-03_postgres.pcap</code>. Dates des dossiers : <code>AAAA-MM-JJ</code> du début de la campagne.</p>
    </section>`;
  G.mountSchema($("#c-flux"), "contrats-flux");
}

/* ---------- espace de rôle ---------- */
function rolePage(rid, open){
  const r = role(rid), R = G.roles[rid];
  nav(rid);
  if (!r || !R){ main().innerHTML = `<p>Rôle introuvable.</p>`; return; }
  const [k, n] = roleProgress(rid);
  const recv = G.INTERFACES.filter(x => x[1] === rid || x[1] === "tous" || x[1].split(",").map(s => s.trim()).includes(rid));
  const give = G.INTERFACES.filter(x => x[0] === rid || x[0] === "tous");
  main().innerHTML = `
  <section class="blk rolehead" style="--rc:${rc(r)}">
    <p class="eyebrow">Rôle ${r.n} · ≈ ${r.charge} h · ${R.steps.length} étapes</p>
    <h1>${r.titre}</h1>
    <p class="lead">${r.mission}</p>
    <div class="versions">${r.versions.map(v => `<span>${G.VERSIONS[v]}</span>`).join("")}</div>
    <div class="chips">${r.comp.map(c => `<span class="chip">${esc(c)}</span>`).join("")}</div>
    <div><p class="eyebrow" style="margin-bottom:4px">Modules du cours à revoir</p><div class="chips">${r.modules.map(G.modLink).join("")}</div></div>
    <div class="noprint"><div class="prog" style="--rc:${rc(r)}"><div id="rprog" style="width:${n ? 100 * k / n : 0}%"></div></div><p class="ts" id="rprog-t" style="font-size:12px;margin-top:3px">${k} / ${n} vérifications cochées</p></div>
    ${R.intro ? `<div class="callout info">${R.intro}</div>` : ""}
  </section>
  <section class="blk grid2">
    <div class="card"><h3>Ce que vous recevez</h3><ul style="margin-top:8px">${recv.map(x => `<li>${who(x[0])} : ${x[2]} <span class="muted">(${x[4]})</span></li>`).join("") || "<li>—</li>"}</ul></div>
    <div class="card"><h3>Ce que vous livrez</h3><ul style="margin-top:8px">${give.map(x => `<li>→ ${who(x[1])} : ${x[2]} <span class="muted">(${x[4]})</span></li>`).join("") || "<li>—</li>"}</ul><p style="margin-top:8px;font-size:14px">Formats : page <a href="#contrats">Contrats</a>.</p></div>
  </section>
  <section class="blk" style="--rc:${rc(r)}"><h2>Étapes</h2>${R.prep ? stepHTML(rid, R.prep, -1) : ""}${R.steps.map((s, i) => stepHTML(rid, s, i)).join("")}</section>
  <section class="blk" style="--rc:${rc(r)}"><h2>Démonstration de ce rôle (2 minutes)</h2>
    <div class="card"><p>${R.demo.intro}</p><ol style="margin-top:8px">${R.demo.etapes.map(e => `<li>${e}</li>`).join("")}</ol>${R.demo.secours ? `<p class="muted" style="margin-top:8px">Secours : ${R.demo.secours}</p>` : ""}</div></section>
  <section class="blk qa"><h2>Cinq questions probables du professeur</h2>${R.questions.map(([q, a]) => `<details><summary>${q}</summary><p>${a}</p></details>`).join("")}</section>
  <footer>Rôle ${r.n} · ${r.titre}. Plateforme réelle : VMware Workstation Pro sur l'hôte Windows (mise à jour du 17 novembre). Les mentions « à vérifier sur votre version » signalent tout ce qui dépend d'OPNsense 26.7, de VMware ou de Docker 26.</footer>`;
  /* schémas montés à l'ouverture d'une étape (et à l'impression) */
  $$("details.step").forEach(d => {
    const mount = () => { const h = d.querySelector(".schema-host"); if (h && !h.dataset.done){ h.dataset.done = "1"; G.mountSchema(h, h.dataset.id, h.dataset.st || null); } };
    d.addEventListener("toggle", () => { if (d.open) mount(); });
    if (d.open) mount();
  });
  window.addEventListener("beforeprint", () => $$(".schema-host").forEach(h => { if (!h.dataset.done){ h.dataset.done = "1"; G.mountSchema(h, h.dataset.id, h.dataset.st || null); } }), {once:true});
  $$(".fini input").forEach(cb => cb.addEventListener("change", () => {
    G.setCheck(cb.dataset.id, cb.checked);
    const [k2, n2] = roleProgress(rid); $("#rprog").style.width = `${n2 ? 100 * k2 / n2 : 0}%`; $("#rprog-t").textContent = `${k2} / ${n2} vérifications cochées`;
    const st = cb.closest("details.step"), all = G.$$(".fini input", st), c = all.filter(x => x.checked).length;
    st.querySelector(".done").textContent = c === all.length ? "✓ terminée" : `${c} / ${all.length}`;
  }));
  if (open){ const d = document.getElementById(`role-${rid}-${open}`); if (d){ d.open = true; d.scrollIntoView({block:"start"}); } }
  else window.scrollTo(0, 0);
}


/* ---------- dépendances entre membres ---------- */
function depOf(rid, s, i){
  const id = `${rid}-${i + 1}`, d = (G.DEP || {})[id];
  if (!d) return null;
  const att = d.attend || ((s.prereq || {}).etapes || []).filter(e => e.split("-")[0] !== rid);
  return {id, d, att, M: G.DEPMODE[d.m]};
}
function depHTML(rid, s, i){
  const x = depOf(rid, s, i); if (!x) return "";
  const avec = (x.d.avec || []).map(r => { const R = role(r); return R ? `<span class="chip">${R.n} · ${esc(R.court)}</span>` : ""; }).join("");
  return `<div class="dep dep-${x.d.m}"><p class="dep-t"><b>${x.M.l}</b> · ${x.M.d}</p>
   ${x.att.length ? `<p class="dep-l">Attend :</p><div class="chips">${x.att.map(G.stepLink).join("")}</div>` : `<p class="dep-l">N'attend aucun autre membre.</p>`}
   ${avec ? `<p class="dep-l">Avec :</p><div class="chips">${avec}</div>` : ""}
   ${x.d.n ? `<p class="dep-n">${x.d.n}</p>` : ""}</div>`;
}
function stepHTML(rid, s, i){
  const id = `role-${rid}-${i + 1}`, fin = s.fini || [];
  const c = fin.filter((_, j) => G.checks[`${rid}-${i+1}-${j}`]).length;
  const part = (n, lab, body) => `<div class="part"><p class="lab" data-n="${n}">${lab}</p>${body}</div>`;
  const pre = s.prereq || {};
  return `<details class="step" id="${id}">
  <summary><span class="n">${i + 1}</span><span class="t">${esc(s.titre)}</span>${s.quand ? `<span class="d">${s.quand}</span>` : ""}${s.fait ? `<span class="depchip fait">${s.fait === true ? "déjà fait" : s.fait}</span>` : ""}${(() => { const x = depOf(rid, s, i); return x ? `<span class="depchip dep-${x.d.m}">${x.M.l}</span>` : ""; })()}<span class="done">${fin.length && c === fin.length ? "✓ terminée" : `${c} / ${fin.length}`}</span><a class="ts noprint" href="#${id}" title="Lien direct vers cette étape" onclick="event.stopPropagation()">lien</a></summary>
  <div class="sbody">
   ${s.maj ? `<div class="callout maj"><p class="maj-t">Mise à jour du 17 novembre · plateforme réelle</p>${s.maj}</div>` : ""}
   ${depHTML(rid, s, i)}
   ${part(1, "But", `<p>${s.but}</p>`)}
   ${part(2, "Pourquoi", `<p>${s.pourquoi}</p>`)}
   ${part(3, "Prérequis", `<div class="chips">${(pre.etapes || []).map(G.stepLink).join("") || `<span class="muted">aucune étape d'un autre rôle</span>`}</div>${pre.texte ? `<p style="font-size:14.5px">${pre.texte}</p>` : ""}<div class="chips">${(pre.modules || []).map(G.modLink).join("")}</div>`)}
   ${s.schema ? part(4, "Schéma", `<div class="schema-host" data-id="${s.schema}" data-st="${s.schemaSt || ""}"></div>`) : part(4, "Schéma", `<p class="muted">Pas de schéma propre à cette étape : voir ${(pre.modules || []).length ? "les modules liés" : "l'étape précédente"}.</p>`)}
   ${part(5, "Installer / configurer", (s.ui || []).map(u => `<div class="ui"><span class="path">${esc(u.path)}</span>${u.verif ? verif() : ""}<span>${u.faire}</span></div>`).join("") + (s.config || []).map(G.codeBlock).join(""))}
   ${part(6, "Tester", (s.tester || []).map(t => `<div class="test">${G.codeBlock({t:t.t || "Commande", code:t.cmd})}${t.sortie ? G.codeBlock({t:"Sortie attendue (exemple)", code:t.sortie}) : ""}${t.sens ? `<dl>${t.sens.map(([a, b]) => `<dt>${esc(a)}</dt><dd>${b}</dd>`).join("")}</dl>` : ""}</div>`).join(""))}
   ${s.captures ? `<div class="shots"><p class="lab">Capture pour le rapport</p><ul>${s.captures.map(([f, q]) => `<li><code>rapport/captures/${esc(f)}</code> : ${q}</li>`).join("")}</ul></div>` : ""}
   ${part(7, "Si ça ne marche pas", (s.pannes || []).map(p => `<div class="panne"><div><b>Symptôme</b>${p[0]}</div><div><b>Diagnostic</b>${p[1]}</div><div><b>Correction</b>${p[2]}</div></div>`).join(""))}
   ${part(8, "Terminé quand", `<ul class="fini">${fin.map((f, j) => `<li><label><input type="checkbox" data-id="${rid}-${i+1}-${j}"${G.checks[`${rid}-${i+1}-${j}`] ? " checked" : ""}><span>${f}</span></label></li>`).join("")}</ul>`)}
   ${part(9, "Livrer", `<div class="livrer">${s.livrer}</div>`)}
  </div></details>`;
}

/* ---------- routeur ---------- */
function route(){
  const h = (location.hash || "#accueil").slice(1);
  if (h === "contrats" || h.startsWith("contrat-")){ contrats(); if (h !== "contrats"){ const e = document.getElementById(h); if (e) e.scrollIntoView(); } else window.scrollTo(0, 0); return; }
  const m = h.match(/^role-([a-z]+)(?:-(\d+))?$/);
  if (m && role(m[1])){ rolePage(m[1], m[2]); return; }
  home(); window.scrollTo(0, 0);
}
window.addEventListener("hashchange", route);
document.addEventListener("DOMContentLoaded", () => { G.initTheme(); route(); });
})();
