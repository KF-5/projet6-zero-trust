// outils/guide-vers-json.mjs — convertit les données du guide (sources/guide/data/*.js) en JSON brut.
// Usage, à la racine du dépôt :  node outils/guide-vers-json.mjs
// Écrit donnees/guide/role-1.json … role-6.json, lus ensuite par P6Explorer.Importeur (.NET).
// Les fichiers du guide sont exécutés dans un bac à sable (vm) : ils ne font que remplir l'objet G.
import fs from "node:fs";
import path from "node:path";
import vm from "node:vm";
import { fileURLToPath } from "node:url";

const racine = path.resolve(path.dirname(fileURLToPath(import.meta.url)), "..");
const dossierGuide = path.join(racine, "sources", "guide", "data");
const sortie = path.join(racine, "donnees", "guide");

const contexte = { window: {} };
contexte.window.G = {};
contexte.G = contexte.window.G;
vm.createContext(contexte);

const fichiers = ["home", "role-chef", "role-parefeu", "role-ids", "role-services", "role-attaques", "role-analyse"];
for (const f of fichiers) {
  const code = fs.readFileSync(path.join(dossierGuide, `${f}.js`), "utf8");
  vm.runInContext(code, contexte, { filename: `${f}.js`, timeout: 2000 });
}

const G = contexte.G;
fs.mkdirSync(sortie, { recursive: true });
let total = 0;
for (const meta of G.ROLES) {
  const donnees = G.roles[meta.id];
  if (!donnees) throw new Error(`Rôle absent du guide : ${meta.id}`);
  const etapes = [];
  if (donnees.prep) etapes.push({ numero: 0, ...donnees.prep });               // rôle 3 : étape 0 (préparation)
  donnees.steps.forEach((s, i) => etapes.push({ numero: i + 1, ...s }));
  const role = {
    numero: meta.n, cle: meta.id, titre: meta.titre, court: meta.court, couleur: meta.c,
    mission: meta.mission, intro: donnees.intro ?? null, demo: donnees.demo ?? null,
    questions: donnees.questions ?? [], etapes,
  };
  fs.writeFileSync(path.join(sortie, `role-${meta.n}.json`), JSON.stringify(role, null, 1) + "\n", "utf8");
  total += etapes.length;
  console.log(`rôle ${meta.n} (${meta.id}) : ${etapes.length} étapes`);
}
console.log(`Total : ${total} étapes dans ${path.relative(racine, sortie)}`);
