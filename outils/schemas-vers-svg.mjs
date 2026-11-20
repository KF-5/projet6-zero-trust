// Rend les schémas du guide (sources/guide/schemas-*.js) en SVG statiques pour P6 Explorer.
// Le guide dessine ses schémas dans le navigateur ; ici, un petit DOM minimal suffit pour appeler
// draw() et steps() et récupérer le SVG de chaque état (A, B) avec les légendes de ses étapes.
// Les schémas « interactifs » (custom : simulateurs, éditeurs) ne sont pas rendus : leur question est gardée.
//
// Usage, à la racine du dépôt :  node outils/schemas-vers-svg.mjs
// Sortie : donnees/schemas/schemas.json (lu par l'application au démarrage).
import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";

const racine = process.cwd();
const guide = path.join(racine, "sources", "guide");

// ---------------------------------------------------------------- DOM minimal
class El {
  constructor(tag) { this.tag = tag; this.attrs = {}; this.children = []; this.text = null; this.parentNode = null; this.style = {}; this.dataset = {}; }
  setAttribute(k, v) { this.attrs[k] = String(v); }
  getAttribute(k) { return this.attrs[k] ?? null; }
  removeAttribute(k) { delete this.attrs[k]; }
  appendChild(c) { c.parentNode = this; this.children.push(c); return c; }
  append(...cs) { cs.forEach((c) => this.appendChild(typeof c === "string" ? Object.assign(new El("#text"), { text: c }) : c)); }
  insertBefore(c, ref) { c.parentNode = this; const i = this.children.indexOf(ref); this.children.splice(i < 0 ? this.children.length : i, 0, c); return c; }
  remove() { if (this.parentNode) this.parentNode.children = this.parentNode.children.filter((x) => x !== this); }
  set textContent(v) { this.text = String(v); this.children = []; }
  get textContent() { return this.text ?? this.children.map((c) => c.textContent).join(""); }
  set innerHTML(v) { this.children = []; this.text = null; }
  get classList() { const e = this; return { add: (...c) => { e.attrs.class = [e.attrs.class, ...c].filter(Boolean).join(" "); }, remove() {}, toggle() {}, contains: () => false }; }
  addEventListener() {}
  querySelector() { return new El("g"); }   // élément détaché : les retouches interactives sont ignorées
  querySelectorAll() { return []; }
  closest() { return null; }
  getBBox() { const t = this.textContent || ""; return { x: Number(this.attrs.x || 0), y: Number(this.attrs.y || 0) - 10, width: t.length * 6.2, height: 13 }; }
  getTotalLength() { return 100; }
  animate() { return { finished: Promise.resolve(), cancel() {} }; }
}
const echapper = (s) => String(s).replace(/&/g, "&amp;").replace(/</g, "&lt;").replace(/>/g, "&gt;").replace(/"/g, "&quot;");
function serialiser(e) {
  if (e.tag === "#text") return echapper(e.text ?? "");
  const attrs = Object.entries(e.attrs).map(([k, v]) => ` ${k}="${echapper(v)}"`).join("");
  const corps = e.text !== null ? echapper(e.text) : e.children.map(serialiser).join("");
  return `<${e.tag}${attrs}>${corps}</${e.tag}>`;
}

const documentFactice = {
  createElementNS: (_ns, tag) => new El(tag), createElement: (tag) => new El(tag), createTextNode: (t) => Object.assign(new El("#text"), { text: t }),
  addEventListener() {}, querySelector: () => null, querySelectorAll: () => [], getElementById: () => null,
  documentElement: new El("html"), body: new El("body"),
};
const fenetre = { matchMedia: () => ({ matches: false, addEventListener() {} }), addEventListener() {}, localStorage: { getItem: () => null, setItem() {} } };
const contexte = vm.createContext({ window: fenetre, document: documentFactice, localStorage: fenetre.localStorage, navigator: {}, console, setTimeout, clearTimeout, requestAnimationFrame: () => 0 });
fenetre.G = {};
contexte.G = fenetre.G;

for (const f of ["core.js", "schemas-base.js", ...fs.readdirSync(guide).filter((f) => f.startsWith("schemas-") && f !== "schemas-base.js").sort()]) {
  try { vm.runInContext(fs.readFileSync(path.join(guide, f), "utf8"), contexte, { filename: f }); }
  catch (e) { console.error(`Lecture de ${f} : ${e.message}`); }
}

// Seules quelques balises de mise en forme du guide sont gardées dans les légendes.
const nettoyerHtml = (h) => String(h ?? "").replace(/<(?!\/?(b|strong|i|em|code)>)[^>]+>/g, "");

const schemas = fenetre.G.schemas || {};
const resultat = {};
let rendus = 0, interactifs = 0;
for (const [nom, def] of Object.entries(schemas)) {
  const entree = { nom, question: nettoyerHtml(def.q), interactif: !!def.custom, vues: [], legende: (def.legend || []).map(([c, t]) => ({ couleur: c, texte: t })) };
  if (def.custom || typeof def.draw !== "function") { interactifs++; resultat[nom] = entree; continue; }
  const etats = def.ab ? ["A", "B"] : [def.ab0 || "A"];
  etats.forEach((st, i) => {
    const ctx = { st, etat: st, i: 0 };
    try {
      const svg = new El("svg");
      const [w, h] = def.vb || [1000, 600];
      Object.assign(svg.attrs, { viewBox: `0 0 ${w} ${h}`, role: "img", "aria-label": def.q || nom, class: "schema-guide-svg" });
      const g = svg.appendChild(new El("g"));
      def.draw(g, ctx);
      const etapes = typeof def.steps === "function" ? def.steps(ctx).map((s) => nettoyerHtml(s.cap)) : [];
      const libelle = def.ab ? (def.abLabels ? def.abLabels[i] : `État ${st}`) : null;
      entree.vues.push({ etat: st, libelle, svg: serialiser(svg), etapes, fin: nettoyerHtml(typeof def.fin === "function" ? def.fin(ctx) : def.fin) });
    } catch (e) { console.error(`Schéma ${nom} (${st}) : ${e.message}`); }
  });
  if (entree.vues.length) rendus++;
  resultat[nom] = entree;
}

const sortie = path.join(racine, "donnees", "schemas");
fs.mkdirSync(sortie, { recursive: true });
fs.writeFileSync(path.join(sortie, "schemas.json"), JSON.stringify(resultat, null, 1) + "\n");
console.log(`${rendus} schémas rendus en SVG, ${interactifs} interactifs (question seulement) → donnees/schemas/schemas.json`);
